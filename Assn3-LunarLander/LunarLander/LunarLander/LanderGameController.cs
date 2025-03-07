using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Xna.Framework;
using static LunarLander.GameState;
using static LunarLander.MenuState;

namespace LunarLander
{
    public class LanderGameController
    {
        // the lander object for this game
        public Lander Lander;

        // the input handler that handles keyboard input
        public readonly InputHandler InputHandler;

        // moon gravity: https://en.wikipedia.org/wiki/Moon
        private const float MoonGravity = 1.62f;    // m/(s^2)

        // the board size in units (and meters)
        public const float BoardSize = 120f;

        // lander start position
        private readonly (float x, float y) _startPosition = (BoardSize * 0.1f, BoardSize * 0.9f);

        // whether terrain has been generated or not
        public bool TerrainGenerated = false;

        // whether the LanderGame should recalculate its terrain on the next render cycle
        public bool RecalculateTerrain = false;

        // List<> containing ordered pairs, representing the terrain
        public List<(float x, float y)> TerrainList;

        // holds the safe zone information
        public List<(float x_start, float x_stop)> SafeZones;

        // holds game state information
        public GameState GameState;
        public TimeSpan LoadingTime;
        public int CurrentLevel;

        // holds menu state information
        public readonly MainMenuController MainMenuController;
        public readonly PauseMenuController PauseMenuController;

        // holds the score (total fuel remaining)
        public float RunningScore;

        // flags whether lander is in safe area or not
        public bool InSafeArea = false;

        // thresholds for safe landings
        public const float MaxSpeed = 4.0f;
        public const float MaxAngle = 5f;
        public const float MinAngle = 355f;

        // controller for data loading
        public LocalStorageManager LocalStorageManager;

        // flag for whether thrust is on
        public bool ThrustOn = false;

        // flags for sounds
        public bool PlayCrashSound = false;
        public bool PlaySuccessSound = false;

        // controller for drawing particles
        public ParticleDrawController ParticleDrawController;

        // constructor just creates the input handler
        public LanderGameController()
        {
            InputHandler = new InputHandler();
            MainMenuController = new MainMenuController(this);
            PauseMenuController = new PauseMenuController(this);
            LocalStorageManager = new LocalStorageManager();

            // in constructor, read in the control scheme and high scores initially
            LocalStorageManager.LoadControlScheme();

            // need to wait for a control scheme to be loaded
            while (LocalStorageManager.StoredControlScheme == null)
            {
                Thread.Sleep(10);   // Added Sleep so that this thread can "breathe" will Async happens
            }

            // set the correct control scheme after load
            InputHandler.ThrustUpButton.BoundKeys = LocalStorageManager.StoredControlScheme.ThrustKeys;
            InputHandler.TurnShipLeftButton.BoundKeys = LocalStorageManager.StoredControlScheme.RotateLeftKeys;
            InputHandler.TurnShipRightButton.BoundKeys = LocalStorageManager.StoredControlScheme.RotateRightKeys;

            // next, wait for high scores to be loaded
            LocalStorageManager.LoadHighScores();

            // need to wait for high scores to be loaded
            while (LocalStorageManager.StoredHighScores == null)
            {
                Thread.Sleep(10);   // Added Sleep so that this thread can "breathe" will Async happens
            }

            // set the high scores list to what we just read in
            MainMenuController.HighScoresFloatList = LocalStorageManager.StoredHighScores.HighScores;
        }

        // use this function to actually start a level
        public void StartLevel(int difficultyLevel)
        {
            Lander = new Lander(_startPosition);

            // if this is level 1, then reset the score
            if (difficultyLevel == 1)
                RunningScore = 0f;

            // set load time to zero when starting level
            LoadingTime = TimeSpan.Zero;

            // generate the terrain according to difficulty
            GenerateTerrain(difficultyLevel);

            // set proper difficulty level
            CurrentLevel = difficultyLevel;

            // start the game
            GameState = Running;
        }

