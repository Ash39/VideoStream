using Microsoft.AspNetCore.Mvc;

namespace VideoStream.Controllers
{
    public class PushStreamContent : IActionResult
    {
        Func<Stream, CancellationToken, Task> _pushAction;
        string _contentType;

        public PushStreamContent(Func<Stream, CancellationToken, Task> pushAction, string contentType)
        {
            _pushAction = pushAction;
            _contentType = contentType;
        }

        public Task ExecuteResultAsync(ActionContext context)
        {
            var response = context.HttpContext.Response;
            response.ContentType = _contentType;
            response.StatusCode = 200;

            return _pushAction(response.Body, context.HttpContext.RequestAborted);
        }
    }
}
