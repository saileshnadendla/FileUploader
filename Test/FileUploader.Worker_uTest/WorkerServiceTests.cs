using FileUploader.Contracts;
using FileUploader.Worker.Helpers;
using FileUploader.Worker.Service;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using System.Text.Json;

namespace FileUploader.Worker_uTest
{
    [TestFixture]
    internal class WorkerServiceTests
    {
        private Mock<ILogger<WorkerService>> _loggerMock;
        private Mock<IHttpClientHelper> _httpClientHelper;
        private Mock<IRedisHelper> _redisHelper;

        [SetUp]
        public void Setup()
        {
            _loggerMock = new Mock<ILogger<WorkerService>>();
            _httpClientHelper = new Mock<IHttpClientHelper>();
            _redisHelper = new Mock<IRedisHelper>();
        }

        [Test]
        public async Task ExecuteAsync_JobSuccessful_JobPostedToRedis()
        {
            // Arrange
            UploadJob? completedJob = null;
            var progressJobs = new List<UploadUpdate>();

            var uploadJob = new UploadJobBuilder()
                                .WithJobId(Guid.NewGuid())
                                .WithFilePath(Path.GetTempPath())
                                .WithFileName(Path.GetTempFileName())
                                .WithFileSize("100")
                                .WithAttempt(1)
                                .Build();

            var jobQueue = new Queue<UploadJob?>();
            jobQueue.Enqueue(uploadJob);
            jobQueue.Enqueue(null);

            _redisHelper.Setup(x => x.GetUploadJob())
                        .ReturnsAsync(() => jobQueue.Count > 0 ? jobQueue.Dequeue() : null);

            _httpClientHelper.Setup(x => x.HttpClientPostAsync(It.IsAny<UploadJob>()))
                             .ReturnsAsync(true);

            _redisHelper.Setup(x => x.PushToRedis("upload:completedjobs", It.IsAny<string>()))
                        .Callback((string key, string uploadjob) =>
                        {
                            completedJob = JsonSerializer.Deserialize<UploadJob>(uploadjob);
                        });

            _redisHelper.Setup(x => x.PublishToRedis(It.IsAny<UploadUpdate>()))
                        .Callback((UploadUpdate update) => progressJobs.Add(update));

            var sut = new WorkerService(_loggerMock.Object, _httpClientHelper.Object, _redisHelper.Object);

            // Act
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await sut.StartAsync(cts.Token);

            // Assert
            Assert.That(progressJobs.Count, Is.EqualTo(2));
            Assert.True(progressJobs.All(x => x.JobId == uploadJob.JobId));
            Assert.That(progressJobs[0].Status, Is.EqualTo(UploadStatusKind.InProgress));
            Assert.That(progressJobs[1].Status, Is.EqualTo(UploadStatusKind.Completed));
            Assert.NotNull(completedJob);
            _redisHelper.Verify(x => x.PushToRedis("upload:completedjobs", It.IsAny<string>()), Times.Once);
            Assert.That(completedJob?.JobId, Is.EqualTo(uploadJob.JobId));
        }

