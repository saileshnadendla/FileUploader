using FileUploader.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileUploader.Worker.Helpers
{
    public interface IHttpClientHelper
    {
        Task<bool> HttpClientPostAsync(UploadJob job);
    }
}
