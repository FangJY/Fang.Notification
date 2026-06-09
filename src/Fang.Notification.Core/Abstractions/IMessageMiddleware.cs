using System;
using System.Threading;
using System.Threading.Tasks;
using Fang.Notification.Core.Models;

namespace Fang.Notification.Core.Abstractions
{
    /// <summary>
    /// 消息中间件接口
    /// </summary>
    public interface IMessageMiddleware
    {
        /// <summary>中间件名称</summary>
        string Name { get; }

        /// <summary>执行顺序（越小越先执行）</summary>
        int Order { get; }

        /// <summary>
        /// 中间件执行方法
        /// </summary>
        Task<SendResult> InvokeAsync(
            NotificationMessage message,
            MessageReceiver receiver,
            Func<NotificationMessage, MessageReceiver, Task<SendResult>> next,
            CancellationToken cancellationToken = default);
    }
}
