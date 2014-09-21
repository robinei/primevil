using System;
using System.Diagnostics;
using System.IO;

namespace Primevil.Formats
{
    public class MPQArchive
    {
        struct HashEntry
        {
            public uint Name1;
            public uint Name2;
            //public ushort Locale;
            //public ushort Platform;
            public uint BlockIndex;
        }

        public struct BlockEntry
        {
            public uint FilePos;
            public uint CSize;
            public uint FSize;
            public uint Flags;
        }

        private class FileHandle : IDisposable
        {
            public string Path;
            public FileStream Stream;
            public uint DecryptionKey;

            public BlockEntry Entry;

            public uint Pos;
            public uint BlockIndex;

            public uint BlockCount;
            public uint[] BlockOffsets;

            public uint BlockSize;
            public byte[] BlockBuffer;

            public byte[] TempBuffer;
            public uint[] CryptBuffer;

            public void Dispose()
            {
                if (Stream != null)
                    Stream.Dispose();
            }
        }

        private const uint FourCC = 0x1a51504d;

        // encryption keys for these two tables (files use key calculated from name)
        private const uint HashTableKey = 0xC3AF3770;
        private const uint BlockTableKey = 0xEC83B3A3;

        private const uint FileImplode = 0x00000100;
        private const uint FileCompress = 0x00000200;       // no diabdat files use this
        private const uint FileEncrypted = 0x00010000;
        private const uint FileFixKey = 0x00020000;         // no diabdat files use this
        private const uint FilePatchFile = 0x00100000;      // no diabdat files use this
        private const uint FileSingleUnit = 0x01000000;     // no diabdat files use this
        private const uint FileDeleteMarker = 0x02000000;   // no diabdat files use this
        private const uint FileSectorCrc = 0x04000000;      // no diabdat files use this
        private const uint FileExists = 0x80000000;         // ALL diabdat files use this

        private const uint AllFlagsMask = FileImplode | FileCompress | FileEncrypted | FileFixKey
            | FilePatchFile | FileSingleUnit | FileDeleteMarker | FileSectorCrc | FileExists;

        private const uint CompressedFlagsMask = FileImplode | FileCompress;

        private static readonly uint[] CryptTable = new uint[0x500];

        private readonly string archivePath;
        private readonly uint[] hashTable;
        private readonly uint[] blockTable;
        private readonly uint hashTableSize;
        private readonly uint blockTableSize;
        
        public readonly uint BlockSize;


        static MPQArchive()
        {
            PrepareCryptTable();
        }

        public MPQArchive(string path)
        {
            archivePath = path;

            var file = new FileStream(path, FileMode.Open, FileAccess.Read);
            var headerBuf = new byte[32];
            var len = file.Read(headerBuf, 0, 32);
            if (len != 32)
                throw new Exception("file too small");
            
            // read archive header
            var r = new BinaryReader(headerBuf);
            var id = r.ReadU32();
            r.Skip(8); // HeaderSize and ArchiveSize
            var version = r.ReadU16();
            BlockSize = (uint)(0x200 << r.ReadU16());
            var hashTablePos = r.ReadU32();
            var blockTablePos = r.ReadU32();
            hashTableSize = r.ReadU32();
            blockTableSize = r.ReadU32();

            if (id != FourCC)
                throw new Exception("bad file header");
            if (version != 0)
                throw new NotImplementedException();

            file.Seek(hashTablePos, SeekOrigin.Begin);
            hashTable = ReadHashTable(file, hashTableSize);

            file.Seek(blockTablePos, SeekOrigin.Begin);
            blockTable = ReadBlockTable(file, blockTableSize);
        }



        public MPQStream Open(string path)
        {
            int index = BlockIndexForPath(path);
            if (index < 0)
                throw new FileNotFoundException();

            var f = new FileHandle {
                Path = path,
                Stream = new FileStream(archivePath, FileMode.Open, FileAccess.Read)
            };
            GetBlockEntry(index, out f.Entry);

            if ((f.Entry.Flags & FileCompress) != 0)
                throw new NotImplementedException();
            if ((f.Entry.Flags & FileFixKey) != 0)
                throw new NotImplementedException();
            if ((f.Entry.Flags & FilePatchFile) != 0)
                throw new NotImplementedException();
            if ((f.Entry.Flags & FileSingleUnit) != 0)
                throw new NotImplementedException();
            if ((f.Entry.Flags & FileSectorCrc) != 0)
                throw new NotImplementedException();
            if ((f.Entry.Flags & ~AllFlagsMask) != 0)
                throw new Exception("unknown flags present");
            if (f.Entry.FSize == 0)
                throw new Exception("file size is zero");

            if ((f.Entry.Flags & FileDeleteMarker) != 0)
                throw new FileNotFoundException();
            if ((f.Entry.Flags & FileExists) == 0)
                throw new FileNotFoundException();

            if ((f.Entry.Flags & FileEncrypted) != 0)
                f.DecryptionKey = FileKey(f.Path, ref f.Entry);

            f.Pos = 0;
            f.BlockIndex = 0xffffffff; // initially invalid
            f.BlockCount = 1 + (f.Entry.FSize - 1) / BlockSize;
            f.BlockSize = 0;
            f.BlockBuffer = new byte[BlockSize];
            f.TempBuffer = new byte[BlockSize];
            f.CryptBuffer = new uint[BlockSize / 4];
            LoadOffsetsTable(f);

            return new MPQStream(this, f);
        }

