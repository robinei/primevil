using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Primevil.Formats;
using Primevil.Game;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using ButtonState = Microsoft.Xna.Framework.Input.ButtonState;
using Keys = Microsoft.Xna.Framework.Input.Keys;

namespace Primevil
{
    public static class Extensions
    {
        public static Rectangle ToXnaRect(this Rect r)
        {
            return new Rectangle(r.X, r.Y, r.Width, r.Height);
        }

        public static Vector2 ToVector2(this CoordF c)
        {
            return new Vector2(c.X, c.Y);
        }

        public static Vector2 ToVector2(this Coord c)
        {
            return new Vector2(c.X, c.Y);
        }

        public static Coord ToCoord(this Point p)
        {
            return new Coord(p.X, p.Y);
        }
    }


    public class XnaGame : Microsoft.Xna.Framework.Game
    {
        private readonly GraphicsDeviceManager graphics;
        private SpriteBatch spriteBatch;
        private int screenWidth;
        private int screenHeight;
        private MouseState lastMouseState;

        private IsoView isoView;
        private TextureAtlas charAtlas;
        private Animation playerAnim;
        private Creature player;
        private readonly List<Coord> pathList = new List<Coord>();
        private int pathIndex = 0;
        private PathFind pathFind;

        private bool musicPlaying;
        private SoundEffect music;

        public XnaGame()
        {
            graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
        }


        protected override void Initialize()
        {
            IsMouseVisible = true;
            var screen = Screen.FromHandle(Window.Handle) ?? Screen.PrimaryScreen;
            screenWidth = screen.Bounds.Width;
            screenHeight = screen.Bounds.Height;
#if WINDOWS
            Window.IsBorderless = true;
            Window.Position = new Point(screen.Bounds.X, screen.Bounds.Y);
#else
            graphics.IsFullScreen = true;
#endif
            graphics.PreferredBackBufferWidth = screenWidth;
            graphics.PreferredBackBufferHeight = screenHeight;
            graphics.ApplyChanges();
            
            isoView = new IsoView {
                ViewSize = new Size(screenWidth, screenHeight),
                DrawTile = (texture, pos, rect) =>
                    spriteBatch.Draw((Texture2D)texture, pos.ToVector2(), sourceRectangle: rect.ToXnaRect())
            };
            isoView.CenterOn(new Coord(75, 68));

            TextureAtlasPacker.TextureCreator = (data, width, height) => {
                var tex = new Texture2D(GraphicsDevice, width, height, false, SurfaceFormat.Color);
                tex.SetData(data);
                return tex;
            };

            base.Initialize();
        }


        protected override void LoadContent()
        {
            spriteBatch = new SpriteBatch(GraphicsDevice);

            var mpq = new MPQArchive("DIABDAT.MPQ");
            isoView.Level = TownLevel.Load(mpq);

            /*using (var wavStream = mpq.Open("music/dtowne.wav")) {
                var wavData = new byte[wavStream.Length];
                var wavLen = wavStream.Read(wavData, 0, (int)wavStream.Length);
                Debug.Assert(wavLen == wavStream.Length);
                music = new SoundEffect(wavData, 22050, AudioChannels.Stereo);
            }*/

            var palette = new byte[768];
            using (var f = new FileStream("Content/palette.pal", FileMode.Open, FileAccess.Read)) {
                var len = f.Read(palette, 0, 768);
                Debug.Assert(len == 768);
            }

            var packer = new TextureAtlasPacker(1024);
            var celFile = CELFile.Load(mpq, "plrgfx/warrior/wls/wlswl.cl2");
            for (int i = 0; i < celFile.NumFrames; ++i) {
                var frame = celFile.GetFrame(i, palette);
                int rectId = packer.Insert(frame.Data, frame.Width, frame.Height, true);
                if (rectId < 0)
                    throw new Exception("atlas is full: " + i);
            }

            charAtlas = packer.CreateAtlas();

            var playerSprite = new Sprite(new []{8,8,8,8,8,8,8,8},
                Enumerable.Range(0, 8*8).Select(index => new Sprite.Frame {
                    Texture = charAtlas.Texture,
                    SourceRect = charAtlas.Rects[index]
                }
            ).ToArray());

            playerAnim = new Animation(playerSprite, 1.0f / 10);

            player = new Creature {
                Position = new CoordF(75.5f, 68.5f),
                CurrentAnimation = playerAnim
            };
            isoView.Level.Creatures.PushBack(player);
            isoView.Level.Map.PlaceCreature(player);
        }


        protected override void UnloadContent()
        {
        }


        protected override void Update(GameTime gameTime)
        {
            var dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || Keyboard.GetState().IsKeyDown(Keys.Escape))
                Exit();

            var mouseState = Mouse.GetState();
            var mousePos = mouseState.Position.ToCoord();

            isoView.HoveredTile = isoView.ScreenToTile(mousePos);

            const float scrollSpeed = 600;
            if (Keyboard.GetState().IsKeyDown(Keys.Left) || Keyboard.GetState().IsKeyDown(Keys.A) || mousePos.X <= 1)
                isoView.ViewOffset.X -= scrollSpeed * dt;
            if (Keyboard.GetState().IsKeyDown(Keys.Right) || Keyboard.GetState().IsKeyDown(Keys.D) || mousePos.X >= screenWidth - 2)
                isoView.ViewOffset.X += scrollSpeed * dt;
            if (Keyboard.GetState().IsKeyDown(Keys.Up) || Keyboard.GetState().IsKeyDown(Keys.W) || mousePos.Y <= 1)
                isoView.ViewOffset.Y -= scrollSpeed * dt;
            if (Keyboard.GetState().IsKeyDown(Keys.Down) || Keyboard.GetState().IsKeyDown(Keys.S) || mousePos.Y >= screenHeight - 2)
                isoView.ViewOffset.Y += scrollSpeed * dt;


            if (lastMouseState.LeftButton == ButtonState.Released && mouseState.LeftButton == ButtonState.Pressed) {
                // just testing pathfinding
                if (pathFind == null)
                    pathFind = new PathFind(isoView.Level.Map.Width, isoView.Level.Map.Height);
                isoView.PathSet.Clear();
                pathIndex = 0;
                if (pathFind.Search(player.MapCoord, isoView.HoveredTile, pathList, isoView.Level.Map.GetCost)) {
                    foreach (var c in pathList)
                        isoView.PathSet.Add(c);
                }
            }

            if (!player.Walking && pathIndex < pathList.Count) {
                player.Walk(pathList[pathIndex++]);
            }
            player.Update(dt);
            isoView.Level.Map.PlaceCreature(player);


            if (music != null && !musicPlaying && gameTime.TotalGameTime.Seconds > 4) {
                music.Play();
                musicPlaying = true;
            }

            lastMouseState = mouseState;
            base.Update(gameTime);
        }


        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.Black);

            spriteBatch.Begin();
            isoView.DrawMap();
            //if (charAtlas != null)
            //    spriteBatch.Draw((Texture2D)charAtlas.Texture, new Vector2(0, 0));
            //spriteBatch.Draw((Texture2D)charAtlas.Texture, new Rectangle(0, 0, screenHeight, screenHeight), Color.White);
            spriteBatch.End();

            base.Draw(gameTime);
        }
    }
}
