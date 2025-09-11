using FileUploader.Client.Helpers;
using FileUploader.Contracts;
using Moq;
using NUnit.Framework;
using StackExchange.Redis;
using System.Text.Json;

namespace FileUploader.Client_uTest.Helper
{
    [TestFixture]
    public class RedisHelperTests
    {
        private Mock<IRedisConnectionHelper> _mockConnectionHelper;
        private Mock<IConnectionMultiplexer> _mockConnection;
        private Mock<IDatabase> _mockDb;
        private Mock<ISubscriber> _mockSubscriber;

        [SetUp]
        public void Setup()
        {
            _mockConnectionHelper = new Mock<IRedisConnectionHelper>();
            _mockConnection = new Mock<IConnectionMultiplexer>();
            _mockDb = new Mock<IDatabase>();
            _mockSubscriber = new Mock<ISubscriber>();

            _mockConnection.Setup(x => x.IsConnected).Returns(true);
            _mockConnection.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
                           .Returns(_mockDb.Object);
            _mockConnection.Setup(x => x.GetSubscriber(It.IsAny<object>()))
                           .Returns(_mockSubscriber.Object);

            _mockConnectionHelper.Setup(x => x.CreateConnectionAsync(It.IsAny<string>()))
                .ReturnsAsync(_mockConnection.Object);
        }

        [Test]
        public async Task PushToRedis_ShouldPush_WhenRedisAvailable()
        {
            // Arrange
            var helper = new RedisHelper(_mockConnectionHelper.Object);
            var payload = "test-payload";

            // Act
            var result = await helper.PushToRedis(payload);

            // Assert
            Assert.IsTrue(result);
            _mockDb.Verify(x => x.ListLeftPushAsync("upload:jobs", payload, It.IsAny<When>(), It.IsAny<CommandFlags>()),
                           Times.Once);
        }

        [Test]
        public async Task PushToRedis_ShouldQueueOffline_WhenRedisUnavailable()
        {
            // Arrange
            _mockConnection.Setup(x => x.IsConnected).Returns(false);
            var helper = new RedisHelper(_mockConnectionHelper.Object);

            // Act
            var result = await helper.PushToRedis("offline-payload");

            // Assert
            Assert.IsTrue(result);
            Assert.AreEqual(1, helper.GetOfflineQueueCount());
        }

        [Test]
        public async Task GetCompletedJobs_ShouldReturnList_WhenItemsExist()
        {
            // Arrange
            var redisValues = new RedisValue[] { "job1", "job2" };
            _mockDb.Setup(x => x.ListRangeAsync("upload:completedjobs", 0, -1, It.IsAny<CommandFlags>()))
                   .ReturnsAsync(redisValues);

            var helper = new RedisHelper(_mockConnectionHelper.Object);

            // Act
            var jobs = await helper.GetCompletedJobs();

            // Assert
            Assert.AreEqual(2, jobs.Count);
            CollectionAssert.AreEquivalent(new List<string> { "job1", "job2" }, jobs);
        }

        [Test]
        public async Task GetCompletedJobs_ShouldReturnEmpty_WhenRedisUnavailable()
        {
            // Arrange
            _mockConnection.Setup(x => x.IsConnected).Returns(false);
            var helper = new RedisHelper(_mockConnectionHelper.Object);

            // Act
            var jobs = await helper.GetCompletedJobs();

            // Assert
            Assert.IsEmpty(jobs);
        }

        [Test]
        public async Task SubscribeToJobStatus_ShouldRaiseEvent_WhenMessageReceived()
        {
            // Arrange
            var helper = new RedisHelper(_mockConnectionHelper.Object);
            UploadUpdate receivedUpdate = null;
            var update = new UploadUpdate(Guid.NewGuid(), "FileName", UploadStatusKind.Completed, 100, "100");
            var serialized = JsonSerializer.Serialize(update);

            helper.UpdateAvailable += (s, e) => { receivedUpdate = e; };

            _mockSubscriber.Setup(x => x.SubscribeAsync(
                    "upload:updates",
                    It.IsAny<Action<RedisChannel, RedisValue>>(),
                    It.IsAny<CommandFlags>()))
                .Callback<RedisChannel, Action<RedisChannel, RedisValue>, CommandFlags>((ch, handler, flags) =>
                {
                    handler(ch, serialized);
                });

            // Act
            await helper.SubscribeToJobStatus();

            // Assert
            Assert.IsNotNull(receivedUpdate);
            Assert.AreEqual(update.JobId, receivedUpdate.JobId);
            Assert.AreEqual("Completed", receivedUpdate.Status.ToString());
        }

        [Test]
        public void Dispose_ShouldCleanUp()
        {
            // Arrange
            var helper = new RedisHelper(_mockConnectionHelper.Object);

            // Act
            helper.Dispose();

            // Assert
            Assert.DoesNotThrow(() => helper.Dispose());
        }
    }
}
