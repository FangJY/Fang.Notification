using System;
using System.IO;
using System.Threading.Tasks;
using Fang.Notification.Core.Models;
using Fang.Notification.Facade;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Fang.Notification.Integration.Tests
{
    [TestClass]
    public class IntegrationTests
    {
        private ServiceProvider _serviceProvider;
        private NotificationService _notificationService;

        private string OpenId = "ou_5da8925912d32b603ada2573e916cc03";

        private static readonly string WebhookUrl =
            Environment.GetEnvironmentVariable("FEISHU_WEBHOOK_URL") ?? "https://open.feishu.cn/open-apis/bot/v2/hook/your-webhook-id";

        private const string GroupChatId =
            "oc_9bdf295b315929712b678ee36fb3fe5e";

        private const string ImagePath =
            @"C:\Users\cjy56\Pictures\Snipaste_2025-05-15_10-55-03.png";

        private const string ExclePath =
            @"C:\Users\cjy56\Desktop\考勤\1.xlsx";

        [TestInitialize]
        public void Setup()
        {
            var services = new ServiceCollection();
            services.AddFangNotification(options =>
                {
                    options.EnableMessageLogging = true;
                })
                .AddFeishu(options =>
                {
                    options.WebhookUrl = WebhookUrl;
                    options.AppId = Environment.GetEnvironmentVariable("FEISHU_APP_ID") ?? "your-app-id";
                    options.AppSecret = Environment.GetEnvironmentVariable("FEISHU_APP_SECRET") ?? "your-app-secret";
                    options.BaseUrl = "https://open.feishu.cn";
                    options.TokenType = "tenant";
                });

            _serviceProvider = services.BuildServiceProvider();
            _notificationService = _serviceProvider.GetRequiredService<NotificationService>();
        }

        /// <summary>
        /// 文本消息 - Webhook 发送（测试机器人 Webhook 地址）
        /// </summary>
        [TestMethod]
        [TestCategory("Integration")]
        public async Task SendTextMessage_ByWebhook_ShouldSucceed()
        {
            var message = new TextMessage
            {
                Title = "集成测试 - Webhook",
                Content = "这是一条来自 Fang.Notification 集成测试的 Webhook 文本消息。",
                Priority = MessagePriority.Normal
            };
            var receiver = MessageReceiver.FromWebhook(WebhookUrl);
            var result = await _notificationService.SendAsync("feishu", message, receiver);

            Assert.IsTrue(result.IsSuccess, $"Webhook 文本发送失败: {result.ErrorMessage}");
            Console.WriteLine($"Webhook 文本发送成功, MessageId: {result.RemoteMessageId}");
        }

        /// <summary>
        /// 文本消息 - 通过 API（群聊 ID）发送，对应 Sample.Console 中 GroupChatId 发送方式
        /// </summary>
        [TestMethod]
        [TestCategory("Integration")]
        public async Task SendTextMessage_ByGroupChat_ShouldSucceed()
        {
            var message = new TextMessage
            {
                Title = "集成测试 - API",
                Content = "这是一条来自 Fang.Notification 集成测试的 API 文本消息。",
                Priority = MessagePriority.Normal
            };

            var receiver = MessageReceiver.FromGroupChatId(GroupChatId);
            var result = await _notificationService.SendAsync("feishu", message, receiver);

            Assert.IsTrue(result.IsSuccess, $"API 文本发送失败: {result.ErrorMessage}");
            Console.WriteLine($"API 文本发送成功, MessageId: {result.RemoteMessageId}");
        }

        /// <summary>
        /// 文件消息 - 通过 API（群聊 ID）发送，对应 Sample.Console 中 FileMessage 发送方式
        /// </summary>
        [TestMethod]
        [TestCategory("Integration")]
        public async Task SendFileMessage_ByGroupChat_ShouldSucceed()
        {
            Assert.IsTrue(File.Exists(ExclePath),
                $"测试文件不存在: {ExclePath}");

            var message = new FileMessage
            {
                Title = "集成测试 - 文件",
                FileUrl = ExclePath,
                FileName = "1.xlsx"
            };

            var receiver = MessageReceiver.FromGroupChatId(GroupChatId);
            var result = await _notificationService.SendAsync("feishu", message, receiver);

            Assert.IsTrue(result.IsSuccess, $"文件发送失败: {result.ErrorMessage}");
            Console.WriteLine($"文件发送成功, MessageId: {result.RemoteMessageId}");
        }

        /// <summary>
        /// 图片消息 - 通过 API（群聊 ID）发送，读取本地图片转为 Base64 后上传
        /// 对应 IM 消息中的图片消息类型
        /// </summary>
        [TestMethod]
        [TestCategory("Integration")]
        public async Task SendImageMessage_ByGroupChat_ShouldSucceed()
        {
            Assert.IsTrue(File.Exists(ImagePath),
                $"测试图片不存在: {ImagePath}");

            // 读取本地图片文件，转为 Base64
            byte[] imageBytes = File.ReadAllBytes(ImagePath);
            string imageBase64 = Convert.ToBase64String(imageBytes);

            var message = new ImageMessage
            {
                Title = "集成测试 - 图片",
                ImageBase64 = imageBase64,
                Width = 0,
                Height = 0
            };

            var receiver = MessageReceiver.FromGroupChatId(GroupChatId);
            var result = await _notificationService.SendAsync("feishu", message, receiver);

            Assert.IsTrue(result.IsSuccess, $"图片发送失败: {result.ErrorMessage}");
            Console.WriteLine($"图片发送成功, MessageId: {result.RemoteMessageId}");
        }

        /// <summary>
        /// 文本消息 - 通过 API（UserId）发送，结构与 GroupChat 测试一致
        /// </summary>
        [TestMethod]
        [TestCategory("Integration")]
        public async Task SendTextMessage_ByUserId_ShouldSucceed()
        {
            var message = new TextMessage
            {
                Title = "集成测试 - UserId",
                Content = "这是一条来自 Fang.Notification 集成测试的 UserId 文本消息。",
                Priority = MessagePriority.Normal
            };

            // ou_ 前缀是飞书 open_id，使用 ReceiverType.OpenId
            var receiver = new MessageReceiver().AddIdentifier(ReceiverType.OpenId, OpenId);
            var result = await _notificationService.SendAsync("feishu", message, receiver);

            Assert.IsTrue(result.IsSuccess, $"UserId(OpenId) 文本发送失败: {result.ErrorMessage}");
            Console.WriteLine($"UserId(OpenId) 文本发送成功, MessageId: {result.RemoteMessageId}");
        }

        /// <summary>
        /// 文件消息 - 通过 API（UserId）发送
        /// </summary>
        [TestMethod]
        [TestCategory("Integration")]
        public async Task SendFileMessage_ByUserId_ShouldSucceed()
        {
            Assert.IsTrue(File.Exists(ExclePath),
                $"测试文件不存在: {ExclePath}");

            var message = new FileMessage
            {
                Title = "集成测试 - 文件(UserId)",
                FileUrl = ExclePath,
                FileName = "1.xlsx"
            };

            var receiver = new MessageReceiver().AddIdentifier(ReceiverType.OpenId, OpenId);
            var result = await _notificationService.SendAsync("feishu", message, receiver);

            Assert.IsTrue(result.IsSuccess, $"UserId(OpenId) 文件发送失败: {result.ErrorMessage}");
            Console.WriteLine($"UserId(OpenId) 文件发送成功, MessageId: {result.RemoteMessageId}");
        }

        /// <summary>
        /// 图片消息 - 通过 API（UserId）发送
        /// </summary>
        [TestMethod]
        [TestCategory("Integration")]
        public async Task SendImageMessage_ByUserId_ShouldSucceed()
        {
            Assert.IsTrue(File.Exists(ImagePath),
                $"测试图片不存在: {ImagePath}");

            byte[] imageBytes = File.ReadAllBytes(ImagePath);
            string imageBase64 = Convert.ToBase64String(imageBytes);

            var message = new ImageMessage
            {
                Title = "集成测试 - 图片(UserId)",
                ImageBase64 = imageBase64,
                Width = 0,
                Height = 0
            };

            var receiver = new MessageReceiver().AddIdentifier(ReceiverType.OpenId, OpenId);
            var result = await _notificationService.SendAsync("feishu", message, receiver);

            Assert.IsTrue(result.IsSuccess, $"UserId(OpenId) 图片发送失败: {result.ErrorMessage}");
            Console.WriteLine($"UserId 图片发送成功, MessageId: {result.RemoteMessageId}");
        }
    }
}
