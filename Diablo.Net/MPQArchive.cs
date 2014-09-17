using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;

namespace Diablo.Net
{
    public class MPQArchive
    {
        // ReSharper disable FieldCanBeMadeReadOnly.Local
        // ReSharper disable MemberCanBePrivate.Local

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct Header
        {
            public uint ID;
            public uint HeaderSize;
            public uint ArchiveSize;
            public ushort FormatVersion;
            public ushort BlockSize;
            public uint HashTablePos;
            public uint BlockTablePos;
            public uint HashTableSize;
            public uint BlockTableSize;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct HashEntry
        {
            public uint Name1;
            public uint Name2;
            public ushort Locale;
            public ushort Platform;
            public uint BlockIndex;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct BlockEntry
        {
            public uint FilePos;
            public uint CSize;
            public uint FSize;
            public uint Flags;
        }

        // ReSharper restore FieldCanBeMadeReadOnly.Local
        // ReSharper restore MemberCanBePrivate.Local



        private class FileHandle
        {
            public string Path;
            public FileStream Stream;
            public uint DecryptionKey;

            public BlockEntry Entry;

            public uint Pos;
            public uint BlockIndex;

            public uint BlockCount;
            public uint[] BlockOffsets;

            public byte[] BlockBuffer;
            public uint BlockSize;
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
        private Header header;
        private uint[] hashTable;
        private uint[] blockTable;
        
        public readonly uint BlockSize;


        static MPQArchive()
        {
            PrepareCryptTable();
        }

        public MPQArchive(string path)
        {
            archivePath = path;
            var file = new FileStream(path, FileMode.Open, FileAccess.Read);
            ReadStruct(file, out header);
            if (header.ID != FourCC)
                throw new Exception("bad file header");
            if (header.FormatVersion != 0)
                throw new NotImplementedException();

            BlockSize = (uint)0x200 << header.BlockSize;
            ReadHashTable(file);
            ReadBlockTable(file);

            //Debug.WriteLine("BlockSize: " + BlockSize);
            //Debug.WriteLine("BlockTableSize: " + header.BlockTableSize);

            /*uint hashKey = HashString("(hash table)", 0x300);
            uint blockKey = HashString("(block table)", 0x300);
            Debug.Assert(hashKey == HashTableKey);
            Debug.Assert(blockKey == BlockTableKey);*/
            /*
            int count = 0;
            foreach (var name in Diabdat.Names) {
                var index = BlockIndexForPath(name);
                if (index < 0)
                    continue;

                ++count;

                BlockEntry b;
                GetBlockEntry(index, out b);
                if ((b.Flags & FileImplode) == 0)
                    continue;
                //if ((b.Flags & FileEncrypted) != 0)
                //    continue;

                Debug.WriteLine("---------");
                Debug.WriteLine("Name: " + name);
                Debug.WriteLine("Index: " + index);
                PrintBlockEntry(b);

                var f = Open(name);

                var temp = new byte[10];
                f.Read(temp, 0, 10);

                break;
            }

            Debug.WriteLine("searched files: " + count);

            var input = new byte[] {0x00, 0x04, 0x82, 0x24, 0x25, 0x8f, 0x80, 0x7f};
            var output = new byte[4096];

            var decoder = new PkUnzipper();
            uint len = decoder.Decompress(input, 0, input.Length, output);
            Debug.WriteLine("decompressed length: " + len);
            string s = output.Aggregate("", (current, b) => current + (char)b);
            Debug.WriteLine("decompressed string: " + s);*/
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

            if ((f.Entry.Flags & FileDeleteMarker) != 0)
                throw new FileNotFoundException();
            if ((f.Entry.Flags & FileExists) == 0)
                throw new FileNotFoundException();

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

            Debug.Assert(f.Entry.FSize > 0);
            f.Pos = 0;
            f.BlockIndex = 0xffffffff; // initially invalid
            f.BlockCount = 1 + (f.Entry.FSize - 1) / BlockSize;
            Debug.WriteLine("f.BlockCount: " + f.BlockCount);
            Debug.WriteLine("f.Entry.FilePos: " + f.Entry.FilePos);

            if ((f.Entry.Flags & FileEncrypted) != 0)
                f.DecryptionKey = FileKey(f.Path, ref f.Entry);

            LoadOffsetsTable(f);

            f.BlockBuffer = new byte[BlockSize];
            f.BlockSize = 0;

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
            return 0;
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
            if (f.Pos >= f.Entry.FSize)
                return 0;

            var blockIndex = f.Pos / BlockSize;
            var blockPos = f.Pos % BlockSize;

            if (blockIndex != f.BlockIndex) {
                var pos = f.Entry.FilePos + f.BlockOffsets[blockIndex];
                uint size = f.BlockOffsets[blockIndex + 1] - f.BlockOffsets[blockIndex];

                var buf = new byte[size];
                f.Stream.Seek(pos, SeekOrigin.Begin);
                f.Stream.Read(buf, 0, (int)size);

                Decrypt(buf, f.DecryptionKey + blockIndex);

                var decoder = new PkUnzipper();
                f.BlockSize = decoder.Decompress(buf, 0, buf.Length, f.BlockBuffer);
                Debug.WriteLine("decompressed length: " + f.BlockSize);

                f.BlockIndex = blockIndex;
                f.BlockSize = size;
            }

            if (blockPos + count > BlockSize)
                count = (int)(BlockSize - blockPos);
            if (f.Pos + count > f.Entry.FSize)
                count = (int)(f.Entry.FSize - f.Pos);

            Buffer.BlockCopy(f.BlockBuffer, (int)blockPos, buffer, offset, count);
            f.Pos += (uint)count;
            return count;
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
                Decrypt(f.BlockOffsets, f.DecryptionKey - 1);

            // verify table
            for (int i = 0; i < f.BlockCount; ++i) {
                uint offset0 = f.BlockOffsets[i];
                uint offset1 = f.BlockOffsets[i + 1];
                if (offset1 <= offset0)
                    throw new Exception("corrupt offset table");
            }

            for (int i = 0; i <= f.BlockCount; ++i)
                Debug.WriteLine("offset: " + f.BlockOffsets[i]);
        }

        int BlockIndexForPath(string path)
        {
            uint index = HashString(path, 0x000) % header.HashTableSize;
            uint name1 = HashString(path, 0x100);
            uint name2 = HashString(path, 0x200);
            uint startIndex = index;

            do {
                HashEntry h;
                GetHashEntry((int)index, out h);

                if (h.Name1 == name1 && h.Name2 == name2 && h.BlockIndex < header.BlockTableSize)
                    return (int)h.BlockIndex;
                index = (index + 1) % header.HashTableSize;
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

        private static void Decrypt(byte[] bytes, int offset, int length, uint key)
        {
            int count = length / 4;
            var data = new uint[count];
            Buffer.BlockCopy(bytes, offset, data, 0, count * 4);
            Decrypt(data, key);
            Buffer.BlockCopy(data, 0, bytes, 0, count * 4);
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

        private static void Decrypt(byte[] bytes, uint key)
        {
            Decrypt(bytes, 0, bytes.Length, key);
        }

        private static void Decrypt(uint[] data, uint key)
        {
            Decrypt(data, 0, data.Length, key);
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
            h.Locale = 0;
            h.Platform = 0;
            h.BlockIndex = hashTable[index * 4 + 3];
        }

        private void GetBlockEntry(int index, out BlockEntry b)
        {
            b.FilePos = blockTable[index * 4 + 0];
            b.CSize = blockTable[index * 4 + 1];
            b.FSize = blockTable[index * 4 + 2];
            b.Flags = blockTable[index * 4 + 3];
        }

        private void ReadHashTable(FileStream file)
        {
            var size = header.HashTableSize * Marshal.SizeOf(typeof(HashEntry));
            var buf = new byte[size];
            file.Seek(header.HashTablePos, SeekOrigin.Begin);
            file.Read(buf, 0, (int)size);

            hashTable = new uint[size / 4];
            Buffer.BlockCopy(buf, 0, hashTable, 0, (int)size);
            Decrypt(hashTable, HashTableKey);
        }

        private void ReadBlockTable(FileStream file)
        {
            var size = header.BlockTableSize * Marshal.SizeOf(typeof(BlockEntry));
            var buf = new byte[size];
            file.Seek(header.BlockTablePos, SeekOrigin.Begin);
            file.Read(buf, 0, (int)size);

            blockTable = new uint[size / 4];
            Buffer.BlockCopy(buf, 0, blockTable, 0, (int)size);
            Decrypt(blockTable, BlockTableKey);
        }


        private void PrintBlockEntry(BlockEntry b)
        {
            Debug.WriteLine("FilePos: " + b.FilePos);
            Debug.WriteLine("CSize: " + b.CSize);
            Debug.WriteLine("FSize: " + b.FSize);
            Debug.WriteLine("Flags: " + b.Flags);
            Debug.WriteLine("FileImplode: " + ((b.Flags & FileImplode) != 0));
            Debug.WriteLine("FileCompress: " + ((b.Flags & FileCompress) != 0));
            Debug.WriteLine("FileEncrypted: " + ((b.Flags & FileEncrypted) != 0));
            Debug.WriteLine("FileFixKey: " + ((b.Flags & FileFixKey) != 0));
            Debug.WriteLine("FilePatchFile: " + ((b.Flags & FilePatchFile) != 0));
            Debug.WriteLine("FileSingleUnit: " + ((b.Flags & FileSingleUnit) != 0));
            Debug.WriteLine("FileDeleteMarker: " + ((b.Flags & FileDeleteMarker) != 0));
            Debug.WriteLine("FileSectorCrc: " + ((b.Flags & FileSectorCrc) != 0));
            Debug.WriteLine("FileExists: " + ((b.Flags & FileExists) != 0));
            Debug.WriteLine("Flags & ~AllFlagsMask: " + (b.Flags & ~AllFlagsMask));
        }

        private void PrintHashEntry(HashEntry h)
        {
            Debug.WriteLine("Name1: " + h.Name1);
            Debug.WriteLine("Name2: " + h.Name2);
            Debug.WriteLine("Locale: " + h.Locale);
            Debug.WriteLine("Platform: " + h.Platform);
            Debug.WriteLine("BlockIndex: " + h.BlockIndex);
        }


        private static void ReadStruct<T>(byte[] buf, out T value) where T : struct
        {
            var handle = GCHandle.Alloc(buf, GCHandleType.Pinned);
            value = (T)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(T));
            handle.Free();
        }

        private static void ReadStruct<T>(Stream stream, out T value) where T : struct
        {
            var size = Marshal.SizeOf(typeof(T));
            var buf = new byte[size];
            stream.Read(buf, 0, size);
            ReadStruct(buf, out value);
        }

        private static string GetFileName(string path)
        {
            int i = path.LastIndexOf('/');
            if (i < 0)
                return path;
            return path.Substring(i + 1);
        }
    }



    public class MPQStream : Stream
    {
        public override void Flush()
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return archive.Seek(this, offset, origin);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return archive.Read(this, buffer, offset, count);
        }

        public override bool CanRead { get { return true; } }
        public override bool CanSeek { get { return true; } }
        public override bool CanWrite { get { return false; } }

        public override long Length
        {
            get { return archive.Length(this); }
        }

        public override long Position
        {
            get { return archive.Position(this); }
            set { Seek(value, SeekOrigin.Begin); }
        }



        private readonly MPQArchive archive;
        private readonly object handle;

        internal MPQStream(MPQArchive archive, object handle)
        {
            this.archive = archive;
            this.handle = handle;
        }

        public MPQArchive Archive
        {
            get { return archive; }
        }

        public object Handle
        {
            get { return handle; }
        }
    }
}
