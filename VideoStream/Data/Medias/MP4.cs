using NuGet.ContentModel;
using NuGet.DependencyResolver;
using System.Diagnostics;
using System.Text;

namespace VideoStream.Data.Medias
{
    public class MP4 : IMediaInformation
    {
        ByteReader reader;

        List<List<MediaSegment>> trackSegments;

        public MP4(Stream stream) 
        { 
            reader = stream != null ? new StreamByteReader(stream) : null;

            MovieBox moov = new MovieBox(Find(reader, Encoding.ASCII.GetBytes("moov")));
            List<MovieFragmentBox> moofs = FindList(reader, Encoding.ASCII.GetBytes("moof")).Select(t => new MovieFragmentBox(t)).ToList();

            int FragmentSize(int fragmentIndex, int track) 
            {
                if (fragmentIndex < moofs.Count - 1)
                {
                    return moofs[fragmentIndex + 1].trafs[track].box.offset - moofs[fragmentIndex].trafs[track].box.offset;
                }
                else
                {
                    int movieSize = reader.size;
                    return movieSize - moofs[fragmentIndex].trafs[track].box.offset;
                }
            }

            trackSegments = new List<List<MediaSegment>>();


            for (int i = 0; i < moov.tracks.Count; i++)
            {
                int index = 0;
                List<MediaSegment> segments = new List<MediaSegment>();
                foreach (MovieFragmentBox moof in moofs)
                {
                    segments.Add(new MediaSegment() {Offset = moof.trafs[i].box.offset, Size = FragmentSize(index++, i), Timestamp =  MoofStartTime(moov, moof, i)});
                }

                segments[0].Size += segments[0].Offset;
                segments[0].Offset = 0;

                trackSegments.Add(segments);
            }
        }

        private float MoofStartTime(MovieBox moov, MovieFragmentBox moof, int track)
        {
            TrackFragmentBox traf = moof.trafs[track];
            int trackTimescale = moov.tracks[track].mdia.mdhd.timescale;
            return (traf.tfdt.baseMediaDecodeTime + traf.truns[0].firstSampleCompositionTimeOffset) / (trackTimescale + moov.PresentaionOffset(track));
        }

        public List<List<MediaSegment>> MediaSegments
        {
            get
            {
                return trackSegments;
            }
        }

        private static IEnumerable<MP4Box> Iterate(ByteReader reader, bool rewind = false)
        {
            if (rewind)
            {
                reader.position = reader.start;
            }

            while (!reader.Ended)
            {
                yield return reader != null ? new MP4Box(reader) : null;
            }
        }

        private static MP4Box Find(ByteReader reader, byte[] kind, bool rewind = false, bool required = false)
        {
            foreach (MP4Box box in Iterate(reader, rewind))
            {
                if (box.kind.SequenceEqual(kind))
                {
                    return box;
                }
            }

            if (required)
            {
                throw new KeyNotFoundException($"Could not find {Encoding.ASCII.GetString(kind)} box");
            }

            return null;
        }

        private static List<MP4Box> FindList(ByteReader reader, byte[] kind, bool rewind = false)
        {
            List<MP4Box> boxes = new List<MP4Box>();
            foreach (MP4Box box in Iterate(reader, rewind))
            {
                if (box.kind.SequenceEqual(kind))
                {
                    boxes.Add(box);
                }
            }

            return boxes;
        }

        internal class MP4Box 
        {
            public int offset;
            public byte[] kind;
            public int fullSize;
            public ByteReader reader;

            public MP4Box(ByteReader reader)
            {
                this.offset = reader.position;

                int size = (int)ByteReaderExtension.ReadIntBigEndian(reader.Read(4));
                byte[] kind = reader.Read(4);

                if (size == 1)
                {
                    size = (int)ByteReaderExtension.ReadIntBigEndian(reader.Read(8));
                }
                else if (size == 0)
                {
                    size = reader.End - reader.position;
                }

                if (kind.SequenceEqual(Encoding.ASCII.GetBytes("uuid")))
                {
                    kind = kind.Concat(reader.Read(16)).ToArray();
                }
                int contentOffset = reader.position;
                int contentSize = size - (contentOffset - this.offset);

                this.kind = kind;
                this.fullSize = size;
                this.reader = reader.SegmentReader(contentOffset, contentSize);
                reader.Skip(contentSize);
            }
        }

