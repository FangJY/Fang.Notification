namespace Fang.Notification.Email.Models
{
    public class Attachment
    {
        public string FileName { get; set; }
        public byte[] Content { get; set; }
        public string MediaType { get; set; }
    }
}
