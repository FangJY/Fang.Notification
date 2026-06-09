using System.Collections.Generic;

namespace Fang.Notification.Core.Models
{
    /// <summary>
    /// 支持的消息类型
    /// </summary>
    public enum MessageType
    {
        /// <summary>纯文本消息</summary>
        Text = 1,

        /// <summary>Markdown消息</summary>
        Markdown = 2,

        /// <summary>图文消息（仅部分平台支持）</summary>
        News = 3,

        /// <summary>图片消息</summary>
        Image = 4,

        /// <summary>文件消息</summary>
        File = 5,

        /// <summary>卡片消息（交互式）</summary>
        Card = 6,

        /// <summary>模板消息</summary>
        Template = 7
    }

    /// <summary>
    /// 消息优先级
    /// </summary>
    public enum MessagePriority
    {
        Low = 0,
        Normal = 1,
        High = 2,
        Urgent = 3
    }
}
