namespace VideoStream.Data
{
    public class VideoPath
    {
        public Guid Id { get; set; }
        public Guid VideoInfoId { get; set; }
        public string? Path { get; set; }
        public string? SHA { get; set; }
    }
}
