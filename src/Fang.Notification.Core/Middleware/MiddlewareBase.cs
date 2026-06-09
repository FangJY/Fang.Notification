using System;
using System.Threading;
using System.Threading.Tasks;
using Fang.Notification.Core.Abstractions;
using Fang.Notification.Core.Models;

namespace Fang.Notification.Core.Middleware
{
    /// <summary>
    /// 中间件基类
    /// </summary>
    public abstract class MiddlewareBase : IMessageMiddleware
    {
        public abstract string Name { get; }

        public abstract int Order { get; }

        public abstract Task<SendResult> InvokeAsync(
            NotificationMessage message,
            MessageReceiver receiver,
            Func<NotificationMessage, MessageReceiver, Task<SendResult>> next,
            CancellationToken cancellationToken = default);
    }
}
