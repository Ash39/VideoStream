using Newtonsoft.Json;
using System.Text.Json.Serialization;

namespace VideoStream.Data.Medias
{
    public class MediaSegment
    {
        public int Offset { get; set; }
        public int Size { get; set; }
        public float Timestamp { get; set; }
    }
}
