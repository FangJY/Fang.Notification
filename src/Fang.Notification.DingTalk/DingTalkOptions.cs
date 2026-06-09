using Fang.Notification.Core.Configuration;

namespace Fang.Notification.DingTalk
{
    /// <summary>
    /// 钉钉通道配置
    /// </summary>
    public class DingTalkOptions : ChannelOptions
    {
        public string ClientID { get; set; }
        public string ClientSecret { get; set; }
        public string CorpId { get; set; }
        public long? AgentId { get; set; }

        public string WebhookUrl { get; set; }
        public string WebhookSecret { get; set; }
        public string BaseUrl { get; set; } = "https://api.dingtalk.com";
        public int TokenRefreshAdvanceSeconds { get; set; } = 300;

        /// <summary>
        /// 机器人编码（robotCode），用于机器人发送群聊消息 API。
        /// 创建企业内部应用机器人后，robotCode 即应用的 AppKey（ClientID）。
        /// </summary>
        public string RobotCode { get; set; }
    }
}
