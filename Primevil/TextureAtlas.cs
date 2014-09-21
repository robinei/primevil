using System;
using Microsoft.Xna.Framework;
using System.Collections.Generic;

namespace Primevil
{
    class TextureAtlas
    {
        private struct Node
        {
            public Rectangle Rect;
            public int Child0;
            public int Child1;
            public bool Populated;

            public Node(Rectangle rect) {
                Rect = rect;
                Child0 = -1;
                Child1 = -1;
                Populated = false;
            }
        }


        public readonly int Dim;
        public readonly byte[] Data;

        // stores the rectangles of all inserted images
        private readonly List<Rectangle> rects = new List<Rectangle>();

        private int nodeCount = 0;
        private Node[] nodes = new Node[512]; // must be array to support internal refs


        public TextureAtlas(int dim)
        {
            Dim = dim;
            Data = new byte[dim * dim * 4];
            AddNode(new Node(new Rectangle(0, 0, dim, dim)));
        }

        public int Insert(byte[] image, int width, int height, bool flipVertical = false)
        {
            var nodeIndex = Insert(ref nodes[0], 0, width, height);
            if (nodeIndex < 0)
                return -1;

            var r = nodes[nodeIndex].Rect;
            BlitImage(image, r, flipVertical);

            int rectIndex = rects.Count;
            rects.Add(r);
            return rectIndex;
        }

        public Rectangle GetRectangle(int rectIndex)
        {
            return rects[rectIndex];
        }

        public int RectangleCount
        {
            get { return rects.Count; }
        }

        public Rectangle[] Rectangles
        {
            get { return rects.ToArray(); }
        }


        private int Insert(ref Node node, int nodeIndex, int width, int height)
        {
            // is this an internal node? of so, recurse
            if (node.Child0 >= 0) {
                var index = Insert(ref nodes[node.Child0], node.Child0, width, height);
                if (index >= 0)
                    return index;
                // try the second child, if the first failed
                return Insert(ref nodes[node.Child1], node.Child1, width, height);
            }

            // this is a leaf node

            // if this leaf is populated, then bail
            if (node.Populated)
                return -1;

            // is it too small?
            var r = node.Rect;
            if (width > r.Width || height > r.Height)
                return -1;

            // or just right?
            if (width == r.Width && height == r.Height) {
                node.Populated = true;
                return nodeIndex;
            }

            // if larger than necessary, we split across the longest axis,
            // ensuring the first child is just big enough along this axis
            int dw = r.Width - width;
            int dh = r.Height - height;
            Rectangle rect0, rect1;
            if (dw > dh) {
                rect0 = new Rectangle(r.X, r.Y, width, r.Height);
                rect1 = new Rectangle(r.X + width, r.Y, r.Width - width, r.Height);
            } else {
                rect0 = new Rectangle(r.X, r.Y, r.Width, height);
                rect1 = new Rectangle(r.X, r.Y + height, r.Width, r.Height - height);
            }

            int child0 = nodeCount;
            node.Child0 = child0;
            node.Child1 = child0 + 1;

            // these may invalidate the node ref, so don't use it any more
            AddNode(new Node(rect0));
            AddNode(new Node(rect1));

            // insert into first child which is just big enough along
            // at least one axis
            return Insert(ref nodes[child0], child0, width, height);
        }

        private void AddNode(Node node) {
            if (nodeCount >= nodes.Length)
                Array.Resize(ref nodes, nodes.Length * 2);
            nodes[nodeCount++] = node;
        }

        private void BlitImage(byte[] image, Rectangle rect, bool flipVertical)
        {
            int srcPitch = rect.Width * 4;
            int dstPitch = Dim * 4;

            for (int y = 0; y < rect.Height; ++y) {
                int srcOffset = (flipVertical ? rect.Height - y - 1 : y) * srcPitch;
                int dstOffset = (rect.Y + y) * dstPitch + rect.X * 4;

                Buffer.BlockCopy(image, srcOffset, Data, dstOffset, srcPitch);
            }
        }
    }
}
