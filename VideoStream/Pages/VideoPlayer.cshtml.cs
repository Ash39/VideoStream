using FFMpegCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VideoStream.Data;

namespace VideoStream.Pages
{
    public class VideoPlayerModel : PageModel
    {
        ApplicationDbContext context;

        public VideoPlayerModel(IDbContextFactory<ApplicationDbContext> contextFactory)
        {
            this.context = contextFactory.CreateDbContext();
        }

        public VideoInfo? VideoInfo { get; set; }

        public void OnGet(Guid id)
        {
            VideoInfo = context.VideoInfo.Find(id);
            ViewData["Title"] = VideoInfo.Name;
        }
    }
}