        internal class FullBox 
        {
            public MP4Box box;
            public int version;
            public int flags;
            public ByteReader reader;

            public FullBox(MP4Box box)
            {
                this.box = box;
                this.version = (int)ByteReaderExtension.ReadIntBigEndian(box.reader.Read(1));
                this.flags = (int)ByteReaderExtension.ReadIntBigEndian(box.reader.Read(3));
                this.reader = box.reader.SegmentReader(box.reader.position, box.reader.size - 4);
            }
        }

        

        internal class MovieBox
        {
            public MP4Box box;
            public MovieHeaderBox movieHeader;
            public List<TrackBox> tracks;

            public MovieBox(MP4Box box)
            {
                this.box = box;
                MP4Box movieHeaderBox = Iterate(box.reader, true).FirstOrDefault(box => box.kind.SequenceEqual(Encoding.ASCII.GetBytes("mvhd")));
                movieHeader = movieHeaderBox != null ? new MovieHeaderBox(movieHeaderBox) : null;
                tracks = FindList(box.reader, Encoding.ASCII.GetBytes("trak"), true).Select(t => new TrackBox(t)).ToList();
            }

            public float PresentaionOffset(int track) 
            { 
                EditListBox elst = tracks[track].elst;

                if (elst == null)
                {
                    return 0f / 1f;
                }

                int movieTimeScale = movieHeader.timeScale;
                int trackTimeScale = tracks[track].mdia.mdhd.timescale;
                float offset = 0f / 1f;

                int i = 0;
                foreach (var edit in elst.edits)
                {
                    if (edit.mediaTime == -1)
                    {
                        offset += (float)edit.segmentDuration / (float)movieTimeScale;
                    }
                    else
                    {
                        offset -= (float)edit.mediaTime / (float)trackTimeScale;
                        Debug.Assert(i == elst.edits.Count - 1);
                    }
                    i++;
                }

                return offset;
            }
        }

        internal class MovieHeaderBox
        {
            public MP4Box box;
            public FullBox fullBox;
            public int timeScale;

            public MovieHeaderBox(MP4Box box)
            {
                this.box = box;
                this.fullBox = box != null ? new FullBox(box) : null;
                int numberSize = this.fullBox.version == 1 ? 8 : 4;

                this.fullBox.reader.Skip(2 * numberSize);
                this.timeScale = (int)ByteReaderExtension.ReadIntBigEndian(this.fullBox.reader.Read(4));
            }
        }

        internal class TrackBox
        {
            public MP4Box box;
            public TrackHeaderBox tkhd;
            public MediaBox mdia;
            public EditBox edts;
            public EditListBox elst;

            public TrackBox(MP4Box box)
            {
                this.box = box;
                MP4Box tkhdBox = Iterate(box.reader, true).FirstOrDefault(box => box.kind.SequenceEqual(Encoding.ASCII.GetBytes("tkhd")));
                tkhd = tkhdBox != null ? new TrackHeaderBox(tkhdBox) : null;
                MP4Box mdiaBox = Iterate(box.reader, true).FirstOrDefault(box => box.kind.SequenceEqual(Encoding.ASCII.GetBytes("mdia")));
                mdia = mdiaBox != null ? new MediaBox(mdiaBox) : null;
                MP4Box edtsBox = Iterate(box.reader, true).FirstOrDefault(box => box.kind.SequenceEqual(Encoding.ASCII.GetBytes("edts")));
                edts = edtsBox != null ? new EditBox(edtsBox) : null;
                elst = edts != null ? edts.elst : null;
            }
        }

        internal class TrackHeaderBox
        {
            public MP4Box box;
            public FullBox fullBox;
            public int trackID;

