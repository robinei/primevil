using System;
using Primevil.Formats;
using System.Diagnostics;

namespace Primevil.Game
{
    public static class TownLevel
    {
        public static Level Load(MPQArchive mpq)
        {
            var palette = new byte[768];
            using (var f = mpq.Open("levels/towndata/town.pal")) {
                var len = f.Read(palette, 0, 768);
                Debug.Assert(len == palette.Length);
            }

            byte[] solData;
            using (var f = mpq.Open("levels/towndata/town.sol")) {
                solData = new byte[f.Length];
                var len = f.Read(solData, 0, (int)f.Length);
                Debug.Assert(len == f.Length);
            }

            var celFile = CELFile.Load(mpq, "levels/towndata/town.cel");
            var minFile = MINFile.Load(mpq, "levels/towndata/town.min");
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
                sectors[i] = new SectorTemplate(dunFile, tilFile, solData);
            }

            int mapWidth = sectors[0].Width + sectors[3].Width;
            int mapHeight = sectors[0].Height + sectors[3].Height;
            var map = new Map(mapWidth, mapHeight);

            map.PlaceSector(sectors[3], 0, 0);
            map.PlaceSector(sectors[2], 0, sectors[3].Height);
            map.PlaceSector(sectors[1], sectors[3].Width, 0);
            map.PlaceSector(sectors[0], sectors[3].Width, sectors[3].Height);

            var packer = new TextureAtlasPacker(2048);

            for (int i = 0; i < celFile.NumFrames; ++i) {
                var frame = celFile.GetFrame(i, palette);
                int rectId = packer.Insert(frame.Data, frame.Width, frame.Height, true);
                if (rectId < 0)
                    throw new Exception("atlas is full: " + i);
            }

            var atlas = packer.CreateAtlas();

            return new Level {
                Map = map,
                Tileset = atlas,
                PillarDefs = minFile
            };
        }
    }
}

