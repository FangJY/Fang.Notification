using Fang.Notification.Core.Abstractions;
using Fang.Notification.Core.Models;

namespace Fang.Notification.WeCom
{
    public class WeComMessageBuilder : IMessageBuilder
    {
        public object BuildMessage(NotificationMessage message)
        {
            switch (message)
            {
                case TextMessage text:
                    return new { msgtype = "text", text = new { content = text.Content } };
                case MarkdownMessage markdown:
                    return new { msgtype = "markdown", markdown = new { content = markdown.Content } };
                default:
                    throw new System.NotSupportedException($"企业微信不支持消息类型: {message.Type}");
            }
        }
    }
}
