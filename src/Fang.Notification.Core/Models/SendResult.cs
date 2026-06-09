using System;

namespace Fang.Notification.Core.Models
{
    /// <summary>
    /// 消息发送结果
    /// </summary>
    public class SendResult
    {
        /// <summary>是否成功</summary>
        public bool IsSuccess { get; set; }

        /// <summary>平台返回的消息ID</summary>
        public string RemoteMessageId { get; set; }

        /// <summary>本地消息ID</summary>
        public string LocalMessageId { get; set; }

        /// <summary>通道名称</summary>
        public string ChannelName { get; set; }

        /// <summary>错误码（平台返回）</summary>
        public string ErrorCode { get; set; }

        /// <summary>错误信息</summary>
        public string ErrorMessage { get; set; }

        /// <summary>发送耗时（毫秒）</summary>
        public long ElapsedMilliseconds { get; set; }

        /// <summary>重试次数</summary>
        public int RetryCount { get; set; }

        /// <summary>原始响应数据</summary>
        public string RawResponse { get; set; }

        /// <summary>发送时间</summary>
        public DateTimeOffset SentAt { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>创建成功结果</summary>
        public static SendResult Success(string channelName, string localMessageId,
            string remoteMessageId = null, long elapsedMs = 0)
        {
            return new SendResult
            {
                IsSuccess = true,
                ChannelName = channelName,
                LocalMessageId = localMessageId,
                RemoteMessageId = remoteMessageId,
                ElapsedMilliseconds = elapsedMs
            };
        }

        /// <summary>创建失败结果</summary>
        public static SendResult Failure(string channelName, string localMessageId,
            string errorCode, string errorMessage, string rawResponse = null)
        {
            return new SendResult
            {
                IsSuccess = false,
                ChannelName = channelName,
                LocalMessageId = localMessageId,
                ErrorCode = errorCode,
                ErrorMessage = errorMessage,
                RawResponse = rawResponse
            };
        }
    }
}
