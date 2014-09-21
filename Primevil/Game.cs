using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Primevil.Formats;
using System;
using System.Diagnostics;
using System.Linq;
using ButtonState = Microsoft.Xna.Framework.Input.ButtonState;
using Keys = Microsoft.Xna.Framework.Input.Keys;
using System.Windows.Forms;

namespace Primevil
{
    public class Game : Microsoft.Xna.Framework.Game
    {
        private GraphicsDeviceManager graphics;
        private SpriteBatch spriteBatch;

        private Texture2D texture;
        private TextureAtlas atlas;
        private MINFile minFile;
        private TILFile tilFile;
        private DUNFile dunFile;
        private int tileIndex;

        private readonly int screenWidth;
        private readonly int screenHeight;

        public Game()
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
        }


        protected override void Initialize()
        {
            base.Initialize();
        }


        protected override void LoadContent()
        {
            spriteBatch = new SpriteBatch(GraphicsDevice);

            var mpq = new MPQArchive("DIABDAT.MPQ");
            var palData = new byte[768];
            byte[] celData;
            byte[] minData;
            byte[] tilData;
            byte[] dunData;

            using (var f = mpq.Open("levels/towndata/town.pal")) {
                var len = f.Read(palData, 0, 768);
                Debug.Assert(len == palData.Length);
            }

            using (var f = mpq.Open("levels/towndata/town.cel")) {
                celData = new byte[f.Length];
                var len = f.Read(celData, 0, celData.Length);
                Debug.Assert(len == celData.Length);
                Debug.WriteLine("celData.Length: " + celData.Length);
            }

            using (var f = mpq.Open("levels/towndata/town.min")) {
                minData = new byte[f.Length];
                var len = f.Read(minData, 0, minData.Length);
                Debug.Assert(len == minData.Length);
            }

            using (var f = mpq.Open("levels/towndata/town.til")) {
                tilData = new byte[f.Length];
                var len = f.Read(tilData, 0, tilData.Length);
                Debug.Assert(len == tilData.Length);
            }

            using (var f = mpq.Open("levels/towndata/sector1s.dun")) {
                dunData = new byte[f.Length];
                var len = f.Read(dunData, 0, dunData.Length);
                Debug.Assert(len == dunData.Length);
            }

            var celFile = new CELFile(celData, palData);
            minFile = new MINFile("town.min", minData);
            tilFile = new TILFile(tilData);
            dunFile = new DUNFile(dunData);
            Debug.WriteLine("PillarHeight: " + minFile.PillarHeight);
            Debug.WriteLine("NumPillars: " + minFile.NumPillars);
            Debug.WriteLine("NumBlocks: " + tilFile.NumBlocks);
            Debug.WriteLine("Width: " + dunFile.Width);
            Debug.WriteLine("Height: " + dunFile.Height);

            atlas = new TextureAtlas(2048);


            /*var temp = new byte[32 * 32 * 4];
            for (int i = 0; i < 32; ++i) {
                temp[i * 32 * 4 + i * 4 + 0] = 255;
                temp[i * 32 * 4 + i * 4 + 1] = 255;
                temp[i * 32 * 4 + i * 4 + 2] = 255;
                temp[i * 32 * 4 + i * 4 + 3] = 255;

                temp[10 * 32 * 4 + i * 4 + 0] = 128;
                temp[10 * 32 * 4 + i * 4 + 1] = 128;
                temp[10 * 32 * 4 + i * 4 + 2] = 128;
                temp[10 * 32 * 4 + i * 4 + 3] = 255;
            }
            atlas.Insert(temp, 32, 32);*/

            for (int i = 0; i < celFile.NumFrames; ++i) {
                CELFile.Frame frame;
                try {
                    frame = celFile.GetFrame(i);
                    if (frame == null)
                        continue;
                } catch (Exception) {
                    Debug.WriteLine("error at: " + i);
                    break;
                }
                int id = atlas.Insert(frame.Data, frame.Width, frame.Height, true);
                if (id < 0) {
                    Debug.WriteLine("atlas is full: " + i);
                    break;
                }
            }

            texture = new Texture2D(GraphicsDevice, atlas.Dim, atlas.Dim, false, SurfaceFormat.Color);
            texture.SetData(atlas.Data);
            tileIndex = 70;
        }


        protected override void UnloadContent()
        {
        }


        protected override void Update(GameTime gameTime)
        {
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || Keyboard.GetState().IsKeyDown(Keys.Escape))
                Exit();

            if (Keyboard.GetState().IsKeyDown(Keys.Left))
                tileIndex = tileIndex - 1;
            if (Keyboard.GetState().IsKeyDown(Keys.Right))
                tileIndex = tileIndex + 1;
            if (tileIndex < 0)
                tileIndex += tilFile.NumBlocks;
            else if (tileIndex >= tilFile.NumBlocks)
                tileIndex -= tilFile.NumBlocks;

            base.Update(gameTime);
        }


        private void DrawMinPillar(int pillarIndex, int xPos, int yPos)
        {
            for (int x = 0; x < 2; ++x) {
                for (int y = 0; y < minFile.PillarHeight; ++y) {
                    int celIndex = minFile.GetCelIndex(pillarIndex, x, y);
                    if (celIndex < 0)
                        continue;

                    spriteBatch.Draw(texture, new Vector2(xPos + x * 32, yPos + y * 32),
                        sourceRectangle: atlas.GetRectangle(celIndex));//, effect: SpriteEffects.FlipHorizontally);
                }
            }
        }

        private void DrawTileBlock(int blockIndex, int xPos, int yPos)
        {
            var b = tilFile.GetBlock(blockIndex);
            DrawMinPillar(b.Top, xPos + 32, yPos);
            DrawMinPillar(b.Left, xPos, yPos + 16);
            DrawMinPillar(b.Right, xPos + 64, yPos + 16);
            DrawMinPillar(b.Bottom, xPos + 32, yPos + 32);
        }

        private void DrawDunFile()
        {
            const int tileWidth = 128;
            const int tileHeight = 64;

            for (int j = 0; j < dunFile.Height; ++j) {
                for (int i = 0; i < dunFile.Width; ++i) {
                    int x = (i * tileWidth / 2) - (j * tileWidth / 2);
                    int y = (i * tileHeight / 2) + (j * tileHeight / 2);

                    int blockIndex = dunFile.GetTileIndex(i, j) - 1;
                    if (blockIndex < 0)
                        continue;

                    DrawTileBlock(blockIndex, x + screenWidth / 2, y);
                }
            }
        }


        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.Black);

            spriteBatch.Begin();
            DrawDunFile();
            //DrawTileBlock(tileIndex, 200, 200);
            //spriteBatch.Draw(texture, new Rectangle(0, 0, screenHeight, screenHeight), Color.White);
            spriteBatch.End();

            base.Draw(gameTime);
        }
    }
}