            public TrackHeaderBox(MP4Box box)
            {
                this.box = box;
                this.fullBox = box != null ? new FullBox(box) : null;

                int intSize = fullBox.version == 1 ? 8 : 4;

                fullBox.reader.Skip(2 * intSize);

                trackID = (int)ByteReaderExtension.ReadIntBigEndian(fullBox.reader.Read(4));
            }
        }

        internal class MediaBox
        {
            public MP4Box box;
            public MediaHeaderBox mdhd;

            public MediaBox(MP4Box box)
            {
                this.box = box;
                MP4Box mdhdBox = Iterate(box.reader).FirstOrDefault(box => box.kind.SequenceEqual(Encoding.ASCII.GetBytes("mdhd")));
                mdhd = mdhdBox != null ? new MediaHeaderBox(mdhdBox) : null;
            }
        }

        internal class MediaHeaderBox
        {
            public MP4Box box;
            public FullBox fullBox;
            public int timescale;

            public MediaHeaderBox(MP4Box box)
            {
                this.box = box;
                this.fullBox = box != null ? new FullBox(box) : null;

                int intSize = fullBox.version == 1 ? 8 : 4;
                fullBox.reader.Skip(2 * intSize);
                timescale = (int)ByteReaderExtension.ReadIntBigEndian(fullBox.reader.Read(4));
            }
        }

        internal class EditBox
        {
            public MP4Box box;
            public EditListBox elst;

            public EditBox(MP4Box box)
            {
                this.box = box;
                MP4Box elstBox = Iterate(box.reader).FirstOrDefault(box => box.kind.SequenceEqual(Encoding.ASCII.GetBytes("elst")));
                elst = elstBox != null ? new EditListBox(elstBox) : null;
            }
        }

        internal class EditListBox
        {
            public MP4Box box;
            public FullBox fullBox;
            public List<(int segmentDuration, uint mediaTime)> edits;

            public EditListBox(MP4Box box)
            {
                this.box = box;
                this.fullBox = box != null ? new FullBox(box) : null;

                int intSize = fullBox.version == 1 ? 8 : 4;

                int entryCount = (int)ByteReaderExtension.ReadIntBigEndian(fullBox.reader.Read(4));

                edits = new List<(int, uint)>();

                (int segmentDuration, uint mediaTime) ReadEdit() 
                {
                    int segmentDuration = (int)ByteReaderExtension.ReadIntBigEndian(fullBox.reader.Read(intSize)); 
                    uint mediaTime = (uint)ByteReaderExtension.ReadSignedIntBigEndian(fullBox.reader.Read(intSize));
                    fullBox.reader.Skip(4);

                    return (segmentDuration, mediaTime);
                }

                for (int i = 0; i < entryCount; i++)
                {
                    edits.Add(ReadEdit());
                }
            }
        }

        internal class MovieFragmentBox
        {
            public MP4Box box;
            public List<TrackFragmentBox> trafs;

            public MovieFragmentBox(MP4Box box)
            {
                this.box = box;
                this.trafs = FindList(box.reader, Encoding.ASCII.GetBytes("traf")).Select(t => new TrackFragmentBox(t)).ToList();

                Debug.Assert(trafs.Count > 0);
            }
        }
        
        internal class TrackFragmentBox
        {
            public MP4Box box;
            public FullBox fullBox;
            public TrackFragmentHeaderBox tfhd;
            public TrackFragmentBaseMediaDecodeTimeBox tfdt;
            public List<TrackRunBox> truns;

            public TrackFragmentBox(MP4Box box)
            {
                this.box = box;
                this.fullBox = box != null ? new FullBox(box) : null;
                MP4Box tfhdBox = Iterate(box.reader, true).FirstOrDefault(box => box.kind.SequenceEqual(Encoding.ASCII.GetBytes("tfhd")));
                this.tfhd = tfhdBox != null ? new TrackFragmentHeaderBox(tfhdBox) : null;
                MP4Box tfdtBox = Iterate(box.reader, true).FirstOrDefault(box => box.kind.SequenceEqual(Encoding.ASCII.GetBytes("tfdt")));
                this.tfdt = tfdtBox != null ? new TrackFragmentBaseMediaDecodeTimeBox(tfdtBox) : null;
                this.truns = FindList(box.reader, Encoding.ASCII.GetBytes("trun"), true).Select(t => new TrackRunBox(t)).ToList();
            }
        }
        
