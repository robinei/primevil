using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace Diablo.Net
{
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
    struct Block
    {
        public uint FilePos;
        public uint CSize;
        public uint FSize;
        public uint Flags;
    }

    class MPQArchive
    {
        private const uint FourCC = 441536589;

        private FileStream _file;
        private Header _header;

        public MPQArchive(string path)
        {
            _file = new FileStream(path, FileMode.Open);
            _header = ReadStruct<Header>(_file);

            if (_header.ID != FourCC)
                throw new Exception("bad file header");

            Debug.WriteLine("BlockTableSize: " + _header.BlockTableSize);
            Debug.WriteLine("BlockTablePos: " + _header.BlockTablePos);

            var b = ReadBlock(1100);
            Debug.WriteLine("FilePos: " + b.FilePos);
            Debug.WriteLine("CSize: " + b.CSize);
            Debug.WriteLine("FSize: " + b.FSize);
            Debug.WriteLine("Flags: " + b.Flags);
        }

        private static T ReadStruct<T>(Stream stream) where T : struct
        {
            var size = Marshal.SizeOf(typeof(T));
            var buf = new byte[size];
            stream.Read(buf, 0, size);
            var handle = GCHandle.Alloc(buf, GCHandleType.Pinned);
            var value = (T)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(T));
            handle.Free();
            return value;
        }

        private Block ReadBlock(uint index)
        {
            _file.Seek(_header.BlockTablePos + index * Marshal.SizeOf(typeof(Block)), SeekOrigin.Begin);
            return ReadStruct<Block>(_file);
        }
    }
}
