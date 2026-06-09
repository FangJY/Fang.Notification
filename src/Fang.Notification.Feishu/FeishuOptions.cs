using Fang.Notification.Core.Configuration;

namespace Fang.Notification.Feishu
{
    public class FeishuOptions : ChannelOptions
    {
        public string AppId { get; set; }
        public string AppSecret { get; set; }
        public string VerificationToken { get; set; }
        public string EncryptKey { get; set; }
        public string WebhookUrl { get; set; }
        public string BaseUrl { get; set; } = "https://open.feishu.cn";
        public string TokenType { get; set; } = "tenant";
        public int TokenRefreshAdvanceSeconds { get; set; } = 300;
    }
}
