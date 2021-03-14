﻿using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace LunarLander
{
    public class LanderGame : Game
    {
        // game controller handles all underlying logic
        private LanderGameController _landerGameController;

        // assets for this game
        private Texture2D _texLander;
        private Rectangle _positionRectangle;
        private SpriteFont _spriteFont;
        private BasicEffect _basicEffect;

        // MonoGame stuff
        private readonly GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch;

        public LanderGame()
        {
            _graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
        }

        protected override void Initialize()
        {
            _graphics.PreferredBackBufferWidth = 1280;
            _graphics.PreferredBackBufferHeight = 720;
            _graphics.ApplyChanges();

            _landerGameController = new LanderGameController();

            _graphics.GraphicsDevice.RasterizerState = new RasterizerState
            {
                FillMode = FillMode.Solid,
                CullMode = CullMode.None,   // CullMode.None If you want to not worry about triangle winding order
                MultiSampleAntiAlias = true,
            };

            _basicEffect = new BasicEffect(_graphics.GraphicsDevice)
            {
                VertexColorEnabled = true,
                View = Matrix.CreateLookAt(new Vector3(0.0f, 0.0f, 1.0f), Vector3.Zero, Vector3.Up),
                Projection = Matrix.CreatePerspectiveOffCenter(0.0f, _graphics.GraphicsDevice.Viewport.Width,
                    _graphics.GraphicsDevice.Viewport.Height, 0, 1.0f, 1000.0f)
            };

            base.Initialize();
        }

        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);

            _texLander = this.Content.Load<Texture2D>("Lander-2");
            _spriteFont = this.Content.Load<SpriteFont>("GameFont");
        }

        protected override void Update(GameTime gameTime)
        {
            // all updating is handled in the game controller
            _landerGameController.Update(gameTime);

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.CornflowerBlue);

            _spriteBatch.Begin();

            // for debugging purposes, draw the background square in center of screen
            // get pixel coordinates from board coordinates
            var (backX, backY) = GetAbsolutePixelCoordinates((0, LanderGameController.BoardSize));
            var rectSizePixels = RescaleUnitsToPixels(LanderGameController.BoardSize);
            // create the MG Rectangle
            var backgroundRect = new Rectangle(backX, backY, rectSizePixels, rectSizePixels);
            // make a generic Gray texture
            var grayTexture = new Texture2D(_graphics.GraphicsDevice, 10, 10);
            var texData = new Color[10 * 10];
            for (var i = 0; i < texData.Length; i++)
                texData[i] = Color.Gray;
            grayTexture.SetData(texData);
            _spriteBatch.Draw(grayTexture, backgroundRect, Color.Gray);

            // end spritebatch here so we can draw terrain over background
            _spriteBatch.End();

            // next, draw the terrain (if generated)
            if (_landerGameController.TerrainGenerated)
            {

                // create a list of all vertices in the terrain
                var terrainVertexList = new List<VertexPositionColor>();
                foreach (var (x, y) in _landerGameController.TerrainList)
                {
                    var (scaledX, scaledY) = GetAbsolutePixelCoordinates((x, y));
                    var (_, scaled0) = GetAbsolutePixelCoordinates((x, 0));
                    terrainVertexList.Add(new VertexPositionColor
                    {
                        Position = new Vector3(scaledX, scaled0, 0),
                        Color = Color.Black
                    });
                    terrainVertexList.Add(new VertexPositionColor
                    {
                        Position = new Vector3(scaledX, scaledY, 0),
                        Color = Color.Black
                    });
                }

                // convert list to an array
                var terrainVertexArray = terrainVertexList.ToArray();

                // create an array of ints in ascending order
                var indexArray = new int[terrainVertexArray.Length];
                for (var i = 0; i < indexArray.Length; i++)
                    indexArray[i] = i;

                foreach (EffectPass pass in _basicEffect.CurrentTechnique.Passes)
                {
                    pass.Apply();

                    _graphics.GraphicsDevice.DrawUserIndexedPrimitives(
                        PrimitiveType.TriangleStrip,
                        terrainVertexArray, 0, terrainVertexArray.Length,
                        indexArray, 0, indexArray.Length - 2
                    );
                }
            }

            // Now, draw the lander
            _spriteBatch.Begin();

            // set lander position rectangle
            // sets the top left of lander (we are re-adjusting the texture origin in the Draw() function)
            var lander = _landerGameController.Lander;
            var (landerX, landerY) = GetAbsolutePixelCoordinates((lander.Position.x,
                lander.Position.y));
            var landerSizePixels = RescaleUnitsToPixels(Lander.Size);
            _positionRectangle = new Rectangle(landerX, landerY, landerSizePixels, landerSizePixels);

            // run draw function
            _spriteBatch.Draw(_texLander,
                _positionRectangle,
                null,
                Color.Aqua,
                lander.Orientation,
                // center origin in the texture
                new Vector2(_texLander.Width / 2, _texLander.Width / 2),
                SpriteEffects.None,
                0);

            _spriteBatch.End();

            base.Draw(gameTime);
        }

        // game board will have relative dimensions in a square
        // this function gets the absolute dimensions
        private (int x, int y) GetAbsolutePixelCoordinates((float x, float y) relativeCoordinates)
        {
            // keep relative coordinates good
            if (relativeCoordinates.x < 0 || relativeCoordinates.x > LanderGameController.BoardSize ||
                relativeCoordinates.y < 0 || relativeCoordinates.y > LanderGameController.BoardSize)
            {
                // uncomment this line if we want to force spaceship to stay in safe area
                // throw new Exception("Relative coordinates must be between 0 and " + BoardSize + ".");
            }

            // get absolute pixel dimensions
            var canvasBounds = GraphicsDevice.Viewport.Bounds;
            var canvasWidthPixels = canvasBounds.Width;
            var canvasHeightPixels = canvasBounds.Height;

            // get size of playable area (will be constrained by height)
            var sizeOfGameAreaPixels = canvasHeightPixels;

            // width will be square centered in screen, same dimensions as height
            var horizontalMarginPixels = (canvasWidthPixels - sizeOfGameAreaPixels) / 2;

            // properly rescale the coordinates to get the correct pixels
            var rescaledX = RescaleUnitsToPixels(relativeCoordinates.x) + horizontalMarginPixels;
            var rescaledY = RescaleUnitsToPixels(LanderGameController.BoardSize - relativeCoordinates.y);

            // return rescaled coordinates
            return (rescaledX, rescaledY);
        }

        // given a unit count, rescale it to pixels
        private int RescaleUnitsToPixels(float units)
        {
            // get absolute pixel dimensions
            var sizeOfGameAreaPixels = GraphicsDevice.Viewport.Bounds.Height;

            // rescale by ratio of game area in pixels to board size
            var rescaledUnits = (int) (sizeOfGameAreaPixels / LanderGameController.BoardSize * units);
            return rescaledUnits;
        }
    }
}
