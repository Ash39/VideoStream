using Microsoft.CodeAnalysis.Elfie.Extensions;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using NuGet.ContentModel;
using System.Buffers.Binary;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace VideoStream.Data.Medias
{
    public class Matroska : IMediaInformation
    {
        private StreamByteReader reader;

        private List<MatroskaCluster> clusters;
        private List<List<MediaSegment>> mediaSegments;

        public Matroska(Stream stream) 
        { 
            reader = new StreamByteReader(stream);

            (int elementId, int size) = ReadElementHeader(reader);

            Debug.Assert(elementId == 0x1A45DFA3);
            reader.Skip(size);

            (int segmentId, _) = ReadElementHeader(reader);
            Debug.Assert(segmentId == 0x18538067);

            ByteReader segmentReader = reader.SegmentReader((int)reader.position, null);

            MatroskaElement segmentInfo = Find(segmentReader, 0x1549A966);
            MatroskaElement? timestampScale = Find(segmentInfo.reader, 0x2AD7B1);

            uint timeStampScaleValue = timestampScale != null ? timestampScale.reader.ReadUInt() : 1000000;

            clusters = new List<MatroskaCluster>();

            foreach (MatroskaElement element in Iterate(segmentReader))
            {
                if (element.elementId == 0x1F43B675)
                {
                    clusters.Add(new MatroskaCluster(element, timeStampScaleValue));
                }
            }

            this.mediaSegments = new List<List<MediaSegment>>();

            List<MediaSegment> mediaSegments = new List<MediaSegment>();

            foreach (MatroskaCluster cluster in clusters)
            {
                mediaSegments.Add(new MediaSegment() { Offset = cluster.element.offset, Size = cluster.element.fullSize, Timestamp = cluster.timeStamp });
                
            }

            mediaSegments[0].Size += mediaSegments[0].Offset;
            mediaSegments[0].Offset = 0;

            this.mediaSegments.Add(mediaSegments);
        }

        public List<List<MediaSegment>> MediaSegments
        {
            get 
            {
                return mediaSegments;
            }
        }

        internal class MatroskaElement
        {
            internal readonly int elementId;
            internal readonly int offset;
            internal readonly int fullSize;
            internal readonly ByteReader reader;

            public MatroskaElement(int elementId, int headerOffset, int headerSize, int contentSize, ByteReader reader)
            {
                this.elementId = elementId;
                this.offset = headerOffset;
                this.fullSize = headerSize + contentSize;
                this.reader = reader.SegmentReader(headerOffset + headerSize, contentSize);
            }
        }

        internal class MatroskaCluster
        {
            internal readonly MatroskaElement element;
            internal readonly float timeStamp;

            public MatroskaCluster(MatroskaElement element, uint timeStampScaleValue)
            {
                Debug.Assert(element.elementId == 0x1F43B675);

                this.element = element;

                this.timeStamp = (Find(this.element.reader, 0xE7).reader.ReadUInt() / 2f) / ((1_000_000_000 / timeStampScaleValue) / 2f);
            }
        }

        private static (int elementId, int size) ReadElementHeader(ByteReader reader) 
        {
            int elementId = reader.ReadVint(true);
            long size = reader.ReadVint();

            return (elementId, (int)size);
        }

        private static MatroskaElement Read(ByteReader reader) 
        {
            int headerOffset = (int)reader.position;
            (int elementId, int contentSize) = ReadElementHeader(reader);
            int headerSize = (int)reader.position - headerOffset;
            return new MatroskaElement(elementId, headerOffset, headerSize, contentSize, reader);
        }
        private static MatroskaElement Find(ByteReader reader, int elementId) 
        {
            foreach (MatroskaElement element in Iterate(reader))
            {
                if (element.elementId == elementId)
                {
                    return element;
                }
            }

            return null;
        }

        private static IEnumerable<MatroskaElement> Iterate(ByteReader reader)
        {
            while (!reader.Ended)
            {
                MatroskaElement element = Read(reader);

                yield return element;

                reader.Skip(element.reader.size);
            }
        }
    }

}
