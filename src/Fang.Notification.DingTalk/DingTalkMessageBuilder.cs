using System;
using System.Linq;
using Fang.Notification.Core.Abstractions;
using Fang.Notification.Core.Models;
using Fang.Notification.DingTalk.Models;

namespace Fang.Notification.DingTalk
{
    public class DingTalkMessageBuilder : IMessageBuilder
    {
        public object BuildMessage(NotificationMessage message)
        {
            switch (message)
            {
                case TextMessage text:
                    return new DingTalkWebhookRequest
                    {
                        msgtype = "text",
                        text = new DingTalkTextMessage { content = text.Content },
                        at = new DingTalkAtInfo
                        {
                            atMobiles = text.AtMobiles?.ToArray(),
                            atUserIds = text.AtUserIds?.ToArray(),
                            isAtAll = text.AtAll
                        }
                    };
                case MarkdownMessage markdown:
                    return new DingTalkWebhookRequest
                    {
                        msgtype = "markdown",
                        markdown = new DingTalkMarkdownMessage
                        {
                            title = markdown.Title,
                            text = markdown.Content
                        }
                    };
                case ImageMessage _:
                    throw new NotSupportedException("钉钉Webhook不支持图片消息");
                case FileMessage _:
                    throw new NotSupportedException("钉钉Webhook不支持文件消息");
                default:
                    throw new NotSupportedException($"钉钉不支持消息类型: {message.Type}");
            }
        }
    }
}