        [Test]
        public async Task ExecuteAsync_JobSuccessfulOnRetry_JobPostedToRedis()
        {
            // Arrange
            UploadJob? completedJob = null;
            UploadJob? failedJob = null;
            var progressJobs = new List<UploadUpdate>();

            var uploadJob = new UploadJobBuilder()
                                .WithJobId(Guid.NewGuid())
                                .WithFilePath(Path.GetTempPath())
                                .WithFileName(Path.GetTempFileName())
                                .WithFileSize("100")
                                .WithAttempt(1)
                                .Build();

            var jobQueue = new Queue<UploadJob?>();
            jobQueue.Enqueue(uploadJob);
            jobQueue.Enqueue(uploadJob);
            jobQueue.Enqueue(null);

            _redisHelper.Setup(x => x.GetUploadJob())
                        .ReturnsAsync(() => jobQueue.Count > 0 ? jobQueue.Dequeue() : null);

            _httpClientHelper.SetupSequence(x => x.HttpClientPostAsync(It.IsAny<UploadJob>()))
                             .ReturnsAsync(false)
                             .ReturnsAsync(true); 

            _redisHelper.Setup(x => x.PushToRedis("upload:jobs", It.IsAny<string>()))
                        .Callback((string key, string uploadjob) =>
                        {
                            failedJob = JsonSerializer.Deserialize<UploadJob>(uploadjob);
                        }).ReturnsAsync(true);

            _redisHelper.Setup(x => x.PushToRedis("upload:completedjobs", It.IsAny<string>()))
                        .Callback((string key, string uploadjob) =>
                        {
                            completedJob = JsonSerializer.Deserialize<UploadJob>(uploadjob);
                        });

            _redisHelper.Setup(x => x.PublishToRedis(It.IsAny<UploadUpdate>()))
                        .Callback((UploadUpdate update) => progressJobs.Add(update));

            var sut = new WorkerService(_loggerMock.Object, _httpClientHelper.Object, _redisHelper.Object);

            // Act
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await sut.StartAsync(cts.Token);

            // Assert
            Assert.That(failedJob?.JobId, Is.EqualTo(uploadJob.JobId));
            Assert.That(failedJob?.Attempt, Is.EqualTo(2));
            Assert.That(progressJobs.Count, Is.EqualTo(4));
            Assert.True(progressJobs.All(x => x.JobId == uploadJob.JobId));
            Assert.That(progressJobs[0].Status, Is.EqualTo(UploadStatusKind.InProgress));
            Assert.That(progressJobs[1].Status, Is.EqualTo(UploadStatusKind.Queued));
            Assert.That(progressJobs[2].Status, Is.EqualTo(UploadStatusKind.InProgress));
            Assert.That(progressJobs[3].Status, Is.EqualTo(UploadStatusKind.Completed));
            Assert.NotNull(completedJob);
            _redisHelper.Verify(x => x.PushToRedis("upload:completedjobs", It.IsAny<string>()), Times.Once);
            Assert.That(completedJob?.JobId, Is.EqualTo(uploadJob.JobId));
        }

        [Test]
        public async Task ExecuteAsync_JobExceededRetries_JobFailurePostedToRedis()
        {
            // Arrange
            UploadJob? completedJob = null;
            UploadJob? failedJob = null;
            var progressJobs = new List<UploadUpdate>();

            var uploadJob = new UploadJobBuilder()
                                .WithJobId(Guid.NewGuid())
                                .WithFilePath(Path.GetTempPath())
                                .WithFileName(Path.GetTempFileName())
                                .WithFileSize("100")
                                .WithAttempt(3)
                                .Build();

            var jobQueue = new Queue<UploadJob?>();
            jobQueue.Enqueue(uploadJob);
            jobQueue.Enqueue(null);

            _redisHelper.Setup(x => x.GetUploadJob())
                        .ReturnsAsync(() => jobQueue.Count > 0 ? jobQueue.Dequeue() : null);

            _httpClientHelper.Setup(x => x.HttpClientPostAsync(It.IsAny<UploadJob>()))
                             .ReturnsAsync(false);

            _redisHelper.Setup(x => x.PushToRedis("upload:jobs:dlq", It.IsAny<string>()))
                        .Callback((string key, string uploadjob) =>
                        {
                            failedJob = JsonSerializer.Deserialize<UploadJob>(uploadjob);
                        });

            _redisHelper.Setup(x => x.PushToRedis("upload:completedjobs", It.IsAny<string>()))
                        .Callback((string key, string uploadjob) =>
                        {
                            completedJob = JsonSerializer.Deserialize<UploadJob>(uploadjob);
                        });

            _redisHelper.Setup(x => x.PublishToRedis(It.IsAny<UploadUpdate>()))
                        .Callback((UploadUpdate update) => progressJobs.Add(update));

            var sut = new WorkerService(_loggerMock.Object, _httpClientHelper.Object, _redisHelper.Object);

            // Act
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await sut.StartAsync(cts.Token);

            // Assert
            Assert.That(failedJob?.JobId, Is.EqualTo(uploadJob.JobId));
            Assert.That(failedJob?.Attempt, Is.EqualTo(3));
            Assert.That(progressJobs.Count, Is.EqualTo(2));
            Assert.True(progressJobs.All(x => x.JobId == uploadJob.JobId));
            Assert.That(progressJobs[0].Status, Is.EqualTo(UploadStatusKind.InProgress));
            Assert.That(progressJobs[1].Status, Is.EqualTo(UploadStatusKind.Failed));
            Assert.NotNull(completedJob);
            _redisHelper.Verify(x => x.PushToRedis("upload:completedjobs", It.IsAny<string>()), Times.Once);
            Assert.That(completedJob?.JobId, Is.EqualTo(uploadJob.JobId));
        }

