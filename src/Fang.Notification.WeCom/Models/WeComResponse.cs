namespace Fang.Notification.WeCom.Models
{
    public class WeComResponse
    {
        public int errcode { get; set; }
        public string errmsg { get; set; }
    }

    public class WeComTokenResponse
    {
        public int errcode { get; set; }
        public string errmsg { get; set; }
        public string access_token { get; set; }
        public int expires_in { get; set; }
    }

    public class WeComImageContent
    {
        public string media_id { get; set; }
    }

    public class WeComFileContent
    {
        public string media_id { get; set; }
    }

    public class WeComMessageRequest
    {
        public string touser { get; set; }
        public string toparty { get; set; }
        public string totag { get; set; }
        public string msgtype { get; set; }
        public int agentid { get; set; }
        public WeComTextMessage text { get; set; }
        public WeComMarkdownMessage markdown { get; set; }
        public WeComNewsMessage news { get; set; }
        public WeComImageContent image { get; set; }
        public WeComFileContent file { get; set; }
        public int safe { get; set; }
    }

    public class WeComMediaUploadResponse
    {
        public int errcode { get; set; }
        public string errmsg { get; set; }
        public string media_id { get; set; }
        public string type { get; set; }
        public long created_at { get; set; }
    }
}