        // level 1: two safe zones, each 2x the length of the ship
        // level 2: one safe zone, 1.5x length of the ship
        private void GenerateTerrain(int difficultyLevel)
        {
            TerrainGenerated = false;

            // keep height within 80% of the top
            const float maxPointHeight = (BoardSize * 0.8f);

            // first, generate random point at x=0 and x=maxBoardSize
            var random = new Random();

            // Refresh the TerrainList
            TerrainList = new List<(float x, float y)>
            {
                (0, (float) (random.NextDouble() * maxPointHeight)),
                (BoardSize, (float) (random.NextDouble() * maxPointHeight))
            };

            // next, generate safe zones
            SafeZones = new List<(float x_start, float x_stop)>();

            // safe zones depend on difficulty level
            var safeZoneLength = difficultyLevel switch
            {
                1 => Lander.Size * 3.5f,
                2 => Lander.Size * 2.5f,
                _ => throw new ArgumentOutOfRangeException(nameof(difficultyLevel), difficultyLevel, null)
            };

            // safe zones must be 15% from the border
            var safeStart = (int) (0.15f * BoardSize);
            var safeStop = (int) (BoardSize - 0.15f * BoardSize - safeZoneLength);

            var numSafeZonesToGen = difficultyLevel switch
            {
                1 => 2,
                2 => 1,
                _ => throw new ArgumentOutOfRangeException(nameof(difficultyLevel), difficultyLevel, null)
            };

            // generate all needed safe zones
            while (numSafeZonesToGen > 0)
            {
                // attempt to generate a new safezone within board bounds
                var thisStart = random.Next(safeStart, safeStop);
                var thisStop = thisStart + safeZoneLength;

                var isValid = true;

                // check validity
                foreach (var (xStart, xStop) in SafeZones)
                {
                    // not a valid safezone if either start or end lies within 0.1 of the
                    // boundaries of a safezone
                    // this will stop safezones from generating right next to each other
                    if (xStart - safeZoneLength * 0.1 < thisStart &&
                        xStop + safeZoneLength * 0.1 > thisStart ||
                        xStart - safeZoneLength * 0.1 < thisStop &&
                        xStop + safeZoneLength * 0.1 > thisStop)
                    {
                        isValid = false;
                        break;
                    }
                }

                // add this safe zone if it's valid
                if (isValid)
                {
                    // get a random height for the safezone
                    var height = (float) random.NextDouble() * maxPointHeight;

                    // minimum height is 5
                    if (height < 5)
                        height = 5;

                    // add both safezone points to the terrain list
                    TerrainList.Add((thisStart, height));
                    TerrainList.Add((thisStop, height));

                    // add this range to safezones list
                    SafeZones.Add((thisStart, thisStop));
                    numSafeZonesToGen--;
                }
            }

            // order safe zones and terrain by x coordinates
            TerrainList.Sort((first, second) => first.x.CompareTo(second.x));
            SafeZones.Sort((first, second) => first.x_start.CompareTo(second.x_start));

            // inner function used to recursively generate terrain levels
            void SubdivideSegment((float x, float y) startPoint, (float x, float y) endPoint)
            {
                var (startX, startY) = startPoint;
                var (endX, endY) = endPoint;

                // calculate differences between two points
                var diffX = endX - startX;
                var diffY = endY - startY;

                // return when x difference in segments is less than 1
                if (diffX < 1f)
                    return;

                // get midpoint of two segments
                var (midX, midY) = (startX + diffX / 2, startY + diffY / 2);

                // can adjust this to get rougher / smoother terrain
                // roughness of 0 is perfect straight lines
                const float roughness = 0.7f;

                // midpoint displacement algorithm
                // get Gaussian random number with mean 0 and std dev 1, multiply that by roughness,
                // then multiply that by difference in X to get the modification factor for the Y coordinate
                var gaussRandom = GaussianRandom(0, 1);
                var modY = roughness * gaussRandom * diffX;

                // modify the midpoint Y value by this mod factor
                midY += modY;

                // keep height within bounds
                if (midY < 0)
                    midY = 0;
                else if (midY > maxPointHeight)
                    midY = maxPointHeight;

                // add this new point to the list, then recurse on two new segments
                TerrainList.Add((midX, midY));
                SubdivideSegment((startX, startY), (midX, midY));
                SubdivideSegment((midX, midY), (endX, endY));
            }

            // track which segments we're subdividing
            var segmentsToSubdivide = new List<((float x, float y) startPoint, (float x, float y) endPoint)>();

            // line segments: from 0 to first safezone, between safezones, last safezone to end
            for (var i = 0; i < TerrainList.Count - 1; i++)
            {
                // don't want to add a line segment that begins at the beginning of a safezone
                // tolerance of 0.001 (segments should only be at minimum 1 unit apart)
                var inSafeZone = SafeZones.Any(sz => Math.Abs(TerrainList[i].x - sz.x_start) < 0.001);

                // if not in the safezone, then add this segment
                if (!inSafeZone)
                {
                    segmentsToSubdivide.Add((TerrainList[i], TerrainList[i + 1]));
                }
            }

            // now, subdivide each segment
            foreach (var (startPoint, endPoint) in segmentsToSubdivide)
            {
                SubdivideSegment(startPoint, endPoint);
            }

            // add points for (0, 0) and (max_x, 0)
            TerrainList.Insert(0, (0, 0));
            TerrainList.Add((BoardSize, 0));

            // need to re-order the terrain after subdivision
            // this will keep the terrain in the correct order for drawing
            TerrainList.Sort((first, second) => first.x.CompareTo(second.x));

            // flags that terrain is now generated
            TerrainGenerated = true;
            RecalculateTerrain = true;
        }

