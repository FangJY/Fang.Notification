using System.Threading.Tasks;
using Fang.Notification.Core.Models;
using Fang.Notification.Core.Middleware;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Fang.Notification.Core.Tests
{
    [TestClass]
    public class MessagePipelineTests
    {
        [TestMethod]
        public async Task Pipeline_WithSingleMiddleware_ShouldExecuteSuccessfully()
        {
            var message = new TextMessage { Content = "Hello" };
            var receiver = MessageReceiver.FromWebhook("https://hook.test");

            var pipeline = new MessagePipelineBuilder()
                .SetFinalHandler((msg, rec) => Task.FromResult(
                    SendResult.Success("test", msg.MessageId)))
                .Build();

            var result = await pipeline.ExecuteAsync(message, receiver);

            Assert.IsTrue(result.IsSuccess);
        }
    }
}
