using FileUploader.Worker.Helpers;
using FileUploader.Worker.Service;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace FileUploader.Worker
{
    public class Startup
    {
        public static async Task Main(string[] args)
        {
            var builder = Host.CreateApplicationBuilder(args);
            builder.Logging.ClearProviders();
            builder.Logging.AddSimpleConsole(o => o.SingleLine = true);

            var config = builder.Configuration;
            var redisHost = Environment.GetEnvironmentVariable("REDIS_HOST") ?? "localhost:6379";

            var configOptions = ConfigurationOptions.Parse(redisHost);
            configOptions.AbortOnConnectFail = false;
            configOptions.ConnectTimeout = 5000;
            configOptions.SyncTimeout = 5000;
            configOptions.ConnectRetry = 3;
            configOptions.ReconnectRetryPolicy = new ExponentialRetry(1000, 10000);

            var mux = await ConnectionMultiplexer.ConnectAsync(configOptions);

            builder.Services.AddSingleton<IConnectionMultiplexer>(mux);
            builder.Services.AddSingleton<IHttpClientHelper>(new HttpClientHelper());

            builder.Services.AddSingleton<IRedisHelper>(serviceProvider =>
            {
                var connectionMultiplexer = serviceProvider.GetRequiredService<IConnectionMultiplexer>();
                return new RedisHelper(connectionMultiplexer);
            });

            builder.Services.AddHostedService<WorkerService>();

            await builder.Build().RunAsync();
        }
    }
}