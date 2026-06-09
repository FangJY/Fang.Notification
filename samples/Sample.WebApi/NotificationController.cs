using System.Threading.Tasks;
using Fang.Notification.Core.Models;
using Fang.Notification.Facade;
using Microsoft.AspNetCore.Mvc;

namespace Sample.WebApi
{
    [ApiController]
    [Route("api/[controller]")]
    public class NotificationController : ControllerBase
    {
        private readonly NotificationService _notificationService;

        public NotificationController(NotificationService notificationService)
        {
            _notificationService = notificationService;
        }

        [HttpPost("send")]
        public async Task<IActionResult> Send([FromBody] SendRequest request)
        {
            var message = new TextMessage
            {
                Title = request.Title,
                Content = request.Content
            };

            var receiver = MessageReceiver.FromUserId(request.UserId);
            var result = await _notificationService.SendAsync(request.Channel, message, receiver);

            return Ok(result);
        }
    }

    public class SendRequest
    {
        public string Channel { get; set; }
        public string Title { get; set; }
        public string Content { get; set; }
        public string UserId { get; set; }
    }
}