        [Test]
        public async Task ExecuteAsync_HttpClientException_JobFailurePostedToRedis()
        {
            // Arrange
            UploadJob? completedJob = null;
            UploadJob? failedJob = null;
            var progressJobs = new List<UploadUpdate>();

            var uploadJob = new UploadJobBuilder()
                                .WithJobId(Guid.NewGuid())
                                .WithFilePath(Path.GetTempPath())
                                .WithFileName(Path.GetTempFileName())
                                .WithFileSize("100")
                                .WithAttempt(1)
                                .Build();

            var jobQueue = new Queue<UploadJob?>();
            jobQueue.Enqueue(uploadJob);
            jobQueue.Enqueue(uploadJob);
            jobQueue.Enqueue(null);

            _redisHelper.Setup(x => x.GetUploadJob())
                        .ReturnsAsync(() => jobQueue.Count > 0 ? jobQueue.Dequeue() : null);

            _httpClientHelper.SetupSequence(x => x.HttpClientPostAsync(It.IsAny<UploadJob>()))
                             .ThrowsAsync(new Exception())
                             .ReturnsAsync(true);

            _redisHelper.Setup(x => x.PushToRedis("upload:jobs", It.IsAny<string>()))
                        .Callback((string key, string uploadjob) =>
                        {
                            failedJob = JsonSerializer.Deserialize<UploadJob>(uploadjob);
                        }).ReturnsAsync(true);

            _redisHelper.Setup(x => x.PushToRedis("upload:completedjobs", It.IsAny<string>()))
                        .Callback((string key, string uploadjob) =>
                        {
                            completedJob = JsonSerializer.Deserialize<UploadJob>(uploadjob);
                        });

            _redisHelper.Setup(x => x.PublishToRedis(It.IsAny<UploadUpdate>()))
                        .Callback((UploadUpdate update) => progressJobs.Add(update));

            var sut = new WorkerService(_loggerMock.Object, _httpClientHelper.Object, _redisHelper.Object);

            // Act
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await sut.StartAsync(cts.Token);

            // Assert
            Assert.That(failedJob?.JobId, Is.EqualTo(uploadJob.JobId));
            Assert.That(failedJob?.Attempt, Is.EqualTo(2));
            Assert.That(progressJobs.Count, Is.EqualTo(5));
            Assert.True(progressJobs.All(x => x.JobId == uploadJob.JobId));
            Assert.That(progressJobs[0].Status, Is.EqualTo(UploadStatusKind.InProgress));
            Assert.That(progressJobs[1].Status, Is.EqualTo(UploadStatusKind.Failed));
            Assert.That(progressJobs[2].Status, Is.EqualTo(UploadStatusKind.Queued));
            Assert.That(progressJobs[3].Status, Is.EqualTo(UploadStatusKind.InProgress));
            Assert.That(progressJobs[4].Status, Is.EqualTo(UploadStatusKind.Completed));
            Assert.NotNull(completedJob);
            _redisHelper.Verify(x => x.PushToRedis("upload:completedjobs", It.IsAny<string>()), Times.Once);
            Assert.That(completedJob?.JobId, Is.EqualTo(uploadJob.JobId));
        }
    }
}
