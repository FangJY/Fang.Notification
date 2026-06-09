using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Fang.Notification.Common.TokenManagement;
using Fang.Notification.Core.Abstractions;
using Fang.Notification.Core.Models;
using Fang.Notification.Feishu.Models;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Moq.Protected;

namespace Fang.Notification.Feishu.Tests
{
    [TestClass]
    public class FeishuChannelTests
    {
        private Mock<HttpMessageHandler> _mockHttp;
        private HttpClient _httpClient;
        private Mock<IOptions<FeishuOptions>> _mockOptions;
        private Mock<IJsonSerializer> _mockSerializer;
        private InMemoryTokenCache _tokenCache;
        private FeishuChannel _channel;

        private const string TestImagePath =
            @"C:\Users\cjy56\Pictures\Snipaste_2025-05-15_10-55-03.png";

        [TestInitialize]
        public void Setup()
        {
            _mockHttp = new Mock<HttpMessageHandler>();
            _httpClient = new HttpClient(_mockHttp.Object);
            _mockOptions = new Mock<IOptions<FeishuOptions>>();
            _mockSerializer = new Mock<IJsonSerializer>();
            _tokenCache = new InMemoryTokenCache();

            _mockOptions.Setup(o => o.Value).Returns(new FeishuOptions
            {
                AppId = "cli_aaa8db8520791be1",
                AppSecret = "gyNzeUUybLGlqYQobWHgwfScGFB02lY4",
                WebhookUrl = "https://open.feishu.cn/open-apis/bot/v2/hook/aeae6c46-8d30-4645-9208-880dbd1e6173",
                BaseUrl = "https://open.feishu.cn"
            });

            _channel = new FeishuChannel(
                _httpClient, _mockOptions.Object, _mockSerializer.Object, _tokenCache);
        }

        // ──────────────────────────────
        // Text message - Webhook
        // ──────────────────────────────

        [TestMethod]
        public async Task SendAsync_TextMessage_Webhook_ShouldSucceed()
        {
            var message = new TextMessage { Content = "Hello Feishu" };
            var receiver = MessageReceiver.FromWebhook(
                "https://open.feishu.cn/open-apis/bot/v2/hook/aeae6c46-8d30-4645-9208-880dbd1e6173");

            SetupHttpResponse(HttpStatusCode.OK,
                "{\"code\":0,\"data\":{\"message_id\":\"om_webhook_123\"}}");
            // Serialize is called with `object` (BuildWebhookMessage returns object)
            _mockSerializer
                .Setup(s => s.Serialize(It.IsAny<object>()))
                .Returns("{\"msg_type\":\"text\",\"content\":{\"text\":\"Hello Feishu\"}}");
            _mockSerializer
                .Setup(s => s.Deserialize<FeishuResponse>(It.IsAny<string>()))
                .Returns(new FeishuResponse { Code = 0, Data = new FeishuResponseData { MessageId = "om_webhook_123" } });

            var result = await _channel.SendAsync(message, receiver);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual("om_webhook_123", result.RemoteMessageId);
        }

        // ──────────────────────────────
        // Text message - API
        // ──────────────────────────────

        [TestMethod]
        public async Task SendAsync_TextMessage_Api_ShouldSucceed()
        {
            var message = new TextMessage { Content = "Hello via API" };
            var receiver = MessageReceiver.FromGroupChatId("oc_9bdf295b315929712b678ee36fb3fe5e");

            // HTTP: token endpoint
            SetupHttpResponseForPath("auth/v3/tenant_access_token", HttpStatusCode.OK,
                "{\"code\":0,\"tenant_access_token\":\"test_tenant_token\",\"expire\":7200}");
            // HTTP: message send endpoint
            SetupHttpResponseForPath("im/v1/messages", HttpStatusCode.OK,
                "{\"code\":0,\"data\":{\"message_id\":\"om_api_456\"}}");

            // Serialize catch-all (for anonymous types like auth body)
            _mockSerializer
                .Setup(s => s.Serialize(It.IsAny<object>()))
                .Returns("{}");
            // Final message body serialize (Dictionary<string, object>)
            _mockSerializer
                .Setup(s => s.Serialize(It.IsAny<Dictionary<string, object>>()))
                .Returns("{\"receive_id\":\"oc_test_group123\",\"msg_type\":\"text\",\"content\":\"{\\\"text\\\":\\\"Hello via API\\\"}\"}");
            // Token response
            _mockSerializer
                .Setup(s => s.Deserialize<FeishuTokenResponse>(It.IsAny<string>()))
                .Returns(new FeishuTokenResponse { Code = 0, TenantAccessToken = "test_tenant_token", Expire = 7200 });
            // Send response
            _mockSerializer
                .Setup(s => s.Deserialize<FeishuResponse>(It.IsAny<string>()))
                .Returns(new FeishuResponse { Code = 0, Data = new FeishuResponseData { MessageId = "om_api_456" } });

            var result = await _channel.SendAsync(message, receiver);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual("om_api_456", result.RemoteMessageId);
        }

        // ──────────────────────────────
        // Image message - from local file (Base64)
        // ──────────────────────────────

        [TestMethod]
        public async Task SendAsync_ImageMessage_FromLocalFile_ShouldSucceed()
        {
            // Assert the test image file exists
            Assert.IsTrue(File.Exists(TestImagePath),
                $"Test image not found at: {TestImagePath}");

            // Read the local image file as bytes, convert to base64
            byte[] imageBytes = File.ReadAllBytes(TestImagePath);
            string imageBase64 = Convert.ToBase64String(imageBytes);

            var message = new ImageMessage
            {
                ImageBase64 = imageBase64,
                Title = "本地图片上传测试"
            };
            var receiver = MessageReceiver.FromGroupChatId("oc_9bdf295b315929712b678ee36fb3fe5e");

            // HTTP: token
            SetupHttpResponseForPath("auth/v3/tenant_access_token", HttpStatusCode.OK,
                "{\"code\":0,\"tenant_access_token\":\"img_upload_token\",\"expire\":7200}");
            // HTTP: image upload
            SetupHttpResponseForPath("im/v1/images", HttpStatusCode.OK,
                "{\"code\":0,\"data\":{\"image_key\":\"img_uploaded_789\"}}");
            // HTTP: message send
            SetupHttpResponseForPath("im/v1/messages", HttpStatusCode.OK,
                "{\"code\":0,\"data\":{\"message_id\":\"om_img_101\"}}");

            // Serialize: catch-all for anonymous types
            _mockSerializer
                .Setup(s => s.Serialize(It.IsAny<object>()))
                .Returns("{}");
            // Final API message body (Dictionary<string, object>)
            _mockSerializer
                .Setup(s => s.Serialize(It.IsAny<Dictionary<string, object>>()))
                .Returns("{\"receive_id\":\"oc_img_group456\",\"msg_type\":\"image\",\"content\":\"{\\\"image_key\\\":\\\"img_uploaded_789\\\"}\"}");
            // Token response
            _mockSerializer
                .Setup(s => s.Deserialize<FeishuTokenResponse>(It.IsAny<string>()))
                .Returns(new FeishuTokenResponse { Code = 0, TenantAccessToken = "img_upload_token", Expire = 7200 });
            // Image upload response
            _mockSerializer
                .Setup(s => s.Deserialize<FeishuUploadResponse>(It.IsAny<string>()))
                .Returns(new FeishuUploadResponse
                {
                    Code = 0,
                    Data = new FeishuUploadData { ImageKey = "img_uploaded_789" }
                });
            // Send response
            _mockSerializer
                .Setup(s => s.Deserialize<FeishuResponse>(It.IsAny<string>()))
                .Returns(new FeishuResponse { Code = 0, Data = new FeishuResponseData { MessageId = "om_img_101" } });

            var result = await _channel.SendAsync(message, receiver);

            Assert.IsTrue(result.IsSuccess,
                $"Image upload + send should succeed. Error: {result.ErrorMessage}");
            Assert.AreEqual("om_img_101", result.RemoteMessageId);

            // Verify image upload HTTP call was actually made
            _mockHttp.Protected().Verify(
                "SendAsync",
                Times.Once(),
                ItExpr.Is<HttpRequestMessage>(r =>
                    r.RequestUri != null &&
                    r.RequestUri.ToString().Contains("im/v1/images")),
                ItExpr.IsAny<CancellationToken>());
        }

        // ──────────────────────────────
        // File message - from local path
        // ──────────────────────────────

        [TestMethod]
        public async Task SendAsync_FileMessage_FromLocalFile_ShouldSucceed()
        {
            var message = new FileMessage
            {
                FileUrl = @"C:\Users\cjy56\Desktop\考勤\1.xlsx",
                FileName = "Snipaste_2025-05-15_10-55-03.png"
            };
            var receiver = MessageReceiver.FromGroupChatId("oc_9bdf295b315929712b678ee36fb3fe5e");

            // HTTP: token
            SetupHttpResponseForPath("auth/v3/tenant_access_token", HttpStatusCode.OK,
                "{\"code\":0,\"tenant_access_token\":\"file_upload_token\",\"expire\":7200}");
            // HTTP: file upload
            SetupHttpResponseForPath("im/v1/files", HttpStatusCode.OK,
                "{\"code\":0,\"data\":{\"file_key\":\"file_uploaded_abc\"}}");
            // HTTP: message send
            SetupHttpResponseForPath("im/v1/messages", HttpStatusCode.OK,
                "{\"code\":0,\"data\":{\"message_id\":\"om_file_202\"}}");

            // Serialize: catch-all for anonymous types
            _mockSerializer
                .Setup(s => s.Serialize(It.IsAny<object>()))
                .Returns("{}");
            // Final API message body (Dictionary<string, object>)
            _mockSerializer
                .Setup(s => s.Serialize(It.IsAny<Dictionary<string, object>>()))
                .Returns("{\"receive_id\":\"oc_file_group789\",\"msg_type\":\"file\",\"content\":\"{\\\"file_key\\\":\\\"file_uploaded_abc\\\"}\"}");
            // Token response
            _mockSerializer
                .Setup(s => s.Deserialize<FeishuTokenResponse>(It.IsAny<string>()))
                .Returns(new FeishuTokenResponse { Code = 0, TenantAccessToken = "file_upload_token", Expire = 7200 });
            // File upload response
            _mockSerializer
                .Setup(s => s.Deserialize<FeishuUploadResponse>(It.IsAny<string>()))
                .Returns(new FeishuUploadResponse
                {
                    Code = 0,
                    Data = new FeishuUploadData { FileKey = "file_uploaded_abc" }
                });
            // Send response
            _mockSerializer
                .Setup(s => s.Deserialize<FeishuResponse>(It.IsAny<string>()))
                .Returns(new FeishuResponse { Code = 0, Data = new FeishuResponseData { MessageId = "om_file_202" } });

            var result = await _channel.SendAsync(message, receiver);

            Assert.IsTrue(result.IsSuccess,
                $"File upload + send should succeed. Error: {result.ErrorMessage}");
            Assert.AreEqual("om_file_202", result.RemoteMessageId);

            // Verify file upload HTTP call was actually made
            _mockHttp.Protected().Verify(
                "SendAsync",
                Times.Once(),
                ItExpr.Is<HttpRequestMessage>(r =>
                    r.RequestUri != null &&
                    r.RequestUri.ToString().Contains("im/v1/files")),
                ItExpr.IsAny<CancellationToken>());
        }

        // ──────────────────────────────
        // Unsupported message type
        // ──────────────────────────────

        [TestMethod]
        public async Task SendAsync_UnsupportedType_ShouldReturnFailure()
        {
            // NewsMessage is NOT in FeishuChannel.SupportedTypes
            var message = new NewsMessage { Content = "News content" };
            var receiver = MessageReceiver.FromWebhook("https://open.feishu.cn/open-apis/bot/v2/hook/aeae6c46-8d30-4645-9208-880dbd1e6173");

            var result = await _channel.SendAsync(message, receiver);

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual("UNSUPPORTED_TYPE", result.ErrorCode);
        }

        // ──────────────────────────────
        // Helpers
        // ──────────────────────────────

        private void SetupHttpResponse(HttpStatusCode statusCode, string responseBody)
        {
            _mockHttp
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(statusCode)
                {
                    Content = new StringContent(responseBody)
                });
        }

        private void SetupHttpResponseForPath(
            string pathSubstring, HttpStatusCode statusCode, string responseBody)
        {
            _mockHttp
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(r =>
                        r.RequestUri != null &&
                        r.RequestUri.ToString().Contains(pathSubstring)),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(statusCode)
                {
                    Content = new StringContent(responseBody)
                });
        }
    }
}
