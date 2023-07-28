using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.FileProviders;
using VideoStream.Data;

namespace VideoStream.Pages
{
    public class IndexModel : PageModel
    {
        private readonly ILogger<IndexModel> _logger;
        private readonly IWebHostEnvironment env;
        private readonly VideoPaths paths;

        public List<VideoInfo> VideoInfos { get; set; } = new List<VideoInfo>();

        public IndexModel(ILogger<IndexModel> logger, IWebHostEnvironment env, IConfiguration configuration)
        {
            _logger = logger;
            this.env = env;
            this.paths = new VideoPaths();
            configuration.GetSection(VideoPaths.Config).Bind(paths);
        }

        public void OnGet()
        {
            List<string> paths = new List<string>();

            paths.AddRange(this.paths.Paths);
            paths.Add(Path.Combine(env.WebRootPath, "video"));

            foreach(string path in paths) 
            {
                CreateVideoInfos(path);
            }
        }

        private void CreateVideoInfos(string path) 
        {
            string[] files = Directory.GetFiles(path, "", SearchOption.AllDirectories);

            for (int i = 0; i < files.Length; i++)
            {
                VideoInfos.Add(CreateVideoInfo(files[i]));
            }
        }

        private VideoInfo CreateVideoInfo(string file) 
        {
            string fileName = Path.GetFileName(file);

            VideoInfo info = new VideoInfo();
            info.Name = fileName.Remove(fileName.Length - Path.GetExtension(fileName).Length);
            info.FullName = fileName;
            info.Thumbnail = "img/video.jpg";

            if (!VideoList.Contains(fileName))
            {
                VideoList.Add(file);
            }

            return info;
        } 
    }
}