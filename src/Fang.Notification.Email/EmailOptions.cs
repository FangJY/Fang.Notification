using Fang.Notification.Core.Configuration;

namespace Fang.Notification.Email
{
    public class EmailOptions : ChannelOptions
    {
        public string SmtpHost { get; set; }
        public int Port { get; set; } = 587;
        public string FromAddress { get; set; }
        public string FromName { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }
        public bool EnableSsl { get; set; } = true;
        public bool UseDefaultCredentials { get; set; } = false;
        public string Encoding { get; set; } = "UTF-8";
        public bool IsHtml { get; set; } = true;
    }
}
