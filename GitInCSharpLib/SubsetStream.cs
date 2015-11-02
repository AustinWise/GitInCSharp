using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Austin.GitInCSharpLib
{
    class SubsetStream : Stream
    {
        readonly Stream mBaseStream;
        readonly long mLength;
        long mBytesRead;

        public SubsetStream(Stream s, long length)
        {
            if (s == null)
                throw new ArgumentNullException(nameof(s));
            if (length < 0)
                throw new ArgumentOutOfRangeException(nameof(length));

            mBaseStream = s;
            mLength = length;
            mBytesRead = 0;
        }

        public override bool CanRead
        {
            get
            {
                return true;
            }
        }

        public override bool CanSeek
        {
            get
            {
                return false;
            }
        }

        public override bool CanWrite
        {
            get
            {
                return false;
            }
        }

        public override long Length
        {
            get
            {
                return mLength;
            }
        }

        public override long Position
        {
            get
            {
                throw new NotSupportedException();
            }

            set
            {
                throw new NotSupportedException();
            }
        }

        public override void Flush()
        {
            throw new NotSupportedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (count <= 0)
                throw new ArgumentOutOfRangeException(nameof(count));

            if (mBytesRead == mLength)
                return -1;

            long bytesToRead = Math.Min(count, mLength - mBytesRead);
            mBytesRead += bytesToRead;

            return mBaseStream.Read(buffer, offset, (int)bytesToRead);
        }
    }
}
