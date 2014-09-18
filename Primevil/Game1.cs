#region Using Statements
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Mime;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Storage;
using Microsoft.Xna.Framework.GamerServices;
using Primevil.Formats;

#endregion

namespace Primevil
{
    /// <summary>
    /// This is the main type for your game
    /// </summary>
    public class Game1 : Game
    {
        GraphicsDeviceManager graphics;
        SpriteBatch spriteBatch;

        Texture2D texture;

        private CELFile celFile;
        private int tileIndex;

        public Game1()
            : base()
        {
            graphics = new GraphicsDeviceManager(this);
            graphics.PreferredBackBufferWidth = 1024;
            graphics.PreferredBackBufferHeight = 1024;
            Content.RootDirectory = "Content";
        }

        /// <summary>
        /// Allows the game to perform any initialization it needs to before starting to run.
        /// This is where it can query for any required services and load any non-graphic
        /// related content.  Calling base.Initialize will enumerate through any components
        /// and initialize them as well.
        /// </summary>
        protected override void Initialize()
        {
            // TODO: Add your initialization logic here

            base.Initialize();
        }

        /// <summary>
        /// LoadContent will be called once per game and is the place to load
        /// all of your content.
        /// </summary>
        protected override void LoadContent()
        {
            // Create a new SpriteBatch, which can be used to draw textures.
            spriteBatch = new SpriteBatch(GraphicsDevice);

            // TODO: use this.Content to load your game content here

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
            }

            celFile = new CELFile(celData, palData);

            var atlas = new TextureAtlas(1024);

            for (int i = 0; i < celFile.NumFrames; ++i) {
                var frame = celFile.GetFrame(i);
                int id = atlas.Insert(frame.Data, frame.Width, frame.Height);
                if (id < 0) {
                    Debug.WriteLine("atlas is full: " + i);
                    break;
                }
            }

            texture = new Texture2D(GraphicsDevice, atlas.Dim, atlas.Dim, false, SurfaceFormat.Color);
            texture.SetData(atlas.Data);

            tileIndex = 102;
            NextFrame();
        }

        void NextFrame()
        {
            return;
            var frame = celFile.GetFrame(tileIndex++);
            Debug.WriteLine("Width: " + frame.Width);
            Debug.WriteLine("Height: " + frame.Height);

            texture = new Texture2D(GraphicsDevice, frame.Width, frame.Height, false, SurfaceFormat.Color);
            texture.SetData(frame.Data);
        }

        /// <summary>
        /// UnloadContent will be called once per game and is the place to unload
        /// all content.
        /// </summary>
        protected override void UnloadContent()
        {
            // TODO: Unload any non ContentManager content here
        }

        /// <summary>
        /// Allows the game to run logic such as updating the world,
        /// checking for collisions, gathering input, and playing audio.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Update(GameTime gameTime)
        {
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || Keyboard.GetState().IsKeyDown(Keys.Escape))
                Exit();

            // TODO: Add your update logic here


            if (Keyboard.GetState().IsKeyDown(Keys.Down)) {
                NextFrame();
            }

            base.Update(gameTime);
        }

        /// <summary>
        /// This is called when the game should draw itself.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.Black);

            // TODO: Add your drawing code here
            
            spriteBatch.Begin();
            spriteBatch.Draw(texture, new Rectangle(0, 0, 1024, 1024), Color.White);
            spriteBatch.End();

            base.Draw(gameTime);
        }
    }
}
