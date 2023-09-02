using VideoStream.Data.Medias;

namespace VideoStream.Data
{
    public class MediaOffset
    {
        public Guid Id { get; set; }
        public string Segment { get; set; }
        public int previous { get; set; }
    }
}
