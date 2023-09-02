using FFMpegCore.Enums;
using FFMpegCore;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.FileProviders;
using VideoStream.Data;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace VideoStream.Pages
{
    public class IndexModel : PageModel
    {
        private readonly ILogger<IndexModel> _logger;
        private readonly ApplicationDbContext context;

        public List<VideoInfo> VideoInfos { get; set; } = new List<VideoInfo>();

        public IndexModel(ILogger<IndexModel> logger, IDbContextFactory<ApplicationDbContext> contextFactory)
        {
            _logger = logger;
            this.context = contextFactory.CreateDbContext();
        }

        public void OnGet()
        {
            VideoInfos = context.VideoInfo.OrderBy( v=> v.Name).ToList();
        }

    }
}