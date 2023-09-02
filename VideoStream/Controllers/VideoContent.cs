using Azure.Core;
using Azure;
using Microsoft.AspNetCore.Mvc;
using System.Net.Mime;

namespace VideoStream.Controllers
{
    public class VideoContent : IActionResult
    {
        public string File { get; }

        public VideoContent(string file)
        {
            File = file;
        }

        public Task ExecuteResultAsync(ActionContext context)
        {
            var Request = context.HttpContext.Request;
            var Response = context.HttpContext.Response;
            
            return Stream(Request, Response, context.HttpContext.RequestAborted);
        }

        private async Task Stream(HttpRequest request, HttpResponse response, CancellationToken cancellationToken) 
        {
            using (Stream video = new FileStream(File, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                long bytesToRead = video.Length;
                long bytesRead = 0;

                response.Headers["Accept-Ranges"] = "bytes";
                response.ContentType = "application/octet-stream";

                byte[] buffer = new byte[4096];

                if (!string.IsNullOrEmpty(request.Headers["Range"]))
                {
                    string[] range = request.Headers["Range"].ToString().Split(new char[] { '=', '-' });
                    bytesRead = long.Parse(range[1]);
                    video.Seek(bytesRead, SeekOrigin.Begin);

                    response.StatusCode = 206;
                    response.Headers["Content-Range"] = $"bytes {bytesRead}-{bytesToRead - 1}/{bytesToRead}";
                }
                Stream outputStream = response.Body;

                while (bytesToRead > 0)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    await video.ReadAsync(buffer, 0, (int)Math.Min(bytesToRead, buffer.LongLength));

                    await outputStream.WriteAsync(buffer);
                    bytesToRead -= buffer.Length;
                }
            }
        }


    }
}
