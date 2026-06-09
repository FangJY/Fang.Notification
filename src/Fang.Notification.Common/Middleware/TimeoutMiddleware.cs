using System;
using System.Threading;
using System.Threading.Tasks;
using Fang.Notification.Core.Abstractions;
using Fang.Notification.Core.Models;

namespace Fang.Notification.Common.Middleware
{
    /// <summary>
    /// 超时控制中间件
    /// </summary>
    public class TimeoutMiddleware : IMessageMiddleware
    {
        private readonly int _timeoutSeconds;

        public TimeoutMiddleware(int timeoutSeconds = 60)
        {
            _timeoutSeconds = timeoutSeconds;
        }

        public string Name => "TimeoutMiddleware";
        public int Order => 50;

        public async Task<SendResult> InvokeAsync(
            NotificationMessage message,
            MessageReceiver receiver,
            Func<NotificationMessage, MessageReceiver, Task<SendResult>> next,
            CancellationToken cancellationToken = default)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(_timeoutSeconds));

            try
            {
                return await next(message, receiver).WithCancellation(cts.Token);
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                return SendResult.Failure("unknown", message.MessageId, "TIMEOUT",
                    $"Request timed out after {_timeoutSeconds} seconds");
            }
        }
    }

    /// <summary>
    /// Task扩展方法（取消支持）
    /// </summary>
    public static class TaskExtensions
    {
        public static async Task<T> WithCancellation<T>(this Task<T> task, CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<bool>();
            using (cancellationToken.Register(s => ((TaskCompletionSource<bool>)s).TrySetResult(true), tcs))
            {
                if (task != await Task.WhenAny(task, tcs.Task))
                    throw new OperationCanceledException(cancellationToken);
            }
            return await task;
        }
    }
}
