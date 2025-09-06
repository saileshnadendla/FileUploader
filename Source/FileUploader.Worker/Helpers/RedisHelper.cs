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
        private readonly IConnectionMultiplexer _redis;

        public RedisHelper(IConnectionMultiplexer redis)
        {
            _redis = redis;
        }

        public async Task<UploadJob> GetUploadJob()
        {
            var db = _redis.GetDatabase();
            var job = await db.ListRightPopAsync("upload:jobs");

            if (job.IsNullOrEmpty)
            {
                return null;
            }

            return JsonSerializer.Deserialize<UploadJob>(job.ToString());
        }

        public async Task PushToRedis(string key, string job)
        {
            var db = _redis.GetDatabase();
            await db.ListLeftPushAsync(key, job);
        }

        public void PublishToRedis(UploadUpdate update)
        {
            var sub = _redis.GetSubscriber();
            sub.PublishAsync("upload:updates", JsonSerializer.Serialize(update));
        }
    }
}
