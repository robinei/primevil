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



    public struct WalkState
    {
        public Coord Start;
        public CoordF Position;
        public Direction Direction;
        public bool Walking;

        public void Walk(Direction dir)
        {
            Start = Position.ToCoord();
            Direction = dir;
            Walking = true;
        }

        public Coord Target { get { return Start + Direction.DeltaCoord(); } }

        public void Update(float dt)
        {
            if (!Walking)
                return;

            var target = Target;
            var targetF = target.ToCoordF() + new CoordF(0.5f, 0.5f);

            var d = targetF - Position;
            var len = d.Length;

            if (len < 0.01) {
                Position = targetF;
                Walking = false;
                return;
            }

            const float speed = 2;
            float moveAmount = speed * dt;
            if (moveAmount > len)
                moveAmount = len;
            d.Normalize();
            Position = Position + (d * moveAmount);
        }
    }



    public class XnaGame : Microsoft.Xna.Framework.Game
    {
        private readonly GraphicsDeviceManager graphics;
        private SpriteBatch spriteBatch;
        private int screenWidth;
        private int screenHeight;

        private IsoView isoView;
        private TextureAtlas charAtlas;
        private Animation playerAnim;

        private bool musicPlaying;
        private SoundEffect music;

        private WalkState walkState = new WalkState {Position = new CoordF(0.5f, 0.5f)};

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

            using (var wavStream = mpq.Open("music/dtowne.wav")) {
                var wavData = new byte[wavStream.Length];
                var wavLen = wavStream.Read(wavData, 0, (int)wavStream.Length);
                Debug.Assert(wavLen == wavStream.Length);
                music = new SoundEffect(wavData, 22050, AudioChannels.Stereo);
            }

            var palette = new byte[768];
            using (var f = new FileStream("Content/palette.pal", FileMode.Open, FileAccess.Read)) {
                var len = f.Read(palette, 0, 768);
                Debug.Assert(len == 768);
            }

            var packer = new TextureAtlasPacker(1024);
            var celFile = CELFile.Load(mpq, "plrgfx/rogue/rld/rldas.cl2");
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
        }


        protected override void UnloadContent()
        {
        }


        protected override void Update(GameTime gameTime)
        {
            var dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || Keyboard.GetState().IsKeyDown(Keys.Escape))
                Exit();

            var mousePos = Mouse.GetState().Position.ToCoord();
            isoView.HoveredTile = isoView.ScreenToTile(mousePos);

            //var pos = isoView.ScreenToTile(mousePos.ToCoordF());
            //Debug.WriteLine("pos({0}, {1})", pos.X, pos.Y);

            const float scrollSpeed = 600;
            if (Keyboard.GetState().IsKeyDown(Keys.Left) || mousePos.X <= 1)
                isoView.ViewOffset.X -= scrollSpeed * dt;
            if (Keyboard.GetState().IsKeyDown(Keys.Right) || mousePos.X >= screenWidth - 2)
                isoView.ViewOffset.X += scrollSpeed * dt;
            if (Keyboard.GetState().IsKeyDown(Keys.Up) || mousePos.Y <= 1)
                isoView.ViewOffset.Y -= scrollSpeed * dt;
            if (Keyboard.GetState().IsKeyDown(Keys.Down) || mousePos.Y >= screenHeight - 2)
                isoView.ViewOffset.Y += scrollSpeed * dt;

            if (Keyboard.GetState().IsKeyDown(Keys.A) && Keyboard.GetState().IsKeyDown(Keys.W))
                walkState.Walk(Direction.West);
            else if (Keyboard.GetState().IsKeyDown(Keys.W) && Keyboard.GetState().IsKeyDown(Keys.D))
                walkState.Walk(Direction.North);
            else if (Keyboard.GetState().IsKeyDown(Keys.S) && Keyboard.GetState().IsKeyDown(Keys.D))
                walkState.Walk(Direction.East);
            else if (Keyboard.GetState().IsKeyDown(Keys.S) && Keyboard.GetState().IsKeyDown(Keys.A))
                walkState.Walk(Direction.South);
            else if (Keyboard.GetState().IsKeyDown(Keys.A))
                walkState.Walk(Direction.SouthWest);
            else if (Keyboard.GetState().IsKeyDown(Keys.S))
                walkState.Walk(Direction.SouthEast);
            else if (Keyboard.GetState().IsKeyDown(Keys.D))
                walkState.Walk(Direction.NorthEast);
            else if (Keyboard.GetState().IsKeyDown(Keys.W))
                walkState.Walk(Direction.NorthWest);

            if (!isoView.Level.Map.IsPassable(walkState.Target))
                walkState.Walking = false;

            playerAnim.Direction = walkState.Direction;
            walkState.Update(dt);
            playerAnim.Update(dt);

            if (music != null && !musicPlaying && gameTime.TotalGameTime.Seconds > 4) {
                music.Play();
                musicPlaying = true;
            }

            base.Update(gameTime);
        }


        private void DrawPlayer()
        {
            var p = isoView.TileToScreen(walkState.Position);
            var frame = playerAnim.CurrentFrame;


            spriteBatch.Draw(
                (Texture2D)frame.Texture,
                new Vector2(p.X - frame.Width*0.5f + 32, p.Y - frame.Height + 15),
                sourceRectangle: frame.SourceRect.ToXnaRect()
            );
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.Black);

            spriteBatch.Begin();
            isoView.DrawMap();
            DrawPlayer();
            //if (charAtlas != null)
            //    spriteBatch.Draw((Texture2D)charAtlas.Texture, new Vector2(0, 0));
            //spriteBatch.Draw((Texture2D)charAtlas.Texture, new Rectangle(0, 0, screenHeight, screenHeight), Color.White);
            spriteBatch.End();

            base.Draw(gameTime);
        }
    }
}
