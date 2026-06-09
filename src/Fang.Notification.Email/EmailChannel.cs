using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Fang.Notification.Common.Middleware;
using System.IO;
using Fang.Notification.Core.Abstractions;
using Fang.Notification.Core.Configuration;
using Fang.Notification.Core.Middleware;
using Fang.Notification.Core.Models;
using Microsoft.Extensions.Options;

namespace Fang.Notification.Email
{
    public class EmailChannel : IMessageChannel
    {
        private readonly EmailOptions _options;
        private readonly MessagePipeline _pipeline;

        public string ChannelName => "email";
        public string DisplayName => "邮件";

        public IReadOnlyList<MessageType> SupportedTypes => new List<MessageType>
        {
            MessageType.Text,
            MessageType.Markdown,
            MessageType.Image,
            MessageType.File
        };

        public EmailChannel(
            IOptions<EmailOptions> options,
            IEnumerable<IMessageMiddleware> middlewares = null)
        {
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

            var builder = new MessagePipelineBuilder()
                .Use(new LoggingMiddleware(LogMessage))
                .Use(new RetryMiddleware())
                .SetFinalHandler((msg, rec) => SendCoreAsync(msg, rec, CancellationToken.None));

            if (middlewares != null)
            {
                foreach (var middleware in middlewares.OrderBy(m => m.Order))
                    builder.Use(middleware);
            }

            _pipeline = builder.Build();
        }

        public bool SupportsMessageType(MessageType type) => SupportedTypes.Contains(type);

        public async Task<SendResult> SendAsync(
            NotificationMessage message,
            MessageReceiver receiver,
            CancellationToken cancellationToken = default)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));
            if (receiver == null) throw new ArgumentNullException(nameof(receiver));

            return await _pipeline.ExecuteAsync(message, receiver, cancellationToken);
        }

        private async Task<SendResult> SendCoreAsync(
            NotificationMessage message,
            MessageReceiver receiver,
            CancellationToken cancellationToken)
        {
            try
            {
                using var smtpClient = new SmtpClient(_options.SmtpHost, _options.Port)
                {
                    EnableSsl = _options.EnableSsl,
                    UseDefaultCredentials = _options.UseDefaultCredentials
                };

                if (!string.IsNullOrEmpty(_options.UserName) && !string.IsNullOrEmpty(_options.Password))
                {
                    smtpClient.Credentials = new NetworkCredential(_options.UserName, _options.Password);
                }

                using var mailMessage = new MailMessage
                {
                    From = new MailAddress(_options.FromAddress, _options.FromName),
                    Subject = message.Title ?? "Notification",
                    Body = message.Content,
                    IsBodyHtml = _options.IsHtml,
                    BodyEncoding = Encoding.GetEncoding(_options.Encoding),
                    SubjectEncoding = Encoding.GetEncoding(_options.Encoding)
                };

                var emailAddresses = receiver.Identifiers
                    .Where(kv => kv.Key == ReceiverType.Email)
                    .SelectMany(kv => kv.Value);

                foreach (var email in emailAddresses)
                {
                    mailMessage.To.Add(email);
                }

                if (message.Properties.TryGetValue("Cc", out var cc) && cc is string ccStr)
                {
                    foreach (var addr in ccStr.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries))
                        mailMessage.CC.Add(addr.Trim());
                }

                if (message.Properties.TryGetValue("Bcc", out var bcc) && bcc is string bccStr)
                {
                    foreach (var addr in bccStr.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries))
                        mailMessage.Bcc.Add(addr.Trim());
                }

                if (message is FileMessage fileMsg && fileMsg.FileBytes != null && !string.IsNullOrEmpty(fileMsg.FileName))
                {
                    var attachment = new System.Net.Mail.Attachment(
                        new MemoryStream(fileMsg.FileBytes), fileMsg.FileName, fileMsg.MediaType ?? "application/octet-stream");
                    mailMessage.Attachments.Add(attachment);
                }

                if (message is ImageMessage imageMsg && imageMsg.ImageBase64 != null)
                {
                    var imageBytes = Convert.FromBase64String(imageMsg.ImageBase64);
                    var fileName = !string.IsNullOrEmpty(imageMsg.Title)
                        ? imageMsg.Title
                        : "image";
                    var ext = imageMsg.ImageUrl != null
                        ? Path.GetExtension(imageMsg.ImageUrl) ?? ".png"
                        : ".png";
                    var attachment = new System.Net.Mail.Attachment(
                        new MemoryStream(imageBytes), $"{fileName}{ext}", "image/png");
                    mailMessage.Attachments.Add(attachment);
                }

                await smtpClient.SendMailAsync(mailMessage);

                return SendResult.Success(ChannelName, message.MessageId);
            }
            catch (Exception ex)
            {
                return SendResult.Failure(ChannelName, message.MessageId, "EMAIL_FAILED", ex.Message);
            }
        }

        private void LogMessage(string msg, LogLevel level)
        {
            System.Diagnostics.Debug.WriteLine($"[{level}] {msg}");
        }
    }
}
