using FFMpegCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.Extensions.FileProviders;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using VideoStream.Data;
using VideoStream.Data.Medias;

namespace VideoStream.Controllers
{
    [Controller]
    [Route("api/[controller]")]
    public class VideoController : Controller
    {
        private readonly ApplicationDbContext context;

        public VideoController(IDbContextFactory<ApplicationDbContext> contextFactory)
        {
            this.context = contextFactory.CreateDbContext();
        }

        [HttpGet]
        public IActionResult Stream(Guid id)
        {
            VideoInfo? info = context.VideoInfo.Find(id);
            if (info != null)
            {
                context.VideoInfo.Entry(info).Reference(b => b.Path).Load();

                if (info.Path != null)
                {
                    if (info.Path.Path != null)
                    {
                        return new VideoContent(info.Path.Path);
                    }
                }
            }

            return NotFound();
        }
    }
}
