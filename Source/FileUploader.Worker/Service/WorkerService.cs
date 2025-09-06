using FileUploader.Contracts;
using FileUploader.Worker.Helpers;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Net.Http.Headers;
using System.Text.Json;

namespace FileUploader.Worker.Service
{
    public class WorkerService : BackgroundService
    {
        private readonly ILogger<WorkerService> _logger;
        private readonly IHttpClientHelper _httpClientHelper;
        private readonly IRedisHelper _redisHelper;

        public WorkerService(ILogger<WorkerService> logger, IHttpClientHelper httpClientHelper, IRedisHelper redisHelper)
        {
            _logger = logger;
            _httpClientHelper = httpClientHelper;
            _redisHelper = redisHelper;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Worker started. Waiting for jobs...");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var jobToProcess = await _redisHelper.GetUploadJob();
                    if (jobToProcess == null)
                    {
                        await Task.Delay(500);
                        continue;
                    }

                    _logger.LogInformation("Picked job {JobId} for {FileName}", jobToProcess.JobId, jobToProcess.FileName);

                    _redisHelper.PublishToRedis(new UploadUpdate(jobToProcess.JobId, jobToProcess.FileName, UploadStatusKind.InProgress, 0, jobToProcess.FileSize, null));
                    var success = await ProcessJob(jobToProcess);

                    if (success)
                    {
                        await HandleSuccess(jobToProcess);
                    }
                    else
                    {
                        await HandleFailure(jobToProcess);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Worker loop error");
                    await Task.Delay(1000, stoppingToken);
                }
            }
        }

        private async Task<bool> ProcessJob(UploadJob job)
        {
            try
            {
                var result = await _httpClientHelper.HttpClientPostAsync(job);

                return result;
            }
            catch (Exception ex)
            {
                _redisHelper.PublishToRedis(new UploadUpdate(job.JobId, job.FileName, UploadStatusKind.Failed, 0, job.FileSize, ex.Message));
                return false;
            }
        }

        private async Task HandleSuccess(UploadJob job)
        {
            var uploadUpdate = new UploadUpdate(job.JobId, job.FileName, UploadStatusKind.Completed, 100, job.FileSize, null);
            _redisHelper.PublishToRedis(uploadUpdate);

            await _redisHelper.PushToRedis("upload:completedjobs", JsonSerializer.Serialize(uploadUpdate));
        }

        private async Task HandleFailure(UploadJob job)
        {
            if (job.Attempt < 3)
            {
                job.Attempt++;
                await _redisHelper.PushToRedis("upload:jobs", JsonSerializer.Serialize(job));
                _redisHelper.PublishToRedis(new UploadUpdate(job.JobId, job.FileName, UploadStatusKind.Queued, 0, job.FileSize, $"Retry {job.Attempt}/3"));
            }
            else
            {
                var uploadUpdate = new UploadUpdate(job.JobId, job.FileName, UploadStatusKind.Failed, 0, job.FileSize, "Max retries exceeded");
                await _redisHelper.PushToRedis("upload:jobs:dlq", JsonSerializer.Serialize(job));
                await _redisHelper.PushToRedis("upload:completedjobs", JsonSerializer.Serialize(uploadUpdate));
                _redisHelper.PublishToRedis(uploadUpdate);
            }
        }
    }
}
