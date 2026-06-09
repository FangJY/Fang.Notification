using System.Collections.Generic;

namespace Fang.Notification.Core.Models
{
    /// <summary>
    /// 接收者类型枚举
    /// </summary>
    public enum ReceiverType
    {
        /// <summary>用户ID（平台内用户标识）</summary>
        UserId = 1,

        /// <summary>部门ID</summary>
        DepartmentId = 2,

        /// <summary>群聊ID</summary>
        GroupChatId = 3,

        /// <summary>Webhook地址</summary>
        WebhookUrl = 4,

        /// <summary>邮箱地址</summary>
        Email = 5,

        /// <summary>手机号</summary>
        Mobile = 6,

        /// <summary>开放平台OpenId</summary>
        OpenId = 7,

        /// <summary>频道/Teams频道ID</summary>
        ChannelId = 8,

        /// <summary>会话ID</summary>
        ConversationId = 9
    }

    /// <summary>
    /// 消息接收者
    /// </summary>
    public class MessageReceiver
    {
        /// <summary>接收者标识列表（支持多类型标识）</summary>
        public Dictionary<ReceiverType, List<string>> Identifiers { get; set; }
            = new Dictionary<ReceiverType, List<string>>();

        /// <summary>接收者名称（可选，用于日志）</summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// 添加接收者标识
        /// </summary>
        public MessageReceiver AddIdentifier(ReceiverType type, params string[] values)
        {
            if (!Identifiers.ContainsKey(type))
                Identifiers[type] = new List<string>();

            Identifiers[type].AddRange(values);
            return this;
        }

        /// <summary>
        /// 创建单个用户ID的接收者
        /// </summary>
        public static MessageReceiver FromUserId(string userId)
        {
            return new MessageReceiver().AddIdentifier(ReceiverType.UserId, userId);
        }

        /// <summary>
        /// 创建开放平台 OpenId 接收者（飞书 ou_ 前缀的用户标识）
        /// </summary>
        public static MessageReceiver FromOpenId(string openId)
        {
            return new MessageReceiver().AddIdentifier(ReceiverType.OpenId, openId);
        }

        /// <summary>
        /// 创建群聊接收者
        /// </summary>
        public static MessageReceiver FromGroupChatId(string groupChatId)
        {
            return new MessageReceiver().AddIdentifier(ReceiverType.GroupChatId, groupChatId);
        }

        /// <summary>
        /// 创建Webhook接收者
        /// </summary>
        public static MessageReceiver FromWebhook(string webhookUrl)
        {
            return new MessageReceiver().AddIdentifier(ReceiverType.WebhookUrl, webhookUrl);
        }

        /// <summary>
        /// 创建邮箱接收者
        /// </summary>
        public static MessageReceiver FromEmail(string email)
        {
            return new MessageReceiver().AddIdentifier(ReceiverType.Email, email);
        }

        /// <summary>
        /// 获取指定类型的第一个标识
        /// </summary>
        public string GetFirst(ReceiverType type)
        {
            return Identifiers.TryGetValue(type, out var list) && list.Count > 0
                ? list[0]
                : null;
        }

        /// <summary>
        /// 检查是否包含指定类型
        /// </summary>
        public bool HasType(ReceiverType type)
        {
            return Identifiers.ContainsKey(type) && Identifiers[type].Count > 0;
        }
    }
}
