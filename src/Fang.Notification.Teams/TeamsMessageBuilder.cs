using Fang.Notification.Core.Abstractions;
using Fang.Notification.Core.Models;
using Fang.Notification.Teams.Models;

namespace Fang.Notification.Teams
{
    public class TeamsMessageBuilder : IMessageBuilder
    {
        public object BuildMessage(NotificationMessage message)
        {
            switch (message)
            {
                case TextMessage text:
                    return new { text = text.Content };
                case ImageMessage image:
                    var imageCard = new TeamsAdaptiveCard();
                    if (!string.IsNullOrEmpty(image.Title))
                    {
                        imageCard.body.Add(new TeamsTextBlock
                        {
                            text = image.Title,
                            size = "large",
                            weight = "bolder"
                        });
                    }
                    imageCard.body.Add(new
                    {
                        type = "Image",
                        url = image.ImageUrl ?? image.ImageBase64,
                        altText = image.Title ?? "image",
                        width = image.Width > 0 ? $"{image.Width}px" : "auto",
                        height = image.Height > 0 ? $"{image.Height}px" : "auto"
                    });
                    return imageCard;
                case CardMessage card:
                    var adaptiveCard = new TeamsAdaptiveCard();
                    adaptiveCard.body.Add(new TeamsTextBlock
                    {
                        text = card.HeaderTitle ?? card.Title,
                        size = "large",
                        weight = "bolder"
                    });
                    if (!string.IsNullOrEmpty(card.Content))
                    {
                        adaptiveCard.body.Add(new TeamsTextBlock
                        {
                            text = card.Content,
                            wrap = true
                        });
                    }
                    return adaptiveCard;
                default:
                    return new { text = message.Content };
            }
        }
    }
}
