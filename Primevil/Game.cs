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
        private Map map;

        private readonly int screenWidth;
        private readonly int screenHeight;

        private double mapX = 0;
        private double mapY = 0;

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
            var palette = new byte[768];

            using (var f = mpq.Open("levels/towndata/town.pal")) {
                var len = f.Read(palette, 0, 768);
                Debug.Assert(len == palette.Length);
            }

            var celFile = CELFile.Load(mpq, "levels/towndata/town.cel");
            minFile = MINFile.Load(mpq, "levels/towndata/town.min");

            var tilFile = TILFile.Load(mpq, "levels/towndata/town.til");
            var dunNames = new string[] {
                "levels/towndata/sector1s.dun",
                "levels/towndata/sector2s.dun",
                "levels/towndata/sector3s.dun",
                "levels/towndata/sector4s.dun"
            };
            var sectors = new SectorTemplate[4];
            for (int i = 0; i < dunNames.Length; ++i) {
                var dunFile = DUNFile.Load(mpq, dunNames[i]);
                sectors[i] = new SectorTemplate(dunFile, tilFile);
            }
            int mapWidth = sectors[0].Width + sectors[3].Width;
            int mapHeight = sectors[0].Height + sectors[3].Height;
            map = new Map(mapWidth, mapHeight);
            map.PlaceSector(sectors[3], 0, 0);
            map.PlaceSector(sectors[2], 0, sectors[3].Height);
            map.PlaceSector(sectors[1], sectors[3].Width, 0);
            map.PlaceSector(sectors[0], sectors[3].Width, sectors[3].Height);

            atlas = new TextureAtlas(2048);

            for (int i = 0; i < celFile.NumFrames; ++i) {
                CELFile.Frame frame;
                try {
                    frame = celFile.GetFrame(i, palette);
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
                for (int y = 0; y < minFile.PillarHeight; ++y) {
                    int celIndex = minFile.GetCelIndex(pillarIndex, x, y);
                    if (celIndex < 0)
                        continue;

                    spriteBatch.Draw(texture, new Vector2(xPos + x * 32, yPos + y * 32),
                        sourceRectangle: atlas.GetRectangle(celIndex));//, effect: SpriteEffects.FlipHorizontally);
                }
            }
        }

        private void DrawMap()
        {
            const int tileWidth = 64;
            const int tileHeight = 32;

            for (int j = 0; j < map.Height; ++j) {
                for (int i = 0; i < map.Width; ++i) {
                    int minIndex = map.GetPillar(i, j);
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
            //DrawTileBlock(tileIndex, 200, 200);
            //spriteBatch.Draw(texture, new Rectangle(0, 0, screenHeight, screenHeight), Color.White);
            spriteBatch.End();

            base.Draw(gameTime);
        }
    }
}
