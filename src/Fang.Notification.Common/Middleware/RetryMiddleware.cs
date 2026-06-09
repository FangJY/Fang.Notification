using System;
using System.Threading;
using System.Threading.Tasks;
using Fang.Notification.Core.Abstractions;
using Fang.Notification.Core.Models;

namespace Fang.Notification.Common.Middleware
{
    /// <summary>
    /// 重试中间件
    /// </summary>
    public class RetryMiddleware : IMessageMiddleware
    {
        public string Name => "RetryMiddleware";
        public int Order => 100;

        public async Task<SendResult> InvokeAsync(
            NotificationMessage message,
            MessageReceiver receiver,
            Func<NotificationMessage, MessageReceiver, Task<SendResult>> next,
            CancellationToken cancellationToken = default)
        {
            int maxRetries = 0;
            int retryDelayMs = 1000;

            if (message.Properties.TryGetValue("MaxRetries", out var maxRetriesObj))
                maxRetries = Convert.ToInt32(maxRetriesObj);
            if (message.Properties.TryGetValue("RetryDelayMs", out var retryDelayObj))
                retryDelayMs = Convert.ToInt32(retryDelayObj);

            SendResult result = null;

            for (int i = 0; i <= maxRetries; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                result = await next(message, receiver);
                result.RetryCount = i;

                if (result.IsSuccess)
                    return result;

                if (i < maxRetries)
                {
                    var delay = i == 0 ? retryDelayMs : retryDelayMs * (int)Math.Pow(2, i - 1);
                    await Task.Delay(delay, cancellationToken);
                }
            }

            return result;
        }
    }
}
