using System.Buffers.Binary;
using System.Diagnostics;

namespace VideoStream.Data.Medias
{
    public abstract class ByteReader
    {
        public readonly int start;
        public readonly int size;
        public int position;

        protected ByteReader(int start, int size)
        {
            this.start = start;
            this.size = size;
            this.position = start;
        }

        public int End => start + size;
        public bool Ended => position >= End;

        public byte[] ReadAt(int position, int count)
        {
            Debug.Assert(count >= 0);

            if (!(start <= position && position <= End))
            {
                throw new Exception($"Illegal read, attempted to read [{position}, {position + count}) in [{start}, {End})");
            }

            count = Math.Min(count, End - position);

            return InternalReadAt(position, count);
        }

        protected abstract byte[] InternalReadAt(int position, int count);
        public virtual void Close()
        {
            throw new NotImplementedException();
        }

        public byte[] Read(int? count = null)
        {
            if (count == null)
            {
                count = End - position;
            }

            byte[] res = ReadAt(position, (int)count);

            if (res != null)
            {
                position += res.Length;
            }

            return res;
        }

        public byte[] Peek(int count)
        {
            return InternalReadAt(position, count);
        }

        public void Skip(int count)
        {
            position = Math.Min(position + count, End);
        }
    }

    internal class RegionByteReader : ByteReader
    {
        private readonly ByteReader parent;


        public RegionByteReader(ByteReader reader, int offset, int? size = null) : base(offset, (int)(size = size == null ? reader.End - offset : size))
        {
            Debug.Assert(offset >= 0);
            Debug.Assert(size >= 0);
            Debug.Assert(offset + size <= (int)reader.End);
            this.parent = reader;
        }

        protected override byte[] InternalReadAt(int position, int count)
        {
            count = Math.Min(count, End - position);
            return parent.ReadAt(position, count);
        }

    }

    internal class StreamByteReader : ByteReader
    {
        private readonly Stream stream;

        public StreamByteReader(Stream stream) : base(0, (int)stream.Length)
        {
            this.stream = stream;
            this.stream.Seek(0, SeekOrigin.End);
        }

        protected override byte[] InternalReadAt(int position, int count)
        {
            this.stream.Seek(position, SeekOrigin.Begin);
            byte[] bytes = new byte[count];

            stream.Read(bytes);

            return bytes;
        }

    }

    internal static class ByteReaderExtension
    {
        internal static ByteReader SegmentReader(this ByteReader reader, int offset, int? size)
        {
            return new RegionByteReader(reader, offset, size);
        }

        internal static int ReadVint(this ByteReader reader, bool raw = false)
        {
            int headerByte = reader.Read(1)[0];
            if (headerByte == 0)
                throw new InvalidCastException("VINT with zero header byte");
            int tailLength = 0;
            int mask = 0x80;
            while ((headerByte & mask) == 0)
            {
                tailLength += 1;
                mask >>= 1;
            }

            int headerNumberPart = raw ? headerByte : headerByte & ~mask;

            byte[] bytes = new byte[] { (byte)headerNumberPart }.Concat(reader.Read(tailLength)).ToArray();
            return (int)ReadIntBigEndian(bytes);
        }

        internal static uint ReadUInt(this ByteReader reader, uint defaultValue = 0)
        {
            if (reader.Ended)
            {
                return defaultValue;
            }
            return (uint)ReadIntBigEndian(reader.Read());
        }

        public static long ReadIntBigEndian(byte[] bytes)
        {
            long ret = 0;
            foreach (byte _byte in bytes)
                ret = (ret << 8) | _byte;
            return ret;
        }
        public static long ReadSignedIntBigEndian(byte[] bytes)
        {
            switch (bytes.Length)
            {
                case 1:
                    return (byte)bytes[0];
                case 2:
                    return BinaryPrimitives.ReadInt16BigEndian(bytes);
                case 4:
                    return BinaryPrimitives.ReadInt32BigEndian(bytes);
                case 8:
                    return BinaryPrimitives.ReadInt64BigEndian(bytes);
                default:
                    return 0;
            }
        }
    }
}
