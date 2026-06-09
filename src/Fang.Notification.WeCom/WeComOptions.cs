using Fang.Notification.Core.Configuration;

namespace Fang.Notification.WeCom
{
    public class WeComOptions : ChannelOptions
    {
        public string CorpId { get; set; }
        public string CorpSecret { get; set; }
        public int? AgentId { get; set; }
        public string WebhookUrl { get; set; }
        public string BaseUrl { get; set; } = "https://qyapi.weixin.qq.com";
        public string UploadUrl { get; set; }
    }
}
