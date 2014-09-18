using System;
using System.IO;

namespace Primevil
{
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
        private readonly IDisposable handle;

        internal MPQStream(MPQArchive archive, IDisposable handle)
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



        private bool disposed;

        protected override void Dispose(bool disposing)
        {
            if (disposed)
                return;
            if (disposing)
                handle.Dispose();
            disposed = true;
        }
    }
}