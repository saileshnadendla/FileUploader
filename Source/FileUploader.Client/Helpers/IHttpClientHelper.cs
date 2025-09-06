using FileUploader.Client.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace FileUploader.Client.Helpers
{
    internal interface IHttpClientHelper
    {
        Task<HttpResponseMessage> GetAsync(FileItem file);
    }
}
