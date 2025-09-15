using FileUploader.Contracts;
using NUnit.Framework;
using StackExchange.Redis;
using System.Text.Json;

namespace FileUploader_sTest
{
    public class FileUploaderSystemTests
    {
        private IConnectionMultiplexer _redis;
        private const string _connectionString = "localhost:6379";

        [OneTimeSetUp]
        public async Task Setup()
        {
            var configOptions = ConfigurationOptions.Parse(_connectionString);
            configOptions.ConnectTimeout = 5000;
            configOptions.SyncTimeout = 5000;
            configOptions.AbortOnConnectFail = false;
            configOptions.ConnectRetry = 3;
            configOptions.ReconnectRetryPolicy = new ExponentialRetry(1000, 5000);

            _redis =  await ConnectionMultiplexer.ConnectAsync(configOptions);
        }

        [Test]
        public async Task FileUpload_ShouldTriggerUpdate()
        {
            var sub = _redis.GetSubscriber();
            var receivedMessages = new List<string>();

            await sub.SubscribeAsync("upload:updates", (_, value) =>
            {
                try
                {
                    var upd = JsonSerializer.Deserialize<UploadUpdate>(value);
                    if (upd is null) return;

                    receivedMessages.Add(value);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing update: {ex.Message}");
                }
            });

            var pub = _redis.GetSubscriber();

            var uploadJob = new UploadJobBuilder()
                                .WithJobId(Guid.NewGuid())
                                .WithFilePath(Path.GetTempPath())
                                .WithFileName(Path.GetTempFileName())
                                .WithFileSize("100")
                                .WithAttempt(1)
                                .Build();


            var db = _redis.GetDatabase();
            await db.ListLeftPushAsync("upload:jobs", JsonSerializer.Serialize(uploadJob))
                .WaitAsync(TimeSpan.FromSeconds(5));

            await Task.Delay(2000); // wait for async update

            Assert.That(receivedMessages.Count(), Is.EqualTo(2));
        }
    }
}
