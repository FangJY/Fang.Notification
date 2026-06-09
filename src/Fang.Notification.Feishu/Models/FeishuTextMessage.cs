namespace Fang.Notification.Feishu.Models
{
    public class FeishuWebhookTextContent
    {
        public string text { get; set; }
    }

    public class FeishuWebhookRequest
    {
        public string msg_type { get; set; }
        public FeishuWebhookTextContent content { get; set; }
    }

    public class FeishuApiMessageContent
    {
        public string text { get; set; }
    }

    public class FeishuApiMessageRequest
    {
        public string receive_id { get; set; }
        public string msg_type { get; set; }
        public string content { get; set; }
    }
}
