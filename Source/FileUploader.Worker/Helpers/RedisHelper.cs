using FileUploader.Contracts;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace FileUploader.Worker.Helpers
{
    public class RedisHelper : IRedisHelper
    {
        private IConnectionMultiplexer _redis;
        private IRedisConnectionHelper _RedisConnectionHelper;
        private const string _connectionString = "localhost:6379";
        private readonly object _lockObject = new object();

        public RedisHelper(IConnectionMultiplexer redis) : this(redis, new RedisConnectionHelper())
        {
        }

        public RedisHelper(IConnectionMultiplexer redis, IRedisConnectionHelper helper)
        {
            _redis = redis ?? throw new ArgumentNullException(nameof(redis));
            _RedisConnectionHelper = helper;
        }

        public async Task<UploadJob> GetUploadJob()
        {
            const int maxRetries = 3;
            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                try
                {
                    if (!EnsureConnectionAsync())
                    {
                        Console.WriteLine("Worker Redis is not connected, skipping job check");
                        await Task.Delay(1000 * (attempt + 1));
                        continue;
                    }

                    var db = _redis.GetDatabase();
                    var job = await db.ListRightPopAsync("upload:jobs");

                    if (job.IsNullOrEmpty)
                    {
                        if (attempt == 0)
                            Console.WriteLine("Worker: No jobs in queue");
                        return null;
                    }

                    var uploadJob = JsonSerializer.Deserialize<UploadJob>(job.ToString());
                    Console.WriteLine($"Worker picked up job: {uploadJob?.JobId} - {uploadJob?.FileName}");
                    return uploadJob;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Worker attempt {attempt + 1} failed to get upload job: {ex.Message}");
                    if (attempt < maxRetries - 1)
                    {
                        await Task.Delay(1000 * (attempt + 1));
                    }
                }
            }

            return null;
        }

        public async Task<bool> PushToRedis(string key, string job)
        {
            const int maxRetries = 3;

            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                try
                {
                    if (!EnsureConnectionAsync())
                    {
                        await Task.Delay(1000 * (attempt + 1));
                        continue;
                    }

                    var db = _redis.GetDatabase();
                    await db.ListLeftPushAsync(key, job);
                    Console.WriteLine($"Worker successfully pushed to {key}");
                    return true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Worker attempt {attempt + 1} failed to push to Redis key {key}: {ex.Message}");
                    if (attempt < maxRetries - 1)
                    {
                        await Task.Delay(1000 * (attempt + 1));
                    }
                }
            }

            return false;
        }

        public async Task<bool> PublishToRedis(UploadUpdate update)
        {
            const int maxRetries = 3;

            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                try
                {
                    if (!EnsureConnectionAsync())
                    {
                        await Task.Delay(1000 * (attempt + 1));
                        continue;
                    }

                    var sub = _redis.GetSubscriber();
                    var message = JsonSerializer.Serialize(update);
                    var result = await sub.PublishAsync("upload:updates", message);

                    Console.WriteLine($"Worker published update for job {update.JobId} - Status: {update.Status} - Subscribers: {result}");
                    return true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Worker attempt {attempt + 1} failed to publish to Redis: {ex.Message}");
                    if (attempt < maxRetries - 1)
                    {
                        await Task.Delay(1000 * (attempt + 1));
                    }
                }
            }

            return false;
        }

        public void Dispose()
        {
            _redis?.Dispose();
        }

        private bool EnsureConnectionAsync()
        {
            try
            {
                if (_redis != null && _redis.IsConnected)
                    return true;

                lock (_lockObject)
                {
                    if (_redis == null || !_redis.IsConnected)
                    {
                        Console.WriteLine("Worker Redis connection lost, attempting to reconnect...");
                        _redis?.Dispose();

                        _redis = _RedisConnectionHelper.CreateConnection(_connectionString);

                        if (_redis.IsConnected)
                        {
                            Console.WriteLine("Worker Redis connection restored successfully");
                        }
                        else
                        {
                            Console.WriteLine("Worker Redis connection attempt completed but not connected yet (will retry in background)");
                        }
                    }
                }

                return _redis.IsConnected;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to ensure Worker Redis connection: {ex.Message}");
                return false;
            }
        }
    }
}