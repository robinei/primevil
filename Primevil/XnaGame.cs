using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Primevil.Formats;
using Primevil.Game;
using System;
using System.Diagnostics;
using System.Linq;
using ButtonState = Microsoft.Xna.Framework.Input.ButtonState;
using Keys = Microsoft.Xna.Framework.Input.Keys;
using System.Windows.Forms;

namespace Primevil
{
    public static class Extensions
    {
        public static Rectangle ToXnaRect(this Rect r)
        {
            return new Rectangle(r.X, r.Y, r.Width, r.Height);
        }
    }


    public class XnaGame : Microsoft.Xna.Framework.Game
    {
        private GraphicsDeviceManager graphics;
        private SpriteBatch spriteBatch;

        private readonly int screenWidth;
        private readonly int screenHeight;

        private double mapX = 0;
        private double mapY = 0;
        private Level level;

        public XnaGame()
        {
            graphics = new GraphicsDeviceManager(this);

            Screen screen = Screen.FromHandle(Window.Handle);
            screen = screen == null ? Screen.PrimaryScreen : screen;
            screenWidth = screen.Bounds.Width;
            screenHeight = screen.Bounds.Height;
            //Window.IsBorderless = true;
            graphics.PreferredBackBufferWidth = screenWidth;
            graphics.PreferredBackBufferHeight = screenHeight;

            Content.RootDirectory = "Content";

            TextureAtlasPacker.TextureCreator = (data, width, height) => {
                var tex = new Texture2D(GraphicsDevice, width, height, false, SurfaceFormat.Color);
                tex.SetData(data);
                return tex;
            };
        }


        protected override void Initialize()
        {
            base.Initialize();
        }


        protected override void LoadContent()
        {
            spriteBatch = new SpriteBatch(GraphicsDevice);

            var mpq = new MPQArchive("DIABDAT.MPQ");
            level = TownLevel.LoadTownLevel(mpq);
        }


        protected override void UnloadContent()
        {
        }


        protected override void Update(GameTime gameTime)
        {
            var dt = gameTime.ElapsedGameTime.TotalSeconds;

            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || Keyboard.GetState().IsKeyDown(Keys.Escape))
                Exit();

            double speed = 600;
            if (Keyboard.GetState().IsKeyDown(Keys.Left))
                mapX += speed * dt;
            if (Keyboard.GetState().IsKeyDown(Keys.Right))
                mapX -= speed * dt;
            if (Keyboard.GetState().IsKeyDown(Keys.Up))
                mapY += speed * dt;
            if (Keyboard.GetState().IsKeyDown(Keys.Down))
                mapY -= speed * dt;

            base.Update(gameTime);
        }


        private void DrawMinPillar(int pillarIndex, int xPos, int yPos)
        {
            for (int x = 0; x < 2; ++x) {
                for (int y = 0; y < level.PillarDefs.PillarHeight; ++y) {
                    int celIndex = level.PillarDefs.GetCelIndex(pillarIndex, x, y);
                    if (celIndex < 0)
                        continue;

                    spriteBatch.Draw(
                        (Texture2D)level.Tileset.Texture,
                        new Vector2(xPos + x * 32, yPos + y * 32),
                        sourceRectangle: level.Tileset.Rects[celIndex].ToXnaRect()
                    );
                }
            }
        }

        private void DrawMap()
        {
            const int tileWidth = 64;
            const int tileHeight = 32;

            for (int j = 0; j < level.Map.Height; ++j) {
                for (int i = 0; i < level.Map.Width; ++i) {
                    int minIndex = level.Map.GetPillar(i, j);
                    if (minIndex < 0)
                        continue;

                    int x = (i * tileWidth / 2) - (j * tileWidth / 2);
                    int y = (i * tileHeight / 2) + (j * tileHeight / 2);
                    DrawMinPillar(minIndex, (int)mapX + x + screenWidth / 2, (int)mapY + y);
                }
            }
        }


        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.Black);

            spriteBatch.Begin();
            DrawMap();
            //spriteBatch.Draw(texture, new Rectangle(0, 0, screenHeight, screenHeight), Color.White);
            spriteBatch.End();

            base.Draw(gameTime);
        }
    }
}
