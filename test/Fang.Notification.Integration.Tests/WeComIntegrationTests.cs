using System;
using System.IO;
using System.Threading.Tasks;
using Fang.Notification.Core.Models;
using Fang.Notification.Facade;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Fang.Notification.Integration.Tests
{
    /// <summary>
    /// 企业微信通道集成测试
    /// 配置数据对应 samples/Sample.Console/Program.cs 中的 WeCom 配置
    /// </summary>
    [TestClass]
    public class WeComIntegrationTests
    {
        private ServiceProvider _serviceProvider;
        private NotificationService _notificationService;

        private static readonly string WebhookUrl =
            Environment.GetEnvironmentVariable("WECOM_WEBHOOK_URL") ?? "https://qyapi.weixin.qq.com/cgi-bin/webhook/send?key=your-webhook-key";

        private static readonly string CorpId = Environment.GetEnvironmentVariable("WECOM_CORP_ID") ?? "your-corp-id";
        private static readonly string CorpSecret = Environment.GetEnvironmentVariable("WECOM_CORP_SECRET") ?? "your-corp-secret";
        private static readonly int AgentId = int.Parse(Environment.GetEnvironmentVariable("WECOM_AGENT_ID") ?? "0");

        private const string UserId = "ChenJunYing";

        private const string FilePath =
            @"C:\Users\cjy56\Desktop\考勤\1.xlsx";

        private const string ImagePath =
            @"C:\Users\cjy56\Pictures\Snipaste_2025-05-15_10-55-03.png";

        [TestInitialize]
        public void Setup()
        {
            var services = new ServiceCollection();
            services.AddFangNotification(options =>
                {
                    options.EnableMessageLogging = true;
                })
                .AddWeCom(options =>
                {
                    options.WebhookUrl = WebhookUrl;
                    options.CorpId = CorpId;
                    options.CorpSecret = CorpSecret;
                    options.AgentId = AgentId;
                    options.BaseUrl = "https://qyapi.weixin.qq.com";
                });

            _serviceProvider = services.BuildServiceProvider();
            _notificationService = _serviceProvider.GetRequiredService<NotificationService>();
        }

        /// <summary>
        /// 文本消息 - Webhook 发送
        /// </summary>
        [TestMethod]
        [TestCategory("Integration")]
        public async Task SendTextMessage_ByWebhook_ShouldSucceed()
        {
            var message = new TextMessage
            {
                Title = "集成测试 - Webhook",
                Content = "这是一条来自 Fang.Notification 集成测试的企业微信 Webhook 文本消息。",
                Priority = MessagePriority.Normal
            };
            var receiver = MessageReceiver.FromWebhook(WebhookUrl);
            var result = await _notificationService.SendAsync("wecom", message, receiver);

            Assert.IsTrue(result.IsSuccess, $"Webhook 文本发送失败: {result.ErrorMessage}");
            Console.WriteLine($"Webhook 文本发送成功, MessageId: {result.RemoteMessageId}");
        }

        /// <summary>
        /// 文本消息 - 通过 API（UserId）发送
        /// </summary>
        [TestMethod]
        [TestCategory("Integration")]
        public async Task SendTextMessage_ByUserId_ShouldSucceed()
        {
            var message = new TextMessage
            {
                Title = "集成测试 - API",
                Content = "这是一条来自 Fang.Notification 集成测试的企业微信 API 文本消息。",
                Priority = MessagePriority.Normal
            };

            var receiver = new MessageReceiver().AddIdentifier(ReceiverType.UserId, UserId);
            var result = await _notificationService.SendAsync("wecom", message, receiver);

            Assert.IsTrue(result.IsSuccess, $"API 文本发送失败: {result.ErrorMessage}");
            Console.WriteLine($"API 文本发送成功, MessageId: {result.RemoteMessageId}");
        }

        /// <summary>
        /// Markdown 消息 - 通过 API（UserId）发送
        /// </summary>
        [TestMethod]
        [TestCategory("Integration")]
        public async Task SendMarkdownMessage_ByUserId_ShouldSucceed()
        {
            var message = new MarkdownMessage
            {
                Title = "集成测试 - Markdown",
                Content = "## 标题\n\n这是一条 **Markdown** 消息。\n\n- 项目一\n- 项目二",
                Priority = MessagePriority.Normal
            };

            var receiver = new MessageReceiver().AddIdentifier(ReceiverType.UserId, UserId);
            var result = await _notificationService.SendAsync("wecom", message, receiver);

            Assert.IsTrue(result.IsSuccess, $"Markdown 发送失败: {result.ErrorMessage}");
            Console.WriteLine($"Markdown 发送成功, MessageId: {result.RemoteMessageId}");
        }
    }
}
