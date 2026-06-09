using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Fang.Notification.Core.Abstractions;
using Fang.Notification.Core.Models;
using LogLevel = Fang.Notification.Core.Configuration.LogLevel;

namespace Fang.Notification.Common.Middleware
{
    /// <summary>
    /// 日志中间件
    /// </summary>
    public class LoggingMiddleware : IMessageMiddleware
    {
        private readonly Action<string, LogLevel> _logger;

        public LoggingMiddleware(Action<string, LogLevel> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public string Name => "LoggingMiddleware";
        public int Order => 0;

        public async Task<SendResult> InvokeAsync(
            NotificationMessage message,
            MessageReceiver receiver,
            Func<NotificationMessage, MessageReceiver, Task<SendResult>> next,
            CancellationToken cancellationToken = default)
        {
            _logger($"Sending message [{message.MessageId}] of type {message.Type}", LogLevel.Information);

            var sw = Stopwatch.StartNew();

            try
            {
                var result = await next(message, receiver);
                sw.Stop();

                if (result.IsSuccess)
                {
                    _logger($"Message [{message.MessageId}] sent successfully in {sw.ElapsedMilliseconds}ms",
                        LogLevel.Information);
                }
                else
                {
                    _logger($"Message [{message.MessageId}] failed: {result.ErrorMessage}",
                        LogLevel.Error);
                }

                return result;
            }
            catch (Exception ex)
            {
                sw.Stop();
                _logger($"Message [{message.MessageId}] exception: {ex.Message}", LogLevel.Error);
                throw;
            }
        }
    }
}
