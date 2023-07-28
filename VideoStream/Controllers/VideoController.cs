using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;
using VideoStream.Data;

namespace VideoStream.Controllers
{
    [Controller]
    [Route("api/[controller]")]
    public class VideoController : Controller
    {
        readonly IWebHostEnvironment env;

        public VideoController(IWebHostEnvironment env)
        {
            this.env = env;
        }

        [HttpGet]
        public IActionResult Stream(string filename)
        {
            VideoData video = new VideoData(VideoList.Get(filename));

            return new PushStreamContent(video.WriteToStream, "video/" + Path.GetExtension(filename));
        }

    }
}
