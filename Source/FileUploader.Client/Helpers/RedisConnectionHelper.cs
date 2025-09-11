using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileUploader.Client.Helpers
{
    internal class RedisConnectionHelper : IRedisConnectionHelper
    {
        public async Task<IConnectionMultiplexer> CreateConnectionAsync(string connectionString)
        {
            var configOptions = ConfigurationOptions.Parse(connectionString);
            configOptions.ConnectTimeout = 5000;
            configOptions.SyncTimeout = 5000;
            configOptions.AbortOnConnectFail = false;
            configOptions.ConnectRetry = 3;
            configOptions.ReconnectRetryPolicy = new ExponentialRetry(1000, 5000);

            return await ConnectionMultiplexer.ConnectAsync(configOptions);
        }
    }
}