        internal long Length(MPQStream stream)
        {
            var f = (FileHandle)stream.Handle;
            return f.Entry.FSize;
        }

        internal long Position(MPQStream stream)
        {
            var f = (FileHandle)stream.Handle;
            return f.Pos;
        }

        internal long Seek(MPQStream stream, long offset, SeekOrigin origin)
        {
            var f = (FileHandle)stream.Handle;
            long pos;
            switch (origin) {
            case SeekOrigin.Begin:
                pos = offset;
                break;
            case SeekOrigin.End:
                pos = f.Entry.FSize + offset;
                break;
            default: // SeekOrigin.Current
                pos = f.Pos + offset;
                break;
            }
            if (pos < 0)
                pos = 0;
            if (pos > f.Entry.FSize)
                pos = f.Entry.FSize;
            f.Pos = (uint)pos;
            return pos;
        }

        internal int Read(MPQStream stream, byte[] buffer, int offset, int count)
        {
            if (offset + count > buffer.Length)
                throw new ArgumentException();

            var f = (FileHandle)stream.Handle;
            int totalRead = 0;

            while (count > 0 && f.Pos < f.Entry.FSize) {
                var blockIndex = f.Pos / BlockSize;
                var blockPos = f.Pos % BlockSize;

                if (blockIndex != f.BlockIndex) {
                    var pos = f.Entry.FilePos + f.BlockOffsets[blockIndex];
                    uint size = f.BlockOffsets[blockIndex + 1] - f.BlockOffsets[blockIndex];

                    f.Stream.Seek(pos, SeekOrigin.Begin);
                    f.Stream.Read(f.TempBuffer, 0, (int)size);

                    if ((f.Entry.Flags & FileEncrypted) != 0)
                        Decrypt(f.TempBuffer, 0, (int)size, f.DecryptionKey + blockIndex, f.CryptBuffer);

                    if (f.Entry.CSize < f.Entry.FSize) {
                        if ((f.Entry.Flags & FileImplode) != 0) {
                            var unzip = new PkUnzipper();
                            f.BlockSize = unzip.Decompress(f.TempBuffer, 0, (int)size, f.BlockBuffer);
                        } else {
                            throw new Exception("corrupt file");
                        }
                    } else {
                        Buffer.BlockCopy(f.TempBuffer, 0, f.BlockBuffer, 0, (int)size);
                        f.BlockSize = size;
                    }
                    f.BlockIndex = blockIndex;
                }

                int toRead = count;
                if (blockPos + toRead > f.BlockSize)
                    toRead = (int)(f.BlockSize - blockPos);
                if (f.Pos + toRead > f.Entry.FSize)
                    toRead = (int)(f.Entry.FSize - f.Pos);

                Buffer.BlockCopy(f.BlockBuffer, (int)blockPos, buffer, offset, toRead);
                count -= toRead;
                offset += toRead;
                totalRead += toRead;
                f.Pos += (uint)toRead;
            }

            return totalRead;
        }




        private void LoadOffsetsTable(FileHandle f)
        {
            uint offsetsCount = f.BlockCount + 1;
            if ((f.Entry.Flags & FileSectorCrc) != 0)
                ++offsetsCount;

            f.BlockOffsets = new uint[offsetsCount];

            // load table
            var offsetsSize = (int)(4 * offsetsCount);
            var offsetsBuffer = new byte[offsetsSize];
            f.Stream.Seek(f.Entry.FilePos, SeekOrigin.Begin);
            f.Stream.Read(offsetsBuffer, 0, offsetsSize);
            Buffer.BlockCopy(offsetsBuffer, 0, f.BlockOffsets, 0, offsetsSize);

            // decrypt table
            if ((f.Entry.Flags & FileEncrypted) != 0)
                Decrypt(f.BlockOffsets, 0, (int)offsetsCount, f.DecryptionKey - 1);

            // verify table
            for (int i = 0; i < f.BlockCount; ++i) {
                uint offset0 = f.BlockOffsets[i];
                uint offset1 = f.BlockOffsets[i + 1];
                if (offset1 <= offset0)
                    throw new Exception("corrupt offset table");
            }
        }