        public void Update(GameTime gameTime)
        {
            // first, handle input
            InputHandler.HandleInput();

            // 10,000 ticks in one millisecond => 10,000,000 ticks in one second
            var elapsedSeconds = gameTime.ElapsedGameTime.Ticks / 10_000_000f;

            // ReSharper disable once SwitchStatementHandlesSomeKnownEnumValuesWithDefault
            switch (GameState)
            {
                case Running when InputHandler.PauseButton.Pressed:
                    PauseMenuController.OpenMenu();
                    break;
                case Running:
                    // turning rate: 2pi/3 rads / sec
                    const float turningRate = 2 * MathHelper.Pi / 3;
                    var newOrientation = Lander.Orientation;

                    // turn left / right if the buttons are pressed
                    var turnLeft = InputHandler.TurnShipLeftButton.Pressed;
                    var turnRight = InputHandler.TurnShipRightButton.Pressed;

                    if (turnLeft && !turnRight)
                        newOrientation -= (turningRate * elapsedSeconds);
                    else if (turnRight && !turnLeft)
                        newOrientation += (turningRate * elapsedSeconds);

                    // we need to use kinematic formulas to calculate position using forces
                    // first, calculate forces
                    // F = ma
                    const float baseForceX = 0f;
                    const float baseForceY = Lander.Mass * (-1 * MoonGravity); // gravity force is negative

                    // thrust: 7 m/s^2
                    // F = ma ==> F = 4280 * 5 = 21400 N
                    const float thrustAcceleration = 7f;
                    const float thrustForce = Lander.Mass * thrustAcceleration;

                    // additional x / y forces from the thruster
                    var modForceX = 0f;
                    var modForceY = 0f;

                    ThrustOn = InputHandler.ThrustUpButton.Pressed && Lander.FuelCapacity > TimeSpan.Zero;
                    if (ThrustOn)
                    {
                        var cartesianOrientation = GetCartesianOrientation(newOrientation);

                        // Force equations: multiply total force by sin / cos theta to get x / y component
                        modForceX = thrustForce * (float) Math.Cos(cartesianOrientation);
                        modForceY = thrustForce * (float) Math.Sin(cartesianOrientation);

                        // decrease fuel capacity
                        Lander.FuelCapacity -= gameTime.ElapsedGameTime;
                        if (Lander.FuelCapacity < TimeSpan.Zero)
                            Lander.FuelCapacity = TimeSpan.Zero;
                    }

                    // add forces together to get final x/y forces
                    var finalForceX = baseForceX + modForceX;
                    var finalForceY = baseForceY + modForceY;

                    // next, calculate acceleration from the force
                    // F = ma ==> a = F/m
                    var accelerationX = finalForceX / Lander.Mass;
                    var accelerationY = finalForceY / Lander.Mass;

                    // next, calculate velocity
                    // vf = vo + at
                    var velocityX = Lander.Velocity.x + accelerationX * elapsedSeconds;
                    var velocityY = Lander.Velocity.y + accelerationY * elapsedSeconds;

                    // finally, calculate new position
                    // deltaPos = vt + (1/2)at^2
                    var deltaX = Lander.Velocity.x * elapsedSeconds
                                 + 0.5f * accelerationX * (float) Math.Pow(elapsedSeconds, 2);
                    var deltaY = Lander.Velocity.y * elapsedSeconds
                                 + 0.5f * accelerationY * (float) Math.Pow(elapsedSeconds, 2);

                    // translate the lander
                    (float x, float y) newPosition = (Lander.Position.x + deltaX, Lander.Position.y + deltaY);

                    // set new force, acceleration, velocity
                    Lander.Velocity = (velocityX, velocityY);
                    Lander.VelocityTotal = (float) Math.Sqrt(Math.Pow(Lander.Velocity.x, 2) +
                                                   Math.Pow(Lander.Velocity.y, 2));

                    Lander.Position = newPosition;

                    // update particles for ship
                    ParticleDrawController.ShipThrust(gameTime, Lander, ThrustOn);

                    // must have landed within the bounds of a safe area
                    InSafeArea = false;
                    foreach (var (xStart, xStop) in SafeZones)
                    {
                        // if leftmost position and rightmost position are within safezone bounds
                        if (Lander.Position.x - Lander.Size / 2 > xStart &&
                            Lander.Position.x + Lander.Size / 2 < xStop)
                        {
                            InSafeArea = true;
                            break;
                        }
                    }

                    // keep orientation between 0 and 2pi
                    if (newOrientation < 0)
                        newOrientation = MathHelper.TwoPi + newOrientation;
                    else if (newOrientation > MathHelper.TwoPi)
                        newOrientation = -1 * MathHelper.TwoPi + newOrientation;
                    Lander.Orientation = newOrientation;
                    Lander.OrientationDegrees = (Lander.Orientation * 180 / MathHelper.Pi) % 360;

                    // check for collision using three circles
                    // one big circle, two little circles at each bottom corner
                    var bigRadius = Lander.Size / 2;
                    // little radius at corner is -(2sqrt(2) - 3) / 2 = 0.08578645, give or take
                    // precise enough for this project
                    var littleRadius = Lander.Size * 0.08579f;
                    // big collision circle: centered about the object's center
                    var bigCenter = Lander.Position;
                    // little circle: tangent to big circle at 3pi/4, 5pi/4 in MG coordinates
                    // (-pi/4, -3pi/4 in Cartesian coordinates)
                    var sinOrientation1 =
                        (float) Math.Sin(GetCartesianOrientation(newOrientation + 3 * MathHelper.PiOver4));
                    var cosOrientation1 =
                        (float) Math.Cos(GetCartesianOrientation(newOrientation + 3 * MathHelper.PiOver4));
                    // to get center position of circle, translate the big center by cos/sin of big and little
                    var littleCenter1X = bigCenter.x
                                         + cosOrientation1 * bigRadius
                                         + cosOrientation1 * littleRadius;
                    var littleCenter1Y = bigCenter.y
                                         + sinOrientation1 * bigRadius
                                         + sinOrientation1 * littleRadius;
                    (float x, float y) littleCenter1 = (littleCenter1X, littleCenter1Y);

                    var sinOrientation2 =
                        (float) Math.Sin(GetCartesianOrientation(newOrientation + 5 * MathHelper.PiOver4));
                    var cosOrientation2 =
                        (float) Math.Cos(GetCartesianOrientation(newOrientation + 5 * MathHelper.PiOver4));
                    // to get center position of circle, translate the big center by cos/sin of big and little
                    var littleCenter2X = bigCenter.x
                                         + cosOrientation2 * bigRadius
                                         + cosOrientation2 * littleRadius;
                    var littleCenter2Y = bigCenter.y
                                         + sinOrientation2 * bigRadius
                                         + sinOrientation2 * littleRadius;

                    (float x, float y) littleCenter2 = (littleCenter2X, littleCenter2Y);

                    // testing collision with all three collision circles
                    if (CheckCollision(((bigCenter), bigRadius)) ||
                        CheckCollision(((littleCenter1), littleRadius)) ||
                        CheckCollision(((littleCenter2), littleRadius)))
                    {
                        // speed must be less than 2 units (meters) / second
                        // angle must be within 355 - 5 degrees
                        // must be in safe area
                        // check for all conditions
                        if ((Lander.OrientationDegrees < MaxAngle || Lander.OrientationDegrees > MinAngle)
                            && Lander.VelocityTotal < MaxSpeed
                            && InSafeArea)
                        {
                            // add remaining fuel to score
                            RunningScore += (float) Lander.FuelCapacity.TotalSeconds;

                            // flag success sound
                            PlaySuccessSound = true;

                            if (CurrentLevel == 1)
                            {
                                GameState = PassedLevel;
                                LoadingTime = TimeSpan.FromSeconds(3);
                            }
                            else
                            {
                                // calculate score
                                MainMenuController.HighScoresFloatList.Add(RunningScore);

                                // sort score from high to low
                                MainMenuController.HighScoresFloatList.Sort((a, b) => b.CompareTo(a));

                                // actually save the high scores on game completion
                                LocalStorageManager.SaveHighScores(MainMenuController.HighScoresFloatList);

                                GameState = BeatGame;
                            }
                        }
                        else
                        {
                            GameState = ShipCrashed;
                            PlayCrashSound = true;
                        }
                    }
                    break;
                case ShipCrashed when InputHandler.PauseButton.Pressed:
                    // only flag the crash sound for one frame
                    PlayCrashSound = false;
                    MainMenuController.OpenMenu();
                    break;
                case ShipCrashed:
                    // only flag the crash sound for one frame
                    PlayCrashSound = false;

                    // update particles for explosion
                    ParticleDrawController.ShipCrash(gameTime, Lander);

                    // update particles for ship
                    ParticleDrawController.ShipThrust(gameTime, Lander, false);

                    break;
                case PassedLevel:
                    PlaySuccessSound = false;
                    LoadingTime -= gameTime.ElapsedGameTime;

                    // update particles for ship
                    ParticleDrawController.ShipThrust(gameTime, Lander, false);

                    if (LoadingTime.TotalSeconds < 0)
                    {
                        StartLevel(2);
                        ParticleDrawController.ClearAllParticles();
                    }
                    break;
                case BeatGame when InputHandler.PauseButton.Pressed:
                    PlaySuccessSound = false;
                    MainMenuController.OpenMenu();
                    break;
                case BeatGame:
                    PlaySuccessSound = false;

                    // update particles for ship
                    ParticleDrawController.ShipThrust(gameTime, Lander, false);

                    break;
                case Paused:
                    PauseMenuController.ProcessMenu(InputHandler);
                    break;
                case MainMenu:
                    // clears all particles
                    ParticleDrawController.ClearAllParticles();
                    MainMenuController.ProcessMenu(InputHandler);
                    break;
            }
        }

