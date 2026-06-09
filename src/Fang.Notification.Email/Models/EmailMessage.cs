using System.Collections.Generic;

namespace Fang.Notification.Email.Models
{
    public class EmailMessage
    {
        public string Subject { get; set; }
        public string Body { get; set; }
        public bool IsHtml { get; set; } = true;
        public List<string> ToAddresses { get; set; } = new List<string>();
        public List<string> CcAddresses { get; set; } = new List<string>();
        public List<string> BccAddresses { get; set; } = new List<string>();
        public List<Attachment> Attachments { get; set; } = new List<Attachment>();
        public string ReplyTo { get; set; }
        public string Priority { get; set; } = "Normal";
    }
}
