using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace VideoStream.Pages
{
    public class VideoPlayerModel : PageModel
    {
        public string File { get; set; }

        public void OnGet(string file)
        {
            File = file;
        }
    }
}
