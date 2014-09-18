using System.Security.Policy;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Primevil.Formats;
using System;
using System.Diagnostics;

namespace Primevil
{
    public class Game1 : Game
    {
        private GraphicsDeviceManager graphics;
        private SpriteBatch spriteBatch;

        private Texture2D texture;
        private TextureAtlas atlas;
        private MINFile minFile;
        private TILFile tilFile;
        private DUNFile dunFile;
        private int tileIndex;

        public Game1()
        {
            graphics = new GraphicsDeviceManager(this);
            graphics.PreferredBackBufferWidth = 2560;
            graphics.PreferredBackBufferHeight = 1440;
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
            minFile = new MINFile(minData, "town.min");
            tilFile = new TILFile(tilData);
            dunFile = new DUNFile(dunData);
            Debug.WriteLine("PillarHeight: " + minFile.PillarHeight);
            Debug.WriteLine("NumPillars: " + minFile.NumPillars);
            Debug.WriteLine("NumBlocks: " + tilFile.NumBlocks);

            atlas = new TextureAtlas(2048);

            for (int i = 0; i < celFile.NumFrames; ++i) {
                CELFile.Frame frame;
                try {
                    frame = celFile.GetFrame(i);
                    if (frame == null)
                        continue;
                } catch (Exception e) {
                    Debug.WriteLine("error at: " + i);
                    break;
                }
                int id = atlas.Insert(frame.Data, frame.Width, frame.Height);
                if (id < 0) {
                    Debug.WriteLine("atlas is full: " + i);
                    break;
                }
            }

            atlas.Freeze();
            texture = new Texture2D(GraphicsDevice, atlas.Dim, atlas.Dim, false, SurfaceFormat.Color);
            texture.SetData(atlas.Data);
        }


        protected override void UnloadContent()
        {
        }


        protected override void Update(GameTime gameTime)
        {
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || Keyboard.GetState().IsKeyDown(Keys.Escape))
                Exit();

            if (Keyboard.GetState().IsKeyDown(Keys.Left))
                tileIndex = (tileIndex - 1) % tilFile.NumBlocks;
            if (Keyboard.GetState().IsKeyDown(Keys.Right))
                tileIndex = (tileIndex + 1) % tilFile.NumBlocks;

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
                        sourceRectangle: atlas.GetRectangle(celIndex),
                        effect: SpriteEffects.FlipVertically);
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


        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.Black);
            
            spriteBatch.Begin();
            DrawTileBlock(tileIndex, 500, 500);
            spriteBatch.End();

            base.Draw(gameTime);
        }
    }
}
