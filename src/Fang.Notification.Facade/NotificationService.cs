using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Fang.Notification.Core.Abstractions;
using Fang.Notification.Core.Configuration;
using Fang.Notification.Core.Models;
using Microsoft.Extensions.Options;

namespace Fang.Notification.Facade
{
    /// <summary>
    /// 通知服务（统一入口）
    /// </summary>
    public class NotificationService
    {
        private readonly Dictionary<string, IMessageChannel> _channels;
        private readonly FangNotificationOptions _options;
        private readonly SemaphoreSlim _concurrencyLimiter;

        public NotificationService(
            IEnumerable<IMessageChannel> channels,
            IOptions<FangNotificationOptions> options = null)
        {
            _channels = channels?.ToDictionary(c => c.ChannelName, StringComparer.OrdinalIgnoreCase)
                ?? throw new ArgumentNullException(nameof(channels));
            _options = options?.Value ?? new FangNotificationOptions();
            _concurrencyLimiter = new SemaphoreSlim(_options.MaxConcurrentSends);
        }

        /// <summary>
        /// 获取所有已注册的通道名称
        /// </summary>
        public IEnumerable<string> GetRegisteredChannels() => _channels.Keys;

        /// <summary>
        /// 发送消息到指定通道
        /// </summary>
        public async Task<SendResult> SendAsync(
            string channelName,
            NotificationMessage message,
            MessageReceiver receiver,
            CancellationToken cancellationToken = default)
        {
            if (!_channels.TryGetValue(channelName, out var channel))
                return SendResult.Failure(channelName, message?.MessageId, "CHANNEL_NOT_FOUND",
                    $"通道 {channelName} 未注册");

            if (!channel.SupportsMessageType(message.Type))
                return SendResult.Failure(channelName, message.MessageId, "UNSUPPORTED_TYPE",
                    $"通道 {channelName} 不支持消息类型 {message.Type}");

            await _concurrencyLimiter.WaitAsync(cancellationToken);
            try
            {
                return await channel.SendAsync(message, receiver, cancellationToken);
            }
            finally
            {
                _concurrencyLimiter.Release();
            }
        }

        /// <summary>
        /// 向多个通道广播消息
        /// </summary>
        public async Task<IDictionary<string, SendResult>> BroadcastAsync(
            IEnumerable<string> channelNames,
            NotificationMessage message,
            MessageReceiver receiver,
            CancellationToken cancellationToken = default)
        {
            var channels = channelNames?.ToList() ?? _channels.Keys.ToList();
            var results = new ConcurrentDictionary<string, SendResult>();

            var tasks = channels.Select(async channelName =>
            {
                var result = await SendAsync(channelName, message, receiver, cancellationToken);
                results[channelName] = result;
            });

            await Task.WhenAll(tasks);
            return results;
        }

        /// <summary>
        /// 使用复合接收者发送（同时向多个平台的不同接收者发送）
        /// </summary>
        public async Task<IEnumerable<SendResult>> SendToMultipleAsync(
            IDictionary<string, MessageReceiver> channelReceivers,
            NotificationMessage message,
            CancellationToken cancellationToken = default)
        {
            var tasks = channelReceivers.Select(kvp =>
                SendAsync(kvp.Key, message, kvp.Value, cancellationToken));

            return await Task.WhenAll(tasks);
        }

        /// <summary>
        /// 批量发送（同一通道，同一接收者，多条消息）
        /// </summary>
        public async Task<IEnumerable<SendResult>> SendBatchAsync(
            string channelName,
            IEnumerable<NotificationMessage> messages,
            MessageReceiver receiver,
            CancellationToken cancellationToken = default)
        {
            if (!_channels.TryGetValue(channelName, out var channel))
                throw new NotSupportedException($"通道 {channelName} 未注册");

            if (channel is IBatchMessageChannel batchChannel)
            {
                return await batchChannel.SendBatchAsync(messages, receiver, cancellationToken);
            }

            var tasks = messages.Select(m => SendAsync(channelName, m, receiver, cancellationToken));
            return await Task.WhenAll(tasks);
        }
    }
}
