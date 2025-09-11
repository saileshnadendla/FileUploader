using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileUploader.Worker.Helpers
{
    public class RedisConnectionHelper : IRedisConnectionHelper
    {
        public IConnectionMultiplexer CreateConnection(string connectionString)
        {
            var configOptions = ConfigurationOptions.Parse(connectionString);
            configOptions.AbortOnConnectFail = false;
            configOptions.ConnectTimeout = 5000;
            configOptions.SyncTimeout = 5000;
            configOptions.ConnectRetry = 3;
            configOptions.ReconnectRetryPolicy = new ExponentialRetry(1000, 10000);

            return ConnectionMultiplexer.Connect(configOptions);
        }
    }
}
