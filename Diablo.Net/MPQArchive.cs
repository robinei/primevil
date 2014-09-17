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


        public class File
        {
        }

        private class FileImpl : File
        {
            public string Path;
            public BlockEntry Entry;

            public uint CurrBlock;
            public uint BlockCount;
            public uint[] BlockOffsets;
            public uint Key;

            public byte[] BlockBuffer;
            public uint BlockPos;
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

        //private const uint InvalidPos = 0xffffffff;

        private readonly FileStream _file;
        private Header _header;

        private readonly uint[] _cryptTable = new uint[0x500];
        private uint[] _hashTable;
        private uint[] _blockTable;
        private readonly uint _blockSize;
        //private uint _blockSizeMask;

        public MPQArchive(string path)
        {
            _file = new FileStream(path, FileMode.Open);
            ReadStruct(_file, out _header);
            if (_header.ID != FourCC)
                throw new Exception("bad file header");
            if (_header.FormatVersion != 0)
                throw new NotImplementedException();

            PrepareCryptTable();
            ReadHashTable();
            ReadBlockTable();

            _blockSize = (uint)0x200 << _header.BlockSize;
            //_blockSizeMask = _blockSize - 1;
            Debug.WriteLine("BlockSize: " + _blockSize);
            Debug.WriteLine("BlockTableSize: " + _header.BlockTableSize);

            uint hashKey = HashString("(hash table)", 0x300);
            uint blockKey = HashString("(block table)", 0x300);
            Debug.Assert(hashKey == HashTableKey);
            Debug.Assert(blockKey == BlockTableKey);

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
                Read(f, temp, 0, 10);

                break;
            }

            Debug.WriteLine("searched files: " + count);

            var input = new byte[] {0x00, 0x04, 0x82, 0x24, 0x25, 0x8f, 0x80, 0x7f};
            var output = new byte[4096];

            var decoder = new PkZipDecoder();
            uint len = decoder.Decompress(input, 0, input.Length, output);
            Debug.WriteLine("decompressed length: " + len);
            string s = output.Aggregate("", (current, b) => current + (char)b);
            Debug.WriteLine("decompressed string: " + s);
        }

        File Open(string path)
        {
            int index = BlockIndexForPath(path);
            if (index < 0)
                throw new FileNotFoundException();

            var f = new FileImpl {
                Path = path
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
            f.CurrBlock = 0;
            f.BlockCount = 1 + (f.Entry.FSize - 1) / _blockSize;
            Debug.WriteLine("f.BlockCount: " + f.BlockCount);
            Debug.WriteLine("f.Entry.FilePos: " + f.Entry.FilePos);

            // calc decryption key
            if ((f.Entry.Flags & FileEncrypted) != 0)
                f.Key = FileKey(f.Path, ref f.Entry);

            LoadOffsetsTable(f);

            f.BlockBuffer = new byte[_blockSize];
            f.BlockPos = 0;
            f.BlockSize = 0;

            return f;
        }

        private void LoadOffsetsTable(FileImpl f)
        {
            uint offsetsCount = f.BlockCount + 1;
            if ((f.Entry.Flags & FileSectorCrc) != 0)
                ++offsetsCount;

            f.BlockOffsets = new uint[offsetsCount];

            // load table
            var offsetsSize = (int)(4 * offsetsCount);
            var offsetsBuffer = new byte[offsetsSize];
            _file.Seek(f.Entry.FilePos, SeekOrigin.Begin);
            _file.Read(offsetsBuffer, 0, offsetsSize);
            Buffer.BlockCopy(offsetsBuffer, 0, f.BlockOffsets, 0, offsetsSize);

            // decrypt table
            if ((f.Entry.Flags & FileEncrypted) != 0)
                Decrypt(f.BlockOffsets, f.Key - 1);

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

        public int Read(File file, byte[] buffer, uint offset, uint count)
        {
            var f = (FileImpl)file;

            if (f.BlockPos == f.BlockSize) {
                if (f.CurrBlock >= f.BlockCount)
                    return 0;
                
                uint pos = f.Entry.FilePos + f.BlockOffsets[f.CurrBlock];
                uint size = f.BlockOffsets[f.CurrBlock + 1] - f.BlockOffsets[f.CurrBlock];

                var buf = new byte[size];
                _file.Seek(pos, SeekOrigin.Begin);
                _file.Read(buf, 0, (int)size);

                Decrypt(buf, f.Key + f.CurrBlock);

                var decoder = new PkZipDecoder();
                f.BlockSize = decoder.Decompress(buf, 0, buf.Length, f.BlockBuffer);
                Debug.WriteLine("decompressed length: " + f.BlockSize);

                f.CurrBlock++;
                f.BlockPos = 0;
                f.BlockSize = size;
            }

            return 0;
        }

        /*public int Read(File file, byte[] buffer, uint offset, uint count)
        {
            if (offset + count > buffer.Length)
                throw new ArgumentException("buffer not big enough");
            
            var f = (FileImpl)file;
            
            // If the file position is at or beyond end of file, do nothing
            if (f.Pos >= f.Entry.FSize)
                return 0;
            
            // If not enough bytes in the file remaining, cut them
            if (count > (f.Entry.FSize - f.Pos))
                count = f.Entry.FSize - f.Pos;
            
            // Compute block position in the file
            uint blockPos = f.Pos & ~_blockSizeMask;
            
            if (f.BlockBuffer == null)
                f.BlockBuffer = new byte[_blockSize];

            if ((f.Pos & _blockSizeMask) != 0) {
                uint bytesInBlock = _blockSize;
                uint bufferOffset = f.Pos & _blockSizeMask;

                if (f.BlockOffset != blockPos) {
                    // f.Pos is not in the currently loaded block

                } else {
                    // read to the end of the block, and no more
                    if (blockPos + bytesInBlock > f.Entry.FSize)
                        bytesInBlock = f.Entry.FSize - blockPos;
                }

                uint bytesToCopy = bytesInBlock - bufferOffset;
                if (bytesToCopy > count)
                    bytesToCopy = count;

                Buffer.BlockCopy(f.BlockBuffer, (int)bufferOffset, buffer, (int)offset, (int)bytesToCopy);
            }

            return 0;
        }*/

        int BlockIndexForPath(string path)
        {
            uint index = HashString(path, 0x000) % _header.HashTableSize;
            uint name1 = HashString(path, 0x100);
            uint name2 = HashString(path, 0x200);
            uint startIndex = index;

            do {
                HashEntry h;
                GetHashEntry((int)index, out h);

                if (h.Name1 == name1 && h.Name2 == name2 && h.BlockIndex < _header.BlockTableSize)
                    return (int)h.BlockIndex;
                index = (index + 1) % _header.HashTableSize;
            } while (index != startIndex);
            return -1;
        }

        uint HashString(string filename, uint hashtype)
        {
            uint seed1 = 0x7FED7FED, seed2 = 0xEEEEEEEE;
            foreach (var ch in filename) {
                var uch = Char.ToUpper(ch);
                if (uch == '/') uch = '\\';
                seed1 = _cryptTable[hashtype + uch] ^ (seed1 + seed2);
                seed2 = uch + seed1 + seed2 + (seed2 << 5) + 3;
            }
            return seed1; 
        }

        private void GetHashEntry(int index, out HashEntry h)
        {
            h.Name1 = _hashTable[index * 4 + 0];
            h.Name2 = _hashTable[index * 4 + 1];
            h.Locale = 0;
            h.Platform = 0;
            h.BlockIndex = _hashTable[index * 4 + 3];
        }

        private void GetBlockEntry(int index, out BlockEntry b)
        {
            b.FilePos = _blockTable[index * 4 + 0];
            b.CSize = _blockTable[index * 4 + 1];
            b.FSize = _blockTable[index * 4 + 2];
            b.Flags = _blockTable[index * 4 + 3];
        }

        private void Decrypt(byte[] bytes, int offset, int length, uint key)
        {
            int count = length / 4;
            var data = new uint[count];
            Buffer.BlockCopy(bytes, offset, data, 0, count * 4);
            Decrypt(data, key);
            Buffer.BlockCopy(data, 0, bytes, 0, count * 4);
        }

        private void Decrypt(uint[] data, int offset, int length, uint key)
        {
            uint seed = 0xEEEEEEEE;

            int stop = offset + length;
            for (int i = offset; i < stop; ++i) {
                seed += _cryptTable[0x400 + (key & 0xFF)];
                uint ch = data[i] ^ (key + seed);

                key = ((~key << 0x15) + 0x11111111) | (key >> 0x0B);
                seed = ch + seed + (seed << 5) + 3;
                data[i] = ch;
            }
        }

        private void Decrypt(byte[] bytes, uint key)
        {
            Decrypt(bytes, 0, bytes.Length, key);
        }

        private void Decrypt(uint[] data, uint key)
        {
            Decrypt(data, 0, data.Length, key);
        }

        private uint FileKey(string path, ref BlockEntry b)
        {
            var filename = GetFileName(path);
            uint fileKey = HashString(filename, 0x300);
            if ((b.Flags & FileFixKey) != 0)
                fileKey = (fileKey + b.FilePos) ^ b.FSize;
            return fileKey;
        }

        /*private static byte[] Explode(byte[] compressedBytes)
        {
            using (var input = new MemoryStream(compressedBytes))
            using (var output = new MemoryStream()) {
                using (var zip = new GZipStream(input, CompressionMode.Decompress)) {
                    zip.CopyTo(output);
                }
                return output.ToArray();
            }
        }*/

        private void PrepareCryptTable()
        {
            uint seed = 0x00100001;

            for (int index1 = 0; index1 < 0x100; index1++) {
                for (int index2 = index1, i = 0; i < 5; i++, index2 += 0x100) {
                    seed = (seed * 125 + 3) % 0x2AAAAB;
                    uint temp1 = (seed & 0xFFFF) << 0x10;

                    seed = (seed * 125 + 3) % 0x2AAAAB;
                    uint temp2 = (seed & 0xFFFF);

                    _cryptTable[index2] = (temp1 | temp2);
                }
            }
        }

        private void ReadHashTable()
        {
            var size = _header.HashTableSize * Marshal.SizeOf(typeof(HashEntry));
            var buf = new byte[size];
            _file.Seek(_header.HashTablePos, SeekOrigin.Begin);
            _file.Read(buf, 0, (int)size);

            _hashTable = new uint[size / 4];
            Buffer.BlockCopy(buf, 0, _hashTable, 0, (int)size);
            Decrypt(_hashTable, HashTableKey);
        }

        private void ReadBlockTable()
        {
            var size = _header.BlockTableSize * Marshal.SizeOf(typeof(BlockEntry));
            var buf = new byte[size];
            _file.Seek(_header.BlockTablePos, SeekOrigin.Begin);
            _file.Read(buf, 0, (int)size);

            _blockTable = new uint[size / 4];
            Buffer.BlockCopy(buf, 0, _blockTable, 0, (int)size);
            Decrypt(_blockTable, BlockTableKey);
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
}
