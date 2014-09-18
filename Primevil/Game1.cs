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
        GraphicsDeviceManager graphics;
        SpriteBatch spriteBatch;

        Texture2D texture;

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

            using (var f = mpq.Open("levels/towndata/town.pal")) {
                f.Read(palData, 0, 768);
            }

            using (var f = mpq.Open("levels/towndata/town.cel")) {
                celData = new byte[f.Length];
                var len = f.Read(celData, 0, celData.Length);
                Debug.Assert(len == celData.Length);
                Debug.WriteLine("celData.Length: " + celData.Length);
            }

            var celFile = new CELFile(celData, palData);

            var atlas = new TextureAtlas(2048);

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

            base.Update(gameTime);
        }


        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.Black);
            
            spriteBatch.Begin();
            spriteBatch.Draw(texture, new Vector2(0, 0), effect: SpriteEffects.FlipVertically);
            spriteBatch.End();

            base.Draw(gameTime);
        }
    }
}
