using System;
using System.Collections.Generic;

namespace Primevil.Game
{
    class PathFind
    {
        private enum NodeState
        {
            Virgin, // never before seen
            Open,   // expanded, but not examined
            Closed  // examined
        };

        private struct Node
        {
            // cost to reach this node
            public float G;

            // heuristic distance
            public float H;

            // field for compactly holding the 3 properties below
            private uint field;

            private const uint IndexMask = ((1 << 27) - 1);
            private const uint ParentMask = ((1 << 3) - 1);
            private const uint ListMask = ((1 << 2) - 1);

            // highest 27 bits of field.
            // index of node in heap
            public int Index
            {
                get { return (int)(field >> 5); }
                set { field = (field & ~(IndexMask << 5)) | ((uint)value << 5); }
            }

            // middle 3 bits.
            // direction of parent
            public Direction ParentDir
            {
                get { return (Direction)((field >> 2) & ParentMask); }
                set { field = (field & ~(ParentMask << 2)) | ((uint)value << 2); }
            }

            // lowest 2 bits.
            // whether node is in open/closed state or neither
            public NodeState State
            {
                get { return (NodeState)(field & ListMask); }
                set { field = (field & ~ListMask) | (uint)value; }
            }
        }

        private static float CalcHeuristicDistance(Coord a, Coord b)
        {
            // manhattan distance
            return (Math.Abs(b.X - a.X) + Math.Abs(b.Y - a.Y)) * HScale;
        }

        private const float HScale = 1.0f;
        private const float GScale = 1.0f;
        private const float MaxCost = 10000.0f;

        private readonly List<int> heap = new List<int>();
        private readonly Node[] nodes;
        private readonly int width, height;

        // calculate the cost of entering a node. can return float.MaxValue to
        // make a node impassable
        public delegate float NodeEvaluator(Coord pos, Direction fromDir);


        public PathFind(int width, int height)
        {
            this.width = width;
            this.height = height;
            nodes = new Node[width * height];
        }


        /*
         * Implements A* search.
         * A custom array based binary heap is used to track the best Open node,
         * and nodes are looked up by direct addresing using their coordinates in the
         * compact nodes array, so we don't need a separate associative container to
         * represent the Closed set.
         */
        public bool Search(Coord start, Coord goal, List<Coord> resultPath, NodeEvaluator costFunc)
        {
            resultPath.Clear();
            if (start.X < 0 || start.Y < 0 || start.X >= width || start.Y >= height)
                return false;
            if (goal.X < 0 || goal.Y < 0 || goal.X >= width || goal.Y >= height)
                return false;
            if (start == goal)
                return true;

            heap.Clear();
            ClearNodes();

            int n = start.Y * width + start.X;
            nodes[n].H = CalcHeuristicDistance(start, goal);
            nodes[n].State = NodeState.Open;
            PushHeap(n);

            while (heap.Count > 0) {
                // examine the best node in the Open set and mark it as Closed
                n = PopHeap();
                nodes[n].State = NodeState.Closed;
                var pos = new Coord(n % width, n / width);

                // are we there yet?
                if (pos == goal) {
                    // walk backward to the start while remembering the coords we pass by
                    do {
                        resultPath.Add(pos);
                        pos += nodes[n].ParentDir.DeltaCoord();
                        n = pos.Y * width + pos.X;
                    } while (pos != start);
                    // reverse it, so the path is in the expected sequence from start to goal
                    resultPath.Reverse();
                    return true;
                }

                // expand all surrounding squares
                for (int d = 0; d < 8; ++d) {
                    var dir = (Direction)d;
                    var fromDir = dir.Opposite();
                    var p = pos + dir.DeltaCoord();

                    if (p.X < 0 || p.Y < 0 || p.X >= width || p.Y >= height)
                        continue;

                    // calc index of node to expand
                    int t = p.Y * width + p.X;

                    // if it is on the ClosedList then we have already examined it
                    if (nodes[t].State == NodeState.Closed)
                        continue;

                    // get the cost of moving into cell t from our direction
                    float cost = dir.StepDistance() * costFunc(p, fromDir) * GScale;
                    if (cost > MaxCost)
                        continue;

                    // add to it the cost of reaching cell n
                    cost += nodes[n].G;

                    if (nodes[t].State == NodeState.Open) {
                        // this node has previously been expanded, but not examined
                        // check to see if this path was a better way to reach it
                        if (cost < nodes[t].G) {
                            // if the new path to this node is better, update cost and
                            // parent then correct the heap according to cost change
                            nodes[t].G = cost;
                            nodes[t].ParentDir = fromDir;
                            UpHeap(nodes[t].Index);
                        }
                    } else {
                        // we've never seen this node before
                        nodes[t].H = CalcHeuristicDistance(p, goal);
                        nodes[t].G = cost;
                        nodes[t].ParentDir = fromDir;
                        nodes[t].State = NodeState.Open;
                        PushHeap(t);
                    }
                }
            }

            return false;
        }


        private void ClearNodes()
        {
            nodes[0] = new Node();
            nodes[1] = new Node();
            nodes[2] = new Node();
            nodes[3] = new Node();
            int count, length = nodes.Length;
            for (count = 4; count <= length / 2; count *= 2)
                Array.Copy(nodes, 0, nodes, count, count);
            Array.Copy(nodes, 0, nodes, count, length - count);
        }

        private bool CmpHeap(int i, int j)
        {
            var a = nodes[heap[i]];
            var b = nodes[heap[j]];
            return (a.G + a.H) < (b.G + b.H);
        }

        private void PushHeap(int n)
        {
            var index = heap.Count;
            nodes[n].Index = index;
            heap.Add(n);
            UpHeap(index);
        }

        private int PopHeap()
        {
            int top = heap[0];
            int last = heap[heap.Count - 1];
            heap[0] = last;
            nodes[last].Index = 0;
            heap.RemoveAt(heap.Count - 1);
            DownHeap(0);
            return top;
        }

        private void UpHeap(int i)
        {
            while(i > 0) {
                int p = (i - 1) / 2; // parent
                if(!CmpHeap(i, p)) break;
                SwapHeap(i, p);
                i = p;
            }
        }

        private void DownHeap(int i)
        {
            while(true) {
                int c1 = (i * 2) + 1; // child 1
                int c2 = (i * 2) + 2; // child 2
            
                if(c1 < heap.Count && CmpHeap(c1, i)) {
                    if(c2 < heap.Count && CmpHeap(c2, c1)) {
                        SwapHeap(i, c2);
                        i = c2;
                    }
                    else {
                        SwapHeap(i, c1);
                        i = c1;
                    }
                }
                else if(c2 < heap.Count && CmpHeap(c2, i)) {
                    SwapHeap(i, c2);
                    i = c2;
                }
                else break;
            }
        }

        private void SwapHeap(int i, int j)
        {
            int temp = heap[i];
            heap[i] = heap[j];
            heap[j] = temp;
            nodes[heap[i]].Index = i;
            nodes[heap[j]].Index = j;
        }
    }
}
