using FileUploader.Client.Model;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace FileUploader.Client.Helpers
{
    internal class HttpClientHelper : IHttpClientHelper
    {
        public async Task<HttpResponseMessage> GetAsync(FileItem file)
        {
            string apiUrl = $"http://localhost:5000/api/download/{file.JobId}_{file.FileName}";
            using(var client = new HttpClient())
            {
                try
                {
                    var response = await client.GetAsync(apiUrl);

                    return response;
                }
                catch (Exception ex)
                {
                    throw new Exception(ex.Message);
                }
            }
        }
    }
}
