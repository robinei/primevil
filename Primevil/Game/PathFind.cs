using System;
using System.Collections.Generic;

namespace Primevil.Game
{
    class PathFind
    {
        private const uint OpenList = 1;
        private const uint ClosedList = 2;

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
                get { return (int)((field >> 5) & IndexMask); }
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
            // whether node is in open/closed list or neither
            public uint List
            {
                get { return field & ListMask; }
                set { field = (field & ~ListMask) | value; }
            }
        }

        private const float HScale = 1.0f;
        private const float GScale = 1.0f;
        private const float MaxCost = 10000.0f;

        private readonly List<int> heap = new List<int>();
        private readonly List<Coord> path = new List<Coord>();
        private readonly Node[] nodes;
        private readonly Map map;


        public PathFind(Map map)
        {
            nodes = new Node[map.Width * map.Height];
            this.map = map;
        }

        /*
         * Implements A* search.
         * A custom array based binary heap is used to track the best Open node,
         * and nodes are looked up by direct addresing using their coordinates in the
         * compact nodes array, so we don't need a separate associative container to
         * represent the Closed list.
         */
        public Coord[] Search(Coord start, Coord goal)
        {
            if (start.X < 0 || start.Y < 0 || start.X >= map.Width || start.Y >= map.Height)
                return null;
            if (goal.X < 0 || goal.Y < 0 || goal.X >= map.Width || goal.Y >= map.Height)
                return null;
            if (start == goal)
                return new Coord[]{};

            heap.Clear();
            path.Clear();
            for (int i = 0; i < map.Width * map.Height; ++i)
                nodes[i] = new Node();

            int n = start.Y * map.Width + start.X;
            nodes[n].H = Heuristic(start, goal);
            nodes[n].List = OpenList;
            PushHeap(n);

            while (heap.Count > 0) {
                // examine the best node in the Open list and mark it as Closed
                n = PopHeap();
                nodes[n].List = ClosedList;
                var pos = new Coord(n % map.Width, n / map.Width);

                // are we there yet?
                if (pos == goal) {
                    // walk backward to the start while remembering the coords we pass by
                    path.Add(pos);
                    do {
                        pos += nodes[n].ParentDir.DeltaCoord();
                        path.Add(pos);
                        n = pos.Y * map.Width + pos.X;
                    } while (pos != start);
                    // reverse it, so the path is in the expected sequence from start to goal
                    path.Reverse();
                    return path.ToArray();
                }

                // expand all surrounding squares
                for (int d = 0; d < 8; ++d) {
                    var dir = (Direction)d;
                    var p = pos + dir.DeltaCoord();

                    // we depend on coords outside the map to return false as well
                    if (!map.IsPassable(p))
                        continue;

                    // calc index of node to expand
                    int t = p.Y * map.Width + p.X;
                    
                    // if it is on the ClosedList then we have already examined it
                    if (nodes[t].List == ClosedList)
                        continue;

                    // get the cost of moving into this cell from our direction
                    float cost = nodes[n].G + dir.StepDistance() * GScale;
                    if (cost > MaxCost)
                        continue;

                    if (nodes[t].List == OpenList) {
                        // the node is on the Open list,
                        // meaning it's been expanded but not examined.
                        if (cost < nodes[t].G) {
                            // if the new path to this node is better, update cost and
                            // parent then correct the heap according to cost change
                            nodes[t].G = cost;
                            nodes[t].ParentDir = dir.Opposite();
                            UpHeap(nodes[t].Index);
                        }
                    } else {
                        // we've never seen this node before
                        nodes[t].H = Heuristic(p, goal);
                        nodes[t].G = cost;
                        nodes[t].ParentDir = dir.Opposite();
                        nodes[t].List = OpenList;
                        PushHeap(t);
                    }
                }
            }

            return null;
        }


        static float Heuristic(Coord a, Coord b)
        {
            // manhattan distance
            return (Math.Abs(b.X - a.X) + Math.Abs(b.Y - a.Y)) * HScale;
        }
        
        bool CmpHeap(int i, int j)
        {
            var a = nodes[heap[i]];
            var b = nodes[heap[j]];
            return (a.G + a.H) < (b.G + b.H);
        }

        void PushHeap(int n)
        {
            var index = heap.Count;
            nodes[n].Index = index;
            heap.Add(n);
            UpHeap(index);
        }
    
        int PopHeap()
        {
            int top = heap[0];
            int last = heap[heap.Count - 1];
            heap[0] = last;
            nodes[last].Index = 0;
            heap.RemoveAt(heap.Count - 1);
            DownHeap(0);
            return top;
        }
    
        void UpHeap(int i) {
            while(i > 0) {
                int p = (i - 1) >> 1; // parent
                if(!CmpHeap(i, p)) break;
                SwapHeap(i, p);
                i = p;
            }
        }

        void DownHeap(int i) {
            while(true) {
                int c1 = (i << 1) + 1; // child 1
                int c2 = (i << 1) + 2; // child 2
            
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
    
        void SwapHeap(int i, int j) {
            int temp = heap[i];
            heap[i] = heap[j];
            heap[j] = temp;
            nodes[heap[i]].Index = i;
            nodes[heap[j]].Index = j;
        }
    }
}
