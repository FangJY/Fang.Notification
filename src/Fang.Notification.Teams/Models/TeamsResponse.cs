namespace Fang.Notification.Teams.Models
{
    public class TeamsResponse
    {
        public string Id { get; set; }
        public string MessageId { get; set; }
        public ErrorResponse Error { get; set; }
    }

    public class ErrorResponse
    {
        public string Code { get; set; }
        public string Message { get; set; }
    }
}
