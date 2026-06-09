using Fang.Notification.Core.Configuration;

namespace Fang.Notification.Teams
{
    public class TeamsOptions : ChannelOptions
    {
        public string TenantId { get; set; }
        public string ClientId { get; set; }
        public string ClientSecret { get; set; }
        public string TeamId { get; set; }
        public string ChannelId { get; set; }
        public string WebhookUrl { get; set; }
        public string GraphBaseUrl { get; set; } = "https://graph.microsoft.com/v1.0";
        public string AuthorityUrl { get; set; } = "https://login.microsoftonline.com";
        public string[] Scopes { get; set; } = { "https://graph.microsoft.com/.default" };
    }
}
