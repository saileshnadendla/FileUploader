using FileUploader.Contracts;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

namespace FileUploader.Client.Helpers
{
    internal class RedisHelper : IRedisHelper
    {
        private ConnectionMultiplexer _redis;
        private ISubscriber _sub;

        public event EventHandler<UploadUpdate> UpdateAvailable;

        public async Task<List<string>> GetCompletedJobs()
        {
            if (_redis == null)
            {
                _redis = await ConnectionMultiplexer.ConnectAsync("localhost:6379");
            }

            var db = _redis.GetDatabase();
            var items = await db.ListRangeAsync("upload:completedjobs", 0, -1);

            return items.Select(x => (string)x).ToList();
        }

        public async Task PushToRedis(string payload)
        {
            if (_redis == null)
            {
                _redis = await ConnectionMultiplexer.ConnectAsync("localhost:6379");
            }

            var db = _redis.GetDatabase();
            await db.ListLeftPushAsync("upload:jobs", payload);
        }

        public async Task SubscribeToJobStatus()
        {
            if (_redis == null)
            {
                _redis = await ConnectionMultiplexer.ConnectAsync("localhost:6379");
            }

            _sub = _redis.GetSubscriber();
            await _sub.SubscribeAsync("upload:updates", (_, value) =>
            {
                try
                {
                    var upd = JsonSerializer.Deserialize<UploadUpdate>(value);
                    if (upd is null) return;
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        UpdateAvailable?.Invoke(this, upd);
                    });
                }
                catch { }
            });
        }
    }
}
