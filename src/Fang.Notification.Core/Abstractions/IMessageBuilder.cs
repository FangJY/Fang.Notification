using Fang.Notification.Core.Models;

namespace Fang.Notification.Core.Abstractions
{
    /// <summary>
    /// 消息构建器接口
    /// </summary>
    public interface IMessageBuilder
    {
        /// <summary>
        /// 构建平台特定的消息对象
        /// </summary>
        object BuildMessage(NotificationMessage message);
    }
}
