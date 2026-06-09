namespace Fang.Notification.WeCom.Models
{
    public class WeComTextMessage
    {
        public string content { get; set; }
    }

    public class WeComMarkdownMessage
    {
        public string content { get; set; }
    }

    public class WeComNewsArticle
    {
        public string title { get; set; }
        public string description { get; set; }
        public string url { get; set; }
        public string picurl { get; set; }
    }

    public class WeComNewsMessage
    {
        public WeComNewsArticle[] articles { get; set; }
    }

    public class WeComImageMessage
    {
        public string base64 { get; set; }
        public string md5 { get; set; }
    }

    public class WeComWebhookRequest
    {
        public string msgtype { get; set; }
        public WeComTextMessage text { get; set; }
        public WeComMarkdownMessage markdown { get; set; }
        public WeComNewsMessage news { get; set; }
        public WeComImageMessage image { get; set; }
    }
}