        internal class TrackFragmentHeaderBox
        {
            public MP4Box box;
            public FullBox fullBox;
            public TFFlags flags;

            public TrackFragmentHeaderBox(MP4Box box)
            {
                this.box = box;
                this.fullBox = box != null ? new FullBox(box) : null;
                this.flags = (TFFlags)fullBox.flags;
            }
        }

        internal class TrackFragmentBaseMediaDecodeTimeBox
        {
            public MP4Box box;
            public FullBox fullBox;
            public int baseMediaDecodeTime;

            public TrackFragmentBaseMediaDecodeTimeBox(MP4Box box)
            {
                this.box = box;
                this.fullBox = box != null ? new FullBox(box) : null;

                int intSize = fullBox.version == 1 ? 8 : 4;

                this.baseMediaDecodeTime = (int)ByteReaderExtension.ReadIntBigEndian(fullBox.reader.Read(intSize));
            }

           
        }

        internal class TrackRunBox
        { 
            public MP4Box box;
            public FullBox fullBox;
            public int sampleCount;
            public uint firstSampleCompositionTimeOffset;

            public TrackRunBox(MP4Box box)
            {
                this.box = box;
                this.fullBox = box != null ? new FullBox(box) : null;

                TRFlags tRFlags = (TRFlags)fullBox.flags;

                this.sampleCount = (int)ByteReaderExtension.ReadIntBigEndian(fullBox.reader.Read(4));

                if ((tRFlags & TRFlags.DATA_OFFSET_PRESENT) != 0)
                    fullBox.reader.Skip(4);
                if ((tRFlags & TRFlags.FIRST_SAMPLE_FLAGS_PRESENT) != 0)
                    fullBox.reader.Skip(4);
                if ((tRFlags & TRFlags.FIRST_SAMPLE_FLAGS_PRESENT) != 0)
                    fullBox.reader.Skip(4);

                Debug.Assert(sampleCount > 0);
                // First sample
                if ((tRFlags & TRFlags.SAMPLE_DURATION_PRESENT)!= 0)
                    fullBox.reader.Skip(4);
                if ((tRFlags & TRFlags.SAMPLE_SIZE_PRESENT)!= 0)
                    fullBox.reader.Skip(4);
                if ((tRFlags & TRFlags.SAMPLE_FLAGS_PRESENT)!= 0)
                    fullBox.reader.Skip(4);
                if ((tRFlags & TRFlags.SAMPLE_COMPOSITION_TIME_OFFSETS_PRESENT) != 0)
                    firstSampleCompositionTimeOffset = (uint)ByteReaderExtension.ReadSignedIntBigEndian(fullBox.reader.Read(4));
                else
                    firstSampleCompositionTimeOffset = 0;
            }
        }

        public enum TFFlags 
        {
            BASE_DATA_OFFSET_PRESENT = 0x1,
            SAMPLE_DESCRIPTION_INDEX_PRESENT = 0x2,
            DEFAULT_SAMPLE_DURATION_PRESENT = 0x8,
            DEFAULT_SAMPLE_SIZE_PRESENT = 0x10,
            DEFAULT_SAMPLE_FLAGS_PRESENT = 0x20,
            DURATION_IS_EMPTY = 0x10000,
            DEFAULT_BASE_IS_MOOF = 0x20000,
        }

        public enum TRFlags
        {
            DATA_OFFSET_PRESENT = 0x1,
            FIRST_SAMPLE_FLAGS_PRESENT = 0x4,
            SAMPLE_DURATION_PRESENT = 0x100,
            SAMPLE_SIZE_PRESENT = 0x200,
            SAMPLE_FLAGS_PRESENT = 0x400,
            SAMPLE_COMPOSITION_TIME_OFFSETS_PRESENT = 0x800,
        }
    }
}
