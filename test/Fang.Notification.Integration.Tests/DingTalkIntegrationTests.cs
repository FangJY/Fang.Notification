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
    /// 钉钉通道集成测试
    /// 配置数据对应 samples/Sample.Console/Program.cs 中的 DingTalk 配置
    /// </summary>
    [TestClass]
    public class DingTalkIntegrationTests
    {
        private ServiceProvider _serviceProvider;
        private NotificationService _notificationService;

        private static readonly string WebhookUrl =
            Environment.GetEnvironmentVariable("DINGTALK_WEBHOOK_URL") ?? "https://oapi.dingtalk.com/robot/send?access_token=your-access-token";

        private static readonly string WebhookSecret =
            Environment.GetEnvironmentVariable("DINGTALK_WEBHOOK_SECRET") ?? "SECyour-webhook-secret";

        private static readonly string ClientId = Environment.GetEnvironmentVariable("DINGTALK_CLIENT_ID") ?? "your-client-id";
        private static readonly string ClientSecret = Environment.GetEnvironmentVariable("DINGTALK_CLIENT_SECRET") ?? "your-client-secret";
        private static readonly string CorpId = Environment.GetEnvironmentVariable("DINGTALK_CORP_ID") ?? "your-corp-id";
        private static readonly long AgentId = long.Parse(Environment.GetEnvironmentVariable("DINGTALK_AGENT_ID") ?? "0");

        private const string UserId = "213904134737638319";

        private const string FilePath =
            @"C:\Users\cjy56\Desktop\考勤\1.xlsx";

        private const string ImagePath =
            @"C:\Users\cjy56\Pictures\Snipaste_2025-05-15_10-55-03.png";

        /// <summary>
        /// 群聊会话 ID（chatid），用于 oapi.dingtalk.com/chat/send API。
        /// 可通过钉钉 JSAPI chooseChat 获取，接口返回的 chatid 字段。
        /// </summary>
        private const string ChatId = "chat3e646d6199323e48c8a1b8ed0599272b"; // TODO: 替换为实际的 chatid

        [TestInitialize]
        public void Setup()
        {
            var services = new ServiceCollection();
            services.AddFangNotification(options =>
                {
                    options.EnableMessageLogging = true;
                })
                .AddDingTalk(options =>
                {
                    options.WebhookUrl = WebhookUrl;
                    options.WebhookSecret = WebhookSecret;
                    options.ClientID = ClientId;
                    options.ClientSecret = ClientSecret;
                    options.CorpId = CorpId;
                    options.AgentId = AgentId;
                    options.BaseUrl = "https://api.dingtalk.com";
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
                Content = "这是一条来自 Fang.Notification 集成测试的 DingTalk Webhook 文本消息。",
                Priority = MessagePriority.Normal
            };
            var receiver = MessageReceiver.FromWebhook(WebhookUrl);
            var result = await _notificationService.SendAsync("dingtalk", message, receiver);

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
                Content = "这是一条来自 Fang.Notification 集成测试的 DingTalk API 文本消息。",
                Priority = MessagePriority.Normal
            };

            var receiver = new MessageReceiver().AddIdentifier(ReceiverType.UserId, UserId);
            var result = await _notificationService.SendAsync("dingtalk", message, receiver);

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
            var result = await _notificationService.SendAsync("dingtalk", message, receiver);

            Assert.IsTrue(result.IsSuccess, $"Markdown 发送失败: {result.ErrorMessage}");
            Console.WriteLine($"Markdown 发送成功, MessageId: {result.RemoteMessageId}");
        }

        /// <summary>
        /// 文件消息 - 通过 API（UserId）发送，上传本地文件后发送工作通知
        /// </summary>
        [TestMethod]
        [TestCategory("Integration")]
        public async Task SendFileMessage_ByUserId_ShouldSucceed()
        {
            Assert.IsTrue(File.Exists(FilePath),
                $"测试文件不存在: {FilePath}");

            var message = new FileMessage
            {
                Title = "集成测试 - 文件",
                FileUrl = FilePath,
                FileName = "1.xlsx"
            };

            var receiver = new MessageReceiver().AddIdentifier(ReceiverType.UserId, UserId);
            var result = await _notificationService.SendAsync("dingtalk", message, receiver);

            Assert.IsTrue(result.IsSuccess, $"文件发送失败: {result.ErrorMessage}");
            Console.WriteLine($"文件发送成功, MessageId: {result.RemoteMessageId}");
        }

        /// <summary>
        /// 图片消息 - 通过 API（UserId）发送，读取本地图片转为 Base64 后上传
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
                Title = "集成测试 - 图片",
                ImageBase64 = imageBase64,
                Width = 0,
                Height = 0
            };

            var receiver = new MessageReceiver().AddIdentifier(ReceiverType.UserId, UserId);
            var result = await _notificationService.SendAsync("dingtalk", message, receiver);

            Assert.IsTrue(result.IsSuccess, $"图片发送失败: {result.ErrorMessage}");
            Console.WriteLine($"图片发送成功, MessageId: {result.RemoteMessageId}");
        }

        /// <summary>
        /// 文本消息 - 群聊发送（需要配置 ChatId，对应 oapi.dingtalk.com/chat/send 的 chatid）
        /// </summary>
        [TestMethod]
        [TestCategory("Integration")]
        public async Task SendTextMessage_ByGroupChat_ShouldSucceed()
        {
            if (string.IsNullOrEmpty(ChatId))
                Assert.Inconclusive("未配置 ChatId，跳过群聊测试");

            var message = new TextMessage
            {
                Title = "集成测试 - 群聊",
                Content = "这是一条来自 Fang.Notification 集成测试的 DingTalk 群聊文本消息。",
                Priority = MessagePriority.Normal
            };
            var receiver = MessageReceiver.FromGroupChatId(ChatId);
            var result = await _notificationService.SendAsync("dingtalk", message, receiver);

            Assert.IsTrue(result.IsSuccess, $"群聊文本发送失败: {result.ErrorMessage}");
            Console.WriteLine($"群聊文本发送成功");
        }

        /// <summary>
        /// 文件消息 - 群聊发送（上传后通过 chat/send 发送）
        /// </summary>
        [TestMethod]
        [TestCategory("Integration")]
        public async Task SendFileMessage_ByGroupChat_ShouldSucceed()
        {
            if (string.IsNullOrEmpty(ChatId))
                Assert.Inconclusive("未配置 ChatId，跳过群聊测试");

            Assert.IsTrue(File.Exists(FilePath),
                $"测试文件不存在: {FilePath}");

            var message = new FileMessage
            {
                Title = "集成测试 - 群聊文件",
                FileUrl = FilePath,
                FileName = "1.xlsx"
            };
            var receiver = MessageReceiver.FromGroupChatId(ChatId);
            var result = await _notificationService.SendAsync("dingtalk", message, receiver);

            Assert.IsTrue(result.IsSuccess, $"群聊文件发送失败: {result.ErrorMessage}");
            Console.WriteLine($"群聊文件发送成功");
        }

        /// <summary>
        /// 图片消息 - 群聊发送（上传后通过 chat/send 发送）
        /// </summary>
        [TestMethod]
        [TestCategory("Integration")]
        public async Task SendImageMessage_ByGroupChat_ShouldSucceed()
        {
            if (string.IsNullOrEmpty(ChatId))
                Assert.Inconclusive("未配置 ChatId，跳过群聊测试");

            Assert.IsTrue(File.Exists(ImagePath),
                $"测试图片不存在: {ImagePath}");

            byte[] imageBytes = File.ReadAllBytes(ImagePath);
            string imageBase64 = Convert.ToBase64String(imageBytes);

            var message = new ImageMessage
            {
                Title = "集成测试 - 群聊图片",
                ImageBase64 = imageBase64,
                Width = 0,
                Height = 0
            };
            var receiver = MessageReceiver.FromGroupChatId(ChatId);
            var result = await _notificationService.SendAsync("dingtalk", message, receiver);

            Assert.IsTrue(result.IsSuccess, $"群聊图片发送失败: {result.ErrorMessage}");
            Console.WriteLine($"群聊图片发送成功");
        }
    }
}
