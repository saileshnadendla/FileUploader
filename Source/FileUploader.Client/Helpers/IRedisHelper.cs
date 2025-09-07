using FileUploader.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileUploader.Client.Helpers
{
    internal interface IRedisHelper
    {
        event EventHandler<UploadUpdate> UpdateAvailable;

        Task<List<string>> GetCompletedJobs();
        Task<bool> PushToRedis(string payload);
        Task SubscribeToJobStatus();
        int GetOfflineQueueCount();
    }
}
