﻿using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace PhysicsSim
{
    public class Game1 : Game
    {
        // assets for this demo
        private Texture2D _texLander;
        private Rectangle _positionRectangle;
        private SpriteFont _spriteFont;

        // lander XY position
        // both represent lander center
        private (int x, int y) _landerPosition;
        private readonly (int x, int y) _startPosition = (500, 100);

        // moon gravity: https://en.wikipedia.org/wiki/Moon
        private const float MoonGravity = 1.62f;    // m/(s^2)

        // lander mass: https://en.wikipedia.org/wiki/Apollo_Lunar_Module
        private const int LanderMass = 4280;        // kg

        // position: radians (on Cartesian coordinate system)
        private float _orientation;

        // ship forces and velocities
        private (float x, float y) _acceleration;
        private (float x, float y) _velocity;
        private (float x, float y) _force;

        // sizes in units (1000)
        private const int BoardSize = 1000;
        private const int LanderSize = 100;

        // MonoGame stuff
        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch;

        public Game1()
        {
            _graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
        }

        protected override void Initialize()
        {
            // TODO: Add your initialization logic here
            //
            // _graphics.PreferredBackBufferWidth = 1400;
            // _graphics.PreferredBackBufferHeight = 700;
            // _graphics.ApplyChanges();

            _acceleration = (0, 0);
            _velocity = (0, 0);
            _force = (0, 0);
            _orientation = 90;
            _landerPosition = _startPosition;

            base.Initialize();
        }

        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);

            // TODO: use this.Content to load your game content here
            _texLander = this.Content.Load<Texture2D>("Images/Lander-2");
            _spriteFont = this.Content.Load<SpriteFont>("GameFont");
        }

        protected override void Update(GameTime gameTime)
        {
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || Keyboard.GetState().IsKeyDown(Keys.Escape))
                Exit();

            // TODO: Add your update logic here
            // CALCULATE NEW SHIP POSITION
            /*
             * steps:
             * 1) calculate all forces
             * 2)
             * 2) use kinematic formulas, as well as change in gameTime, to get change in position
             */

            // we need to use kinematic formulas to calculate position using forces
            (int x, int y) newLanderPosition;
            //TODO calculate
            newLanderPosition = _landerPosition;

            // set new lander position
            _landerPosition = newLanderPosition;


            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.CornflowerBlue);

            // TODO: Add your drawing code here
            _spriteBatch.Begin();

            /*
             * using the function SpriteBatch.Draw with following arguments:
             *
             * void SpriteBatch.Draw(Texture2D texture,
             *                       Rectangle destinationRectangle,
             *                       Rectangle? sourceRectangle,
             *                       Color color,
             *                       float rotation,
             *                       Vector2 origin,
             *                       SpriteEffects effects,
             *                       float layerDepth) (+ 6 overloads)
             */

            // for debugging purposes, draw the background rectangle
            var (backX, backY) = GetAbsolutePixelCoordinates((0, 0));
            var rectSizePixels = RescaleUnitsToPixels(1000);
            var backgroundRect = new Rectangle(backX, backY, rectSizePixels, rectSizePixels);
            var grayTexture = new Texture2D(_graphics.GraphicsDevice, 10, 10);
            var texData = new Color[10 * 10];
            for (var i = 0; i < texData.Length; i++)
                texData[i] = Color.Gray;
            grayTexture.SetData(texData);
            _spriteBatch.Draw(grayTexture, backgroundRect, Color.Blue);
            // delete up to here

            // Draw the lander

            // set lander position rectangle
            // sets the top left of lander (we are re-moving the texture in the Draw() function)
            var (landerX, landerY) = GetAbsolutePixelCoordinates((_landerPosition.x,
                _landerPosition.y));
            var landerSizePixels = RescaleUnitsToPixels(LanderSize);
            _positionRectangle = new Rectangle(landerX, landerY, landerSizePixels, landerSizePixels);

            // run draw function
            _spriteBatch.Draw(_texLander,
                              _positionRectangle,
                              null,
                              Color.Aqua,
                              MathHelper.Pi / 2,
                              new Vector2(_texLander.Width / 2, _texLander.Width / 2),
                              SpriteEffects.None,
                              0);

            _spriteBatch.End();

            base.Draw(gameTime);
        }

        // game board will have relative dimensions in a square
        // this function gets the absolute dimensions
        private (int x, int y) GetAbsolutePixelCoordinates((int x, int y) relativeCoordinates)
        {
            // keep relative coordinates good
            if (relativeCoordinates.x < 0 || relativeCoordinates.x > BoardSize ||
                relativeCoordinates.y < 0 || relativeCoordinates.y > BoardSize)
            {
                throw new Exception("Relative coordinates must be between 0 and " + BoardSize + ".");
            }

            // get absolute pixel dimensions
            var canvasBounds = GraphicsDevice.Viewport.Bounds;
            var canvasWidthPixels = canvasBounds.Width;
            var canvasHeightPixels = canvasBounds.Height;

            // get size of playable area
            var sizeOfGameAreaPixels = canvasHeightPixels;

            // height will be from bottom to top
            // width will be square centered in screen, same dimensions as height
            var horizontalMarginPixels = (canvasWidthPixels - sizeOfGameAreaPixels) / 2;

            // multiply the coordinate (units) by ratio of pixels to units to get pixels
            var (x, y) = relativeCoordinates;
            var rescaledX = (int) ((float) sizeOfGameAreaPixels / BoardSize * x + horizontalMarginPixels);
            var rescaledY = (int) ((float) sizeOfGameAreaPixels / BoardSize * y);

            return (rescaledX, rescaledY);
        }

        // given a unit count, rescale it to pixels
        private int RescaleUnitsToPixels(int units)
        {
            // get absolute pixel dimensions
            var canvasBounds = GraphicsDevice.Viewport.Bounds;
            var canvasWidthPixels = canvasBounds.Width;
            var canvasHeightPixels = canvasBounds.Height;

            // get size of playable area
            var sizeOfGameAreaPixels = canvasHeightPixels;

            // rescale
            var rescaledUnits = (int) ((float) sizeOfGameAreaPixels / BoardSize * units);
            return rescaledUnits;
        }
    }
}
