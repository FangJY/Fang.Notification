using System;
using Fang.Notification.Core.Abstractions;
using Fang.Notification.Core.Models;

namespace Fang.Notification.Email
{
    public class EmailMessageBuilder : IMessageBuilder
    {
        public object BuildMessage(NotificationMessage message)
        {
            var emailMsg = new Models.EmailMessage
            {
                Subject = message.Title ?? "Notification",
                Body = message.Content
            };

            if (message is TextMessage text)
            {
                emailMsg.IsHtml = false;
            }

            if (message.Properties.TryGetValue("IsHtml", out var isHtml))
            {
                emailMsg.IsHtml = Convert.ToBoolean(isHtml);
            }

            return emailMsg;
        }
    }
}
