using FileUploader.Contracts;
using StackExchange.Redis;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace FileUploader.Client.Helpers
{
    internal class RedisHelper : IRedisHelper
    {
        private ConnectionMultiplexer _redis;
        private ISubscriber _sub;
        private readonly object _lockObject = new object();
        private readonly string _connectionString = "localhost:6379";
        private bool _isConnecting = false;
        private bool _isSubscribed = false;
        private bool _disposed = false;

        private readonly ConcurrentQueue<string> _offlineQueue = new ConcurrentQueue<string>();
        private readonly Timer _retryTimer;
        private readonly Timer _subscriptionCheckTimer;
        private bool _isProcessingOfflineQueue = false;

        public event EventHandler<UploadUpdate> UpdateAvailable;

        public RedisHelper()
        {
            _retryTimer = new Timer(ProcessOfflineQueue, null, TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(3));
            _subscriptionCheckTimer = new Timer(CheckSubscriptionHealth, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));

            _ = Task.Run(async () => await EnsureConnectionAsync());
        }

        private async Task<bool> EnsureConnectionAsync()
        {
            try
            {
                if (_disposed) return false;

                if (_redis != null && _redis.IsConnected)
                    return true;

                if (_isConnecting)
                {
                    var waitCount = 0;
                    while (_isConnecting && waitCount < 100)
                    {
                        await Task.Delay(100);
                        waitCount++;
                    }
                    return _redis != null && _redis.IsConnected;
                }

                lock (_lockObject)
                {
                    if (_isConnecting)
                        return false;
                    _isConnecting = true;
                }

                try
                {
                    Console.WriteLine("Attempting to connect to Redis...");

                    if (_redis != null)
                    {
                        try
                        {
                            _redis.ConnectionRestored -= OnConnectionRestored;
                            _redis.ConnectionFailed -= OnConnectionFailed;
                            _redis.Dispose();
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error disposing old Redis connection: {ex.Message}");
                        }
                        _redis = null;
                        _sub = null;
                        _isSubscribed = false;
                    }

                    var configOptions = ConfigurationOptions.Parse(_connectionString);
                    configOptions.ConnectTimeout = 5000;
                    configOptions.SyncTimeout = 5000;
                    configOptions.AbortOnConnectFail = false;
                    configOptions.ConnectRetry = 3;
                    configOptions.ReconnectRetryPolicy = new ExponentialRetry(1000, 5000);

                    _redis = await ConnectionMultiplexer.ConnectAsync(configOptions);

                    if (_redis.IsConnected)
                    {
                        Console.WriteLine("Redis connection established successfully");

                        _redis.ConnectionRestored += OnConnectionRestored;
                        _redis.ConnectionFailed += OnConnectionFailed;

                        _isSubscribed = false;

                        _ = Task.Run(async () =>
                        {
                            await Task.Delay(100);
                            await SubscribeToJobStatus();
                            await ProcessOfflineQueueAsync();
                        });

                        return true;
                    }
                    else
                    {
                        Console.WriteLine("Redis connection failed - not connected");
                        return false;
                    }
                }
                finally
                {
                    _isConnecting = false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to connect to Redis: {ex.Message}");
                _isConnecting = false;
                return false;
            }
        }

        private void OnConnectionRestored(object sender, ConnectionFailedEventArgs e)
        {
            Console.WriteLine($"Redis connection restored: {e.EndPoint}");
            _isSubscribed = false;

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(500);
                    await SubscribeToJobStatus();
                    await ProcessOfflineQueueAsync();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error in connection restored handler: {ex.Message}");
                }
            });
        }

        private void OnConnectionFailed(object sender, ConnectionFailedEventArgs e)
        {
            Console.WriteLine($"Redis connection failed: {e.EndPoint} - {e.Exception?.Message}");
            _isSubscribed = false;
        }

        public async Task<List<string>> GetCompletedJobs()
        {
            try
            {
                if (!await EnsureConnectionAsync())
                {
                    return new List<string>();
                }

                var db = _redis.GetDatabase();
                var items = await db.ListRangeAsync("upload:completedjobs", 0, -1)
                    .WaitAsync(TimeSpan.FromSeconds(5));

                return items.Select(x => (string)x).ToList();
            }
            catch (TimeoutException)
            {
                Console.WriteLine("Redis operation timed out while getting completed jobs");
                return new List<string>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting completed jobs: {ex.Message}");
                return new List<string>();
            }
        }

        public async Task<bool> PushToRedis(string payload)
        {
            try
            {
                if (!await EnsureConnectionAsync())
                {
                    _offlineQueue.Enqueue(payload);
                    Console.WriteLine($"Redis unavailable, job queued offline. Queue size: {_offlineQueue.Count}");
                    return true;
                }

                var db = _redis.GetDatabase();
                await db.ListLeftPushAsync("upload:jobs", payload)
                    .WaitAsync(TimeSpan.FromSeconds(5));

                Console.WriteLine("Job successfully pushed to Redis");
                return true;
            }
            catch (TimeoutException)
            {
                _offlineQueue.Enqueue(payload);
                Console.WriteLine($"Redis timeout, job queued offline. Queue size: {_offlineQueue.Count}");
                return true;
            }
            catch (Exception ex)
            {
                _offlineQueue.Enqueue(payload);
                Console.WriteLine($"Redis error, job queued offline: {ex.Message}. Queue size: {_offlineQueue.Count}");
                return true;
            }
        }

        private void ProcessOfflineQueue(object state)
        {
            _ = Task.Run(async () => await ProcessOfflineQueueAsync());
        }

        private async Task ProcessOfflineQueueAsync()
        {
            if (_isProcessingOfflineQueue || _offlineQueue.IsEmpty || _disposed)
                return;

            _isProcessingOfflineQueue = true;

            try
            {
                if (!await EnsureConnectionAsync())
                {
                    Console.WriteLine("Cannot process offline queue - Redis connection unavailable");
                    return;
                }

                var db = _redis.GetDatabase();
                var processedCount = 0;
                var failedCount = 0;
                var itemsToProcess = Math.Min(_offlineQueue.Count, 10);

                for (int i = 0; i < itemsToProcess && _offlineQueue.TryDequeue(out string payload); i++)
                {
                    try
                    {
                        await db.ListLeftPushAsync("upload:jobs", payload)
                            .WaitAsync(TimeSpan.FromSeconds(5));
                        processedCount++;
                        Console.WriteLine($"Successfully pushed offline job to Redis (processed {processedCount})");
                    }
                    catch (Exception ex)
                    {
                        var tempQueue = new List<string> { payload };
                        while (_offlineQueue.TryDequeue(out string item))
                        {
                            tempQueue.Add(item);
                        }
                        foreach (var item in tempQueue)
                        {
                            _offlineQueue.Enqueue(item);
                        }

                        failedCount++;
                        Console.WriteLine($"Failed to push offline job: {ex.Message}");
                        break;
                    }
                }

                if (processedCount > 0)
                {
                    Console.WriteLine($"Successfully processed {processedCount} offline jobs. Remaining in queue: {_offlineQueue.Count}");
                }

                if (failedCount > 0)
                {
                    Console.WriteLine($"Failed to process {failedCount} offline jobs. Will retry later.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing offline queue: {ex.Message}");
            }
            finally
            {
                _isProcessingOfflineQueue = false;
            }
        }

        public async Task SubscribeToJobStatus()
        {
            try
            {
                if (_disposed || _redis?.IsConnected != true || _isSubscribed)
                    return;

                _sub = _redis.GetSubscriber();
                await _sub.SubscribeAsync("upload:updates", (_, value) =>
                {
                    try
                    {
                        Console.WriteLine($"Received Redis update: {value}");
                        var upd = JsonSerializer.Deserialize<UploadUpdate>(value);
                        if (upd is null) return;

                        if (Application.Current != null)
                        {
                            Application.Current.Dispatcher.BeginInvoke(() =>
                            {
                                UpdateAvailable?.Invoke(this, upd);
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error processing update: {ex.Message}");
                    }
                });

                _isSubscribed = true;
                Console.WriteLine("Successfully subscribed to upload:updates channel");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to subscribe to job status: {ex.Message}");
                _isSubscribed = false;
            }
        }

        private async void CheckSubscriptionHealth(object state)
        {
            try
            {
                if (_disposed) return;

                if (_redis?.IsConnected == true && !_isSubscribed)
                {
                    Console.WriteLine("Subscription health check: Not subscribed, attempting to resubscribe...");
                    await SubscribeToJobStatus();
                }

                if (_redis?.IsConnected == true && !_offlineQueue.IsEmpty && !_isProcessingOfflineQueue)
                {
                    Console.WriteLine($"Health check: Processing {_offlineQueue.Count} offline jobs...");
                    _ = Task.Run(async () => await ProcessOfflineQueueAsync());
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in subscription health check: {ex.Message}");
            }
        }

        public int GetOfflineQueueCount()
        {
            return _offlineQueue.Count;
        }

        public void Dispose()
        {
            try
            {
                _disposed = true;

                _retryTimer?.Dispose();
                _subscriptionCheckTimer?.Dispose();

                if (_sub != null)
                {
                    try
                    {
                        _sub.Unsubscribe("upload:updates");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error unsubscribing: {ex.Message}");
                    }
                }

                if (_redis != null)
                {
                    try
                    {
                        _redis.ConnectionRestored -= OnConnectionRestored;
                        _redis.ConnectionFailed -= OnConnectionFailed;
                        _redis.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error disposing Redis connection: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error disposing RedisHelper: {ex.Message}");
            }
        }
    }
}