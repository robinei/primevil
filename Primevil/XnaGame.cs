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

        private int screenWidth;
        private int screenHeight;

        private double mapX = 0;
        private double mapY = 0;
        private Level level;

        private int hoveredX, hoveredY;

        public XnaGame()
        {
            graphics = new GraphicsDeviceManager(this);

            Content.RootDirectory = "Content";

            TextureAtlasPacker.TextureCreator = (data, width, height) => {
                var tex = new Texture2D(GraphicsDevice, width, height, false, SurfaceFormat.Color);
                tex.SetData(data);
                return tex;
            };
        }


        protected override void Initialize()
        {
            IsMouseVisible = true;
            Screen screen = Screen.FromHandle(Window.Handle);
            screen = screen == null ? Screen.PrimaryScreen : screen;
            screenWidth = screen.Bounds.Width;
            screenHeight = screen.Bounds.Height;
            graphics.PreferredBackBufferWidth = screenWidth;
            graphics.PreferredBackBufferHeight = screenHeight;
            graphics.IsFullScreen = true;
            graphics.ApplyChanges();
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

            var mousePos = Mouse.GetState().Position;
            double speed = 600;

            if (Keyboard.GetState().IsKeyDown(Keys.Left) || Keyboard.GetState().IsKeyDown(Keys.A) || mousePos.X <= 1)
                mapX -= speed * dt;
            if (Keyboard.GetState().IsKeyDown(Keys.Right) || Keyboard.GetState().IsKeyDown(Keys.D) || mousePos.X >= screenWidth - 2)
                mapX += speed * dt;
            if (Keyboard.GetState().IsKeyDown(Keys.Up) || Keyboard.GetState().IsKeyDown(Keys.W) || mousePos.Y <= 1)
                mapY -= speed * dt;
            if (Keyboard.GetState().IsKeyDown(Keys.Down) || Keyboard.GetState().IsKeyDown(Keys.S) || mousePos.Y >= screenHeight - 2)
                mapY += speed * dt;

            ScreenToTile(mousePos.X, mousePos.Y, out hoveredX, out hoveredY);

            base.Update(gameTime);
        }


        private void ScreenToTile(int x, int y, out int tileX, out int tileY)
        {
            const int w = 32; // tileWidth / 2
            const int h = 16; // tileHeight / 2
            int xoff = -(int)mapX;
            int yoff = -(int)mapY;

            tileX = ( x*h + y*w - xoff*h - yoff*w) / (2*w*h);
            tileY = (-x*h + y*w + xoff*h - yoff*w) / (2*w*h);
        }

        private static void TileToWorld(int i, int j, out int screenX, out int screenY)
        {
            const int w = 32; // tileWidth / 2
            const int h = 16; // tileHeight / 2
            screenX = i * w - j * w;
            screenY = i * h + j * h;
        }

        private void DrawPillar(int pillarIndex, int xPos, int yPos)
        {
            int maxY = level.PillarDefs.PillarHeight;
            yPos -= (maxY - 1) * 32; // yPos initially points to position of lowest tile
            for (int x = 0; x < 2; ++x) {
                for (int y = 0; y < maxY; ++y) {
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
            var map = level.Map;

            int i0, j0;
            ScreenToTile(0, 0, out i0, out j0);
            i0 -= 2;

            int x, y;
            TileToWorld(i0, j0, out x, out y);
            x -= (int)mapX + 32;
            y -= (int)mapY;

            int row = 0;
            int x0 = x;
            int i = i0, j = j0;
            while (y < screenHeight + 300) {
                for (; x < screenWidth; x += tileWidth, ++i, --j) {
                    if (i == hoveredX && j == hoveredY)
                        continue;
                    if (i < 0 || j < 0 || i >= map.Width || j >= map.Height)
                        continue;
                    int minIndex = map.GetPillar(i, j);
                    if (minIndex < 0)
                        continue;

                    DrawPillar(minIndex, x, y);
                }

                x = x0;
                y += tileHeight / 2;
                if (++row % 2 != 0) {
                    x -= tileWidth / 2;
                    ++j0;
                } else {
                    ++i0;
                }
                i = i0;
                j = j0;
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
