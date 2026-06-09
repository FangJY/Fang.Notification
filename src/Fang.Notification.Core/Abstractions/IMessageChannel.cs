using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Fang.Notification.Core.Models;

namespace Fang.Notification.Core.Abstractions
{
    /// <summary>
    /// 消息通道基础接口
    /// </summary>
    public interface IMessageChannel
    {
        /// <summary>通道唯一名称（如 "dingtalk", "feishu"）</summary>
        string ChannelName { get; }

        /// <summary>通道显示名称</summary>
        string DisplayName { get; }

        /// <summary>该通道支持的消息类型</summary>
        IReadOnlyList<MessageType> SupportedTypes { get; }

        /// <summary>检查是否支持指定的消息类型</summary>
        bool SupportsMessageType(MessageType type);

        /// <summary>
        /// 发送单条消息
        /// </summary>
        Task<SendResult> SendAsync(
            NotificationMessage message,
            MessageReceiver receiver,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// 支持批量发送的通道接口
    /// </summary>
    public interface IBatchMessageChannel : IMessageChannel
    {
        /// <summary>
        /// 批量发送消息
        /// </summary>
        Task<IEnumerable<SendResult>> SendBatchAsync(
            IEnumerable<NotificationMessage> messages,
            MessageReceiver receiver,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 向多个接收者发送同一条消息
        /// </summary>
        Task<IEnumerable<SendResult>> SendToManyAsync(
            NotificationMessage message,
            IEnumerable<MessageReceiver> receivers,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// 支持消息模板的通道接口
    /// </summary>
    public interface ITemplateMessageChannel : IMessageChannel
    {
        /// <summary>
        /// 发送模板消息
        /// </summary>
        Task<SendResult> SendTemplateAsync(
            string templateId,
            Dictionary<string, object> templateData,
            MessageReceiver receiver,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// 通道健康检查接口
    /// </summary>
    public interface IChannelHealthCheck
    {
        /// <summary>
        /// 检查通道是否可用
        /// </summary>
        Task<HealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// 健康检查结果
    /// </summary>
    public class HealthCheckResult
    {
        public bool IsHealthy { get; set; }
        public string Status { get; set; }
        public string Message { get; set; }
        public TimeSpan ResponseTime { get; set; }
        public DateTime CheckedAt { get; set; } = DateTime.UtcNow;
    }
}
