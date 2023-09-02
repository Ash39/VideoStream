namespace VideoStream.Data
{
    public class VideoInfo
    {
        public Guid Id { get; set; }
        public string? Name { get; set; }
        public string? FullName { get; set; }
        public string? Thumbnail { get; set; }
        public string? MediaType { get; set; }
        public VideoPath? Path { get; set; }
    }
}