        int BlockIndexForPath(string path)
        {
            uint index = HashString(path, 0x000) % hashTableSize;
            uint name1 = HashString(path, 0x100);
            uint name2 = HashString(path, 0x200);
            uint startIndex = index;

            do {
                HashEntry h;
                GetHashEntry((int)index, out h);

                if (h.Name1 == name1 && h.Name2 == name2 && h.BlockIndex < blockTableSize)
                    return (int)h.BlockIndex;
                index = (index + 1) % hashTableSize;
            } while (index != startIndex);
            return -1;
        }

        private static uint HashString(string filename, uint hashtype)
        {
            uint seed1 = 0x7FED7FED, seed2 = 0xEEEEEEEE;
            foreach (var ch in filename) {
                var uch = Char.ToUpper(ch);
                if (uch == '/') uch = '\\';
                seed1 = CryptTable[hashtype + uch] ^ (seed1 + seed2);
                seed2 = uch + seed1 + seed2 + (seed2 << 5) + 3;
            }
            return seed1; 
        }

        private static void Decrypt(byte[] bytes, int offset, int length, uint key, uint[] temp)
        {
            int count = length / 4;
            Buffer.BlockCopy(bytes, offset, temp, 0, count * 4);
            Decrypt(temp, 0, count, key);
            Buffer.BlockCopy(temp, 0, bytes, 0, count * 4);
        }

        private static void Decrypt(uint[] data, int offset, int length, uint key)
        {
            uint seed = 0xEEEEEEEE;

            int stop = offset + length;
            for (int i = offset; i < stop; ++i) {
                seed += CryptTable[0x400 + (key & 0xFF)];
                uint ch = data[i] ^ (key + seed);

                key = ((~key << 0x15) + 0x11111111) | (key >> 0x0B);
                seed = ch + seed + (seed << 5) + 3;
                data[i] = ch;
            }
        }

        private static uint FileKey(string path, ref BlockEntry b)
        {
            var filename = GetFileName(path);
            uint fileKey = HashString(filename, 0x300);
            if ((b.Flags & FileFixKey) != 0)
                fileKey = (fileKey + b.FilePos) ^ b.FSize;
            return fileKey;
        }

        private static void PrepareCryptTable()
        {
            uint seed = 0x00100001;

            for (int index1 = 0; index1 < 0x100; index1++) {
                for (int index2 = index1, i = 0; i < 5; i++, index2 += 0x100) {
                    seed = (seed * 125 + 3) % 0x2AAAAB;
                    uint temp1 = (seed & 0xFFFF) << 0x10;

                    seed = (seed * 125 + 3) % 0x2AAAAB;
                    uint temp2 = (seed & 0xFFFF);

                    CryptTable[index2] = (temp1 | temp2);
                }
            }
        }


        private void GetHashEntry(int index, out HashEntry h)
        {
            h.Name1 = hashTable[index * 4 + 0];
            h.Name2 = hashTable[index * 4 + 1];
            h.BlockIndex = hashTable[index * 4 + 3];
        }

        private void GetBlockEntry(int index, out BlockEntry b)
        {
            b.FilePos = blockTable[index * 4 + 0];
            b.CSize = blockTable[index * 4 + 1];
            b.FSize = blockTable[index * 4 + 2];
            b.Flags = blockTable[index * 4 + 3];
        }

        private static uint[] ReadHashTable(FileStream file, uint tableSize)
        {
            var size = tableSize * 16;
            var buf = new byte[size];
            file.Read(buf, 0, (int)size);

            var table = new uint[size / 4];
            Buffer.BlockCopy(buf, 0, table, 0, (int)size);
            Decrypt(table, 0, (int)size / 4, HashTableKey);
            return table;
        }

        private static uint[] ReadBlockTable(FileStream file, uint tableSize)
        {
            var size = tableSize * 16;
            var buf = new byte[size];
            file.Read(buf, 0, (int)size);

            var table = new uint[size / 4];
            Buffer.BlockCopy(buf, 0, table, 0, (int)size);
            Decrypt(table, 0, (int)size / 4, BlockTableKey);
            return table;
        }

        private static string GetFileName(string path)
        {
            int i = path.LastIndexOf('/');
            if (i < 0)
                return path;
            return path.Substring(i + 1);
        }
    }
}
