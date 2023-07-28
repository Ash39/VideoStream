using System.Net.Mail;
using System.Net;
using Microsoft.Extensions.FileProviders;

namespace VideoStream.Data
{
    public class VideoData
    {
        private string filename;

        public VideoData(string filename)
        {
            this.filename = filename;
        }

        public async Task WriteToStream(Stream outputStream, CancellationToken cancellation)
        {
            try
            {
                var buffer = new byte[4096];

                using (Stream video = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    var length = (int)video.Length;
                    var bytesRead = 1;

                    while (length > 0 && bytesRead > 0)
                    {
                        bytesRead = await video.ReadAsync(buffer, 0, Math.Min(length, buffer.Length));
                        await outputStream.WriteAsync(buffer, 0, bytesRead);
                        length -= bytesRead;

                        cancellation.ThrowIfCancellationRequested();
                    }
                }
            }
            catch (HttpProtocolException ex) 
            {
                return;
            }
            catch (OperationCanceledException)
            {
                return;
            }
            finally
            {
                outputStream.Close();
            }
        }
    }
}