        // Got this Gaussian random number generation from:
        // https://stackoverflow.com/a/218600
        private static float GaussianRandom(float mean, float stdDev)
        {
            var rand = new Random();
            var u1 = 1.0 - rand.NextDouble(); //uniform(0,1] random doubles
            var u2 = 1.0 - rand.NextDouble();
            var randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) *
                                   Math.Sin(2.0 * Math.PI * u2); //random normal(0,1)
            var randNormal =
                mean + stdDev * randStdNormal; //random normal(mean,stdDev^2)

            return (float) randNormal;
        }

        // used to convert the MG orientation system to a regular Cartesian orientation
        // because of the way MonoGame uses radians, we need to do some conversion here
        // Cartesian radians: 0, pi/2, pi, 3pi/2 (x, y, -x, -y directions)
        // MonoGame radians: pi/2, 0, 3pi/2, pi  (x, y, -x, -y directions)
        // convert MG radians to standard:
        // standard_radians = -(MonoGame radians) + pi/2
        public static float GetCartesianOrientation(float monoGameOrientation)
        {
            return -1 * monoGameOrientation + MathHelper.PiOver2;
        }

        private bool CheckCollision(((float x, float y) center, float radius) circle)
        {
            // iterate through each line segment, check for intersection with each of the spaceships three collisions
            for (var x = 0; x < TerrainList.Count - 1; x++)
            {
                var terrainPoint1 = TerrainList[x];
                var terrainPoint2 = TerrainList[x + 1];
                if (LineCircleIntersection(terrainPoint1, terrainPoint2, circle))
                {
                    return true;
                }
            }

            // if no collisions, return false
            return false;
        }

        // Reference: https://stackoverflow.com/questions/37224912/circle-line-segment-collision
        private static bool LineCircleIntersection((float x, float y) pt1,
            (float x, float y) pt2, ((float x, float y) center, float radius) circle) {
            (float x, float y) v1 = (pt2.x - pt1.x, pt2.y - pt1.y);
            (float x, float y) v2 = (pt1.x - circle.center.x, pt1.y - circle.center.y);

            var b = -2 * (v1.x * v2.x + v1.y * v2.y);
            var c =  2 * (v1.x * v1.x + v1.y * v1.y);

            float d;
            try
            {
                d = (float) Math.Sqrt(b * b - 2 * c * (v2.x * v2.x + v2.y * v2.y - circle.radius * circle.radius));
            }
            catch
            {
                // return false if no intercept
                return false;
            }

            // These represent the unit distance of point one and two on the line
            var u1 = (b - d) / c;
            var u2 = (b + d) / c;
            if (u1 <= 1 && u1 >= 0) {  // If point on the line segment
                return true;
            }
            if (u2 <= 1 && u2 >= 0) {  // If point on the line segment
                return true;
            }

            return false;
        }
    }
}