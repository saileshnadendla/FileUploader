using FileUploader.Contracts;
using FileUploader.Worker.Helpers;
using Moq;
using NUnit.Framework;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace FileUploader.Worker_uTest.Helper
{
    internal class RedisHelperTests
    {
        private Mock<IConnectionMultiplexer> _mockConnection;
        private Mock<IDatabase> _mockDb;
        private Mock<ISubscriber> _mockSubscriber;
        private Mock<IRedisConnectionHelper> _mockHelper;

        [SetUp]
        public void Setup()
        {
            _mockConnection = new Mock<IConnectionMultiplexer>();
            _mockDb = new Mock<IDatabase>();
            _mockSubscriber = new Mock<ISubscriber>();
            _mockHelper = new Mock<IRedisConnectionHelper>();

            _mockConnection.Setup(c => c.IsConnected).Returns(true);
            _mockConnection.Setup(c => c.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
                           .Returns(_mockDb.Object);
            _mockConnection.Setup(c => c.GetSubscriber(It.IsAny<object>()))
                           .Returns(_mockSubscriber.Object);

            _mockHelper.Setup(h => h.CreateConnection(It.IsAny<string>()))
                       .Returns(_mockConnection.Object);
        }

        [Test]
        public async Task GetUploadJob_ShouldReturnJob_WhenJobExists()
        {
            // Arrange
            var guid = Guid.NewGuid();
            var fileName = Path.GetTempFileName();
            var uploadJob = new UploadJobBuilder()
                                .WithJobId(guid)
                                .WithFilePath(Path.GetTempPath())
                                .WithFileName(fileName)
                                .WithFileSize("100")
                                .WithAttempt(1)
                                .Build();
            var serialized = JsonSerializer.Serialize(uploadJob);

            _mockDb.Setup(db => db.ListRightPopAsync("upload:jobs", It.IsAny<CommandFlags>()))
                   .ReturnsAsync(serialized);

            var helper = new RedisHelper(_mockConnection.Object, _mockHelper.Object);

            // Act
            var result = await helper.GetUploadJob();

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(guid, result.JobId);
            Assert.AreEqual(fileName, result.FileName);
        }

        [Test]
        public async Task GetUploadJob_ShouldReturnNull_WhenQueueEmpty()
        {
            // Arrange
            _mockDb.Setup(db => db.ListRightPopAsync("upload:jobs", It.IsAny<CommandFlags>()))
                   .ReturnsAsync(RedisValue.Null);

            var helper = new RedisHelper(_mockConnection.Object, _mockHelper.Object);

            // Act
            var result = await helper.GetUploadJob();

            // Assert
            Assert.IsNull(result);
        }

        [Test]
        public async Task PushToRedis_ShouldReturnTrue_WhenSuccessful()
        {
            // Arrange
            var helper = new RedisHelper(_mockConnection.Object, _mockHelper.Object);

            // Act
            var result = await helper.PushToRedis("upload:jobs", "payload");

            // Assert
            Assert.IsTrue(result);
            _mockDb.Verify(db => db.ListLeftPushAsync("upload:jobs", "payload",
                            It.IsAny<When>(), It.IsAny<CommandFlags>()), Times.Once);
        }

        [Test]
        public async Task PushToRedis_ShouldRetryAndFail_WhenRedisDisconnected()
        {
            // Arrange
            _mockConnection.Setup(c => c.IsConnected).Returns(false);

            var helper = new RedisHelper(_mockConnection.Object, _mockHelper.Object);

            // Act
            var result = await helper.PushToRedis("upload:jobs", "payload");

            // Assert
            Assert.IsFalse(result);
            _mockDb.Verify(db => db.ListLeftPushAsync(It.IsAny<RedisKey>(),
                            It.IsAny<RedisValue>(), It.IsAny<When>(), It.IsAny<CommandFlags>()), Times.Never);
        }

        [Test]
        public async Task PublishToRedis_ShouldReturnTrue_WhenSuccessful()
        {
            // Arrange
            var update = new UploadUpdate(Guid.NewGuid(), "FileName", UploadStatusKind.Completed, 100, "100");
            _mockSubscriber.Setup(s => s.PublishAsync("upload:updates",
                            It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()))
                           .ReturnsAsync(1);

            var helper = new RedisHelper(_mockConnection.Object, _mockHelper.Object);

            // Act
            var result = await helper.PublishToRedis(update);

            // Assert
            Assert.IsTrue(result);
            _mockSubscriber.Verify(s => s.PublishAsync("upload:updates",
                It.IsAny<RedisValue>(),
                It.IsAny<CommandFlags>()), Times.Once);
        }

        [Test]
        public async Task PublishToRedis_ShouldRetryAndFail_WhenRedisDisconnected()
        {
            // Arrange
            _mockConnection.Setup(c => c.IsConnected).Returns(false);

            var helper = new RedisHelper(_mockConnection.Object, _mockHelper.Object);
            var update = new UploadUpdate(Guid.NewGuid(), "FileName", UploadStatusKind.Failed, 100, "100");

            // Act
            var result = await helper.PublishToRedis(update);

            // Assert
            Assert.IsFalse(result);
            _mockSubscriber.Verify(s => s.PublishAsync(It.IsAny<RedisChannel>(),
                It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()), Times.Never);
        }

        [Test]
        public void Dispose_ShouldDisposeConnection()
        {
            // Arrange
            var helper = new RedisHelper(_mockConnection.Object, _mockHelper.Object);

            // Act
            helper.Dispose();

            // Assert
            _mockConnection.Verify(c => c.Dispose(), Times.Once);
        }
    }
}
