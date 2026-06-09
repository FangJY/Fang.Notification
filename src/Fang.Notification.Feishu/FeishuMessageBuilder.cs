using Fang.Notification.Core.Abstractions;
using Fang.Notification.Core.Models;
using Newtonsoft.Json;

namespace Fang.Notification.Feishu
{
    public class FeishuMessageBuilder : IMessageBuilder
    {
        public object BuildMessage(NotificationMessage message)
        {
            return message switch
            {
                TextMessage text => new
                {
                    msg_type = "text",
                    content = new { text = text.Content }
                },
                CardMessage card => new
                {
                    msg_type = "interactive",
                    card = JsonConvert.DeserializeObject(card.TemplateData)
                },
                _ => throw new System.NotSupportedException($"飞书Webhook不支持消息类型: {message.Type}")
            };
        }
    }
}
