using System;
using System.Collections.Generic;

namespace Fang.Notification.Core.Models
{
    /// <summary>
    /// 统一消息模型基类
    /// </summary>
    public abstract class NotificationMessage
    {
        /// <summary>消息唯一标识</summary>
        public string MessageId { get; set; } = Guid.NewGuid().ToString("N");

        /// <summary>消息标题</summary>
        public string Title { get; set; }

        /// <summary>消息正文内容</summary>
        public string Content { get; set; }

        /// <summary>消息类型</summary>
        public abstract MessageType Type { get; }

        /// <summary>消息优先级</summary>
        public MessagePriority Priority { get; set; } = MessagePriority.Normal;

        /// <summary>创建时间</summary>
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>自定义扩展属性（平台特有参数）</summary>
        public Dictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();

        /// <summary>消息标签（用于分类、路由）</summary>
        public List<string> Tags { get; set; } = new List<string>();
    }

    /// <summary>
    /// 文本消息
    /// </summary>
    public class TextMessage : NotificationMessage
    {
        public override MessageType Type => MessageType.Text;

        /// <summary>是否at所有人（群聊场景）</summary>
        public bool AtAll { get; set; }

        /// <summary>需要at的用户列表</summary>
        public List<string> AtUserIds { get; set; } = new List<string>();

        /// <summary>需要at的手机号列表</summary>
        public List<string> AtMobiles { get; set; } = new List<string>();
    }

    /// <summary>
    /// Markdown消息
    /// </summary>
    public class MarkdownMessage : NotificationMessage
    {
        public override MessageType Type => MessageType.Markdown;
    }

    /// <summary>
    /// 卡片消息（交互式消息的抽象）
    /// </summary>
    public class CardMessage : NotificationMessage
    {
        public override MessageType Type => MessageType.Card;

        /// <summary>卡片标题</summary>
        public string HeaderTitle { get; set; }

        /// <summary>卡片模板类型</summary>
        public string TemplateType { get; set; }

        /// <summary>卡片数据（JSON格式的模板数据）</summary>
        public string TemplateData { get; set; }

        /// <summary>卡片元素列表</summary>
        public List<CardElement> Elements { get; set; } = new List<CardElement>();

        /// <summary>卡片操作按钮</summary>
        public List<CardAction> Actions { get; set; } = new List<CardAction>();
    }

    /// <summary>
    /// 卡片元素
    /// </summary>
    public class CardElement
    {
        public string Tag { get; set; }
        public string Text { get; set; }
        public Dictionary<string, object> Fields { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// 卡片操作按钮
    /// </summary>
    public class CardAction
    {
        public string ActionId { get; set; }
        public string Label { get; set; }
        public string Url { get; set; }
        public string Type { get; set; } = "button";
        public Dictionary<string, object> Value { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// 图文消息
    /// </summary>
    public class NewsMessage : NotificationMessage
    {
        public override MessageType Type => MessageType.News;
        public List<NewsArticle> Articles { get; set; } = new List<NewsArticle>();
    }

/// <summary>
/// 图文消息中的文章
/// </summary>
public class NewsArticle
{
    public string Title { get; set; }
    public string Description { get; set; }
    public string Url { get; set; }
    public string PicUrl { get; set; }
}

/// <summary>
/// 图片消息
/// </summary>
public class ImageMessage : NotificationMessage
{
    public override MessageType Type => MessageType.Image;

    /// <summary>图片URL地址</summary>
    public string ImageUrl { get; set; }

    /// <summary>图片Base64编码数据</summary>
    public string ImageBase64 { get; set; }

    /// <summary>媒体文件ID（平台上传后返回的media_id）</summary>
    public string MediaId { get; set; }

    /// <summary>图片宽度（像素）</summary>
    public int Width { get; set; }

    /// <summary>图片高度（像素）</summary>
    public int Height { get; set; }
}

/// <summary>
/// 文件消息
/// </summary>
public class FileMessage : NotificationMessage
{
    public override MessageType Type => MessageType.File;

    /// <summary>文件名（含扩展名）</summary>
    public string FileName { get; set; }

    /// <summary>文件本地路径</summary>
    public string FileUrl { get; set; }

    /// <summary>文件二进制内容</summary>
    public byte[] FileBytes { get; set; }

    /// <summary>媒体文件ID（平台上传后返回）</summary>
    public string MediaId { get; set; }

    /// <summary>文件大小（字节）</summary>
    public long FileSize { get; set; }

    /// <summary>文件MIME类型</summary>
    public string MediaType { get; set; }
}
}
