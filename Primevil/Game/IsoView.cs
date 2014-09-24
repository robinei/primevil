﻿using System.Collections.Generic;

namespace Primevil.Game
{
    class IsoView
    {
        public const int TileWidth = 64;
        public const int TileHeight = 32;

        public CoordF ViewOffset;
        public Size ViewSize;
        public Level Level;
        public Coord HoveredTile;

        public delegate void TileDrawer(object texture, Coord pos, Rect sourceRect);
        public TileDrawer DrawTile;

        // just testing pathfinding
        public readonly HashSet<Coord> PathSet = new HashSet<Coord>();


        public void CenterOn(Coord c)
        {
            var wc = TileToWorld(c).ToCoordF();
            ViewOffset = new CoordF(wc.X - ViewSize.Width/2.0f, wc.Y - ViewSize.Height/2.0f);
        }


        #region Coordinate conversions
        public static Coord TileToWorld(Coord tilePos)
        {
            return TileToWorld(tilePos.ToCoordF()).ToCoord();
        }

        public static CoordF TileToWorld(CoordF tilePos)
        {
            const int w = TileWidth / 2;
            const int h = TileHeight / 2;
            return new CoordF(
                tilePos.X * w - tilePos.Y * w,
                tilePos.X * h + tilePos.Y * h
            );
        }

        public CoordF TileToScreen(CoordF tilePos)
        {
            var c = TileToWorld(tilePos);
            return new CoordF(c.X - ViewOffset.X - 32.0f, c.Y - ViewOffset.Y);
        }

        public Coord TileToScreen(Coord tilePos)
        {
            return TileToScreen(tilePos.ToCoordF()).ToCoord();
        }

        public Coord ScreenToTile(Coord screenPos)
        {
            return ScreenToTile(screenPos.ToCoordF()).ToCoord();
        }

        public CoordF ScreenToTile(CoordF screenPos)
        {
            const int w = 32; // tileWidth / 2
            const int h = 16; // tileHeight / 2
            const float wh2 = 2 * w * h;
            int xoff = -(int)ViewOffset.X;
            int yoff = -(int)ViewOffset.Y;

            return new CoordF(
                (screenPos.X * h + screenPos.Y * w - xoff * h - yoff * w) / wh2,
                (-screenPos.X * h + screenPos.Y * w + xoff * h - yoff * w) / wh2
            );
        }
        #endregion


        public void DrawMap()
        {
            var map = Level.Map;

            // calc coordinate of upper left corner of screen
            var tilePos = ScreenToTile(new Coord(0, 0));
            // move two tiles to the north-west to ensure we start outside the screen
            tilePos.X -= 2;
            int i0 = tilePos.X;
            int j0 = tilePos.Y;

            // convert back to screen space
            var screenPos = TileToScreen(tilePos);
            int x = screenPos.X;
            int y = screenPos.Y;

            int row = 0;
            int x0 = x;
            int i = i0, j = j0;
            while (y < ViewSize.Height + 300) {
                for (; x < ViewSize.Width; x += TileWidth, ++i, --j) {
                    //if (i == HoveredTile.X && j == HoveredTile.Y)
                    //    continue;
                    //if (PathSet.Contains(new Coord(i, j)))
                    //    continue;
                    if (i < 0 || j < 0 || i >= map.Width || j >= map.Height)
                        continue;

                    int minIndex = map.GetPillar(i, j);
                    if (minIndex >= 0)
                        DrawPillar(minIndex, x, y);

                    var creature = map.GetCreature(new Coord(i, j));
                    if (creature != null)
                        DrawCreature(creature);
                }

                x = x0;
                y += TileHeight / 2;
                if (++row % 2 != 0) {
                    x -= TileWidth / 2;
                    ++j0;
                } else {
                    ++i0;
                }
                i = i0;
                j = j0;
            }
        }

        private void DrawCreature(Creature c)
        {
            var p = TileToScreen(c.Position).ToCoord();
            var f = c.CurrentAnimation.CurrentFrame;
            DrawTile(f.Texture, new Coord(p.X - f.Width/2 + 32, p.Y - f.Height + 15), f.SourceRect);
        }

        private void DrawPillar(int pillarIndex, int xPos, int yPos)
        {
            int maxY = Level.PillarDefs.PillarHeight;
            yPos -= (maxY - 1) * TileHeight; // yPos initially points to position of lowest tile
            for (int x = 0; x < 2; ++x) {
                for (int y = 0; y < maxY; ++y) {
                    int celIndex = Level.PillarDefs.GetCelIndex(pillarIndex, x, y);
                    if (celIndex < 0)
                        continue;
                    DrawTile(Level.Tileset.Texture, new Coord(xPos + x * TileWidth / 2, yPos + y * TileHeight),
                        Level.Tileset.Rects[celIndex]);
                }
            }
        }
    }
}
