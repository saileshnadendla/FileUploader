using FileUploader.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileUploader.Worker.Helpers
{
    public interface IRedisHelper
    {
        Task<UploadJob> GetUploadJob();

        Task<bool> PushToRedis(string key, string job);

        Task<bool> PublishToRedis(UploadUpdate update);
    }
}
