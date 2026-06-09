using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Fang.Notification.Core.Abstractions;
using Fang.Notification.Core.Models;

namespace Fang.Notification.Core.Middleware
{
    /// <summary>
    /// 消息管道构建器
    /// </summary>
    public class MessagePipelineBuilder
    {
        private readonly List<IMessageMiddleware> _middlewares = new List<IMessageMiddleware>();
        private Func<NotificationMessage, MessageReceiver, Task<SendResult>> _finalHandler;

        /// <summary>
        /// 添加中间件
        /// </summary>
        public MessagePipelineBuilder Use(IMessageMiddleware middleware)
        {
            _middlewares.Add(middleware);
            return this;
        }

        /// <summary>
        /// 设置最终处理器
        /// </summary>
        public MessagePipelineBuilder SetFinalHandler(
            Func<NotificationMessage, MessageReceiver, Task<SendResult>> handler)
        {
            _finalHandler = handler;
            return this;
        }

        /// <summary>
        /// 构建管道
        /// </summary>
        public MessagePipeline Build()
        {
            if (_finalHandler == null)
                throw new InvalidOperationException("Final handler must be set before building pipeline.");

            var sorted = _middlewares.OrderBy(m => m.Order).ToList();
            return new MessagePipeline(sorted, _finalHandler);
        }
    }

    /// <summary>
    /// 消息管道
    /// </summary>
    public class MessagePipeline
    {
        private Func<NotificationMessage, MessageReceiver, Task<SendResult>> _handler;

        internal MessagePipeline(
            IReadOnlyList<IMessageMiddleware> middlewares,
            Func<NotificationMessage, MessageReceiver, Task<SendResult>> finalHandler)
        {
            _handler = finalHandler;

            foreach (var middleware in middlewares.Reverse())
            {
                var next = _handler;
                var current = middleware;
                _handler = (msg, rec) => current.InvokeAsync(msg, rec,
                    (m, r) => next(m, r), CancellationToken.None);
            }
        }

        /// <summary>
        /// 执行管道
        /// </summary>
        public Task<SendResult> ExecuteAsync(
            NotificationMessage message,
            MessageReceiver receiver,
            CancellationToken cancellationToken = default)
        {
            return _handler(message, receiver);
        }
    }
}
