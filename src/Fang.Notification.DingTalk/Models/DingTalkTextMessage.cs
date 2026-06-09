namespace Fang.Notification.DingTalk.Models
{
    public class DingTalkTextMessage
    {
        public string content { get; set; }
    }

    public class DingTalkMarkdownMessage
    {
        public string title { get; set; }
        public string text { get; set; }
    }

    public class DingTalkAtInfo
    {
        public string[] atMobiles { get; set; }
        public string[] atUserIds { get; set; }
        public bool isAtAll { get; set; }
    }

    public class DingTalkWebhookRequest
    {
        public string msgtype { get; set; }
        public DingTalkTextMessage text { get; set; }
        public DingTalkMarkdownMessage markdown { get; set; }
        public DingTalkAtInfo at { get; set; }
    }
}
