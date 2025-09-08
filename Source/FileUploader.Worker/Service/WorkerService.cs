using FileUploader.Contracts;
using FileUploader.Worker.Helpers;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Drawing;
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
                        await Task.Delay(500, stoppingToken);
                        continue;
                    }

                    _logger.LogInformation("Picked job {JobId} for {FileName}", jobToProcess.JobId, jobToProcess.FileName);

                    var updatePublished = await _redisHelper.PublishToRedis(GetUpdateObject(jobToProcess.JobId, jobToProcess.FileName, UploadStatusKind.InProgress, 0, jobToProcess.FileSize, null));
                    if (!updatePublished)
                    {
                        _logger.LogWarning("Failed to publish InProgress status for job {JobId}", jobToProcess.JobId);
                    }

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
                _logger.LogError(ex, "Failed to process job {JobId}", job.JobId);

                var updatePublished = await _redisHelper.PublishToRedis(GetUpdateObject(job.JobId, job.FileName, UploadStatusKind.Failed, 0, job.FileSize, ex.Message));
                if (!updatePublished)
                {
                    _logger.LogWarning("Failed to publish failure status for job {JobId}", job.JobId);
                }

                return false;
            }
        }

        private async Task HandleSuccess(UploadJob job)
        {
            var uploadUpdate = GetUpdateObject(job.JobId, job.FileName, UploadStatusKind.Completed, 100, job.FileSize, null);

            var updatePublished = await _redisHelper.PublishToRedis(uploadUpdate);
            if (!updatePublished)
            {
                _logger.LogWarning("Failed to publish completion status for job {JobId}", job.JobId);
            }

            var pushed = await _redisHelper.PushToRedis("upload:completedjobs", JsonSerializer.Serialize(uploadUpdate));
            if (!pushed)
            {
                _logger.LogError("Failed to add job {JobId} to completed jobs list", job.JobId);
            }
        }

        private async Task HandleFailure(UploadJob job)
        {
            if (job.Attempt < 3)
            {
                job.Attempt++;
                var requeued = await _redisHelper.PushToRedis("upload:jobs", JsonSerializer.Serialize(job));

                if (requeued)
                {
                    _logger.LogInformation("Requeued job {JobId} for retry {Attempt}/3", job.JobId, job.Attempt);
                    var updatePublished = await _redisHelper.PublishToRedis(GetUpdateObject(job.JobId, job.FileName, UploadStatusKind.Queued, 0, job.FileSize, $"Retry {job.Attempt}/3"));
                    if (!updatePublished)
                    {
                        _logger.LogWarning("Failed to publish retry status for job {JobId}", job.JobId);
                    }
                }
                else
                {
                    _logger.LogError("Failed to requeue job {JobId} for retry", job.JobId);
                    await HandleFinalFailure(job);
                }
            }
            else
            {
                await HandleFinalFailure(job);
            }
        }

        private async Task HandleFinalFailure(UploadJob job)
        {
            var uploadUpdate = GetUpdateObject(job.JobId, job.FileName, UploadStatusKind.Failed, 0, job.FileSize, "Max retries exceeded");

            var movedToDLQ = await _redisHelper.PushToRedis("upload:jobs:dlq", JsonSerializer.Serialize(job));
            if (!movedToDLQ)
            {
                _logger.LogError("Failed to move job {JobId} to dead letter queue", job.JobId);
            }

            var addedToCompleted = await _redisHelper.PushToRedis("upload:completedjobs", JsonSerializer.Serialize(uploadUpdate));
            if (!addedToCompleted)
            {
                _logger.LogError("Failed to add failed job {JobId} to completed jobs list", job.JobId);
            }

            var updatePublished = await _redisHelper.PublishToRedis(uploadUpdate);
            if (!updatePublished)
            {
                _logger.LogWarning("Failed to publish final failure status for job {JobId}", job.JobId);
            }

            _logger.LogWarning("Job {JobId} failed after {Attempt} attempts", job.JobId, job.Attempt);
        }

        private UploadUpdate GetUpdateObject(Guid id, string name, UploadStatusKind status, int progress, string size, string error)
        {
            var uploadUpdate = new UploadUpdateBuilder()
                                .WithGuid(id)
                                .WithFileName(name)
                                .WithStatus(status)
                                .WithPercentage(progress)
                                .WithFileSize(size)
                                .WithError(error)
                                .Build();

            return uploadUpdate;
        }
    }
}