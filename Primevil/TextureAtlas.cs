using System;
using Microsoft.Xna.Framework;
using System.Collections.Generic;

namespace Primevil
{
    class TextureAtlas
    {
        private class Node
        {
            public Rectangle Rect;

            private Node child0, child1;
            private bool populated;

            public Node Insert(int width, int height)
            {
                if (child0 != null) {
                    var node = child0.Insert(width, height);
                    return node ?? child1.Insert(width, height);
                }

                if (populated)
                    return null;

                if (width > Rect.Width || height > Rect.Height)
                    return null;

                if (width == Rect.Width && height == Rect.Height) {
                    populated = true;
                    return this;
                }

                child0 = new Node();
                child1 = new Node();

                int dw = Rect.Width - width;
                int dh = Rect.Height - height;

                if (dw > dh) {
                    child0.Rect = new Rectangle(Rect.X, Rect.Y, width, Rect.Height);
                    child1.Rect = new Rectangle(Rect.X + width, Rect.Y, Rect.Width - width, Rect.Height);
                } else {
                    child0.Rect = new Rectangle(Rect.X, Rect.Y, Rect.Width, height);
                    child1.Rect = new Rectangle(Rect.X, Rect.Y + height, Rect.Width, Rect.Height - height);
                }

                return child0.Insert(width, height);
            }
        }


        public readonly int Dim;
        public readonly byte[] Data;
        private readonly List<Rectangle> rects = new List<Rectangle>();
        private Node root;


        public TextureAtlas(int dim)
        {
            Dim = dim;
            Data = new byte[dim * dim * 4];
            root = new Node {
                Rect = new Rectangle(0, 0, dim, dim)
            };
        }

        public void Freeze()
        {
            // the tree can be safely GCed now
            root = null;
        }

        public int Insert(byte[] image, int width, int height)
        {
            if (root == null)
                return -1; // we are frozen

            var node = root.Insert(width, height);
            if (node == null)
                return -1;

            BlitImage(image, node.Rect);

            int imageIndex = rects.Count;
            rects.Add(node.Rect);
            return imageIndex;
        }

        public Rectangle GetRectangle(int imageIndex)
        {
            return rects[imageIndex];
        }


        private void BlitImage(byte[] image, Rectangle rect)
        {
            int srcPitch = rect.Width * 4;
            int dstPitch = Dim * 4;

            for (int y = 0; y < rect.Height; ++y) {
                int srcOffset = y * srcPitch;
                int dstOffset = (rect.Y + y) * dstPitch + rect.X * 4;

                Buffer.BlockCopy(image, srcOffset, Data, dstOffset, srcPitch);
            }
        }
    }
}
