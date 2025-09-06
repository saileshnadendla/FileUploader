using FileUploader.Contracts;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace FileUploader.Worker.Helpers
{
    public class HttpClientHelper : IHttpClientHelper
    {
        private readonly HttpClient _httpClient = new HttpClient { BaseAddress = new Uri("http://localhost:5000") };

        public async Task<bool> HttpClientPostAsync(UploadJob job)
        {
            using (var form = new MultipartFormDataContent())
            {
                using (var stream = File.OpenRead(job.FilePath))
                {
                    var streamContent = new StreamContent(stream);
                    streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                    form.Add(streamContent, "file", job.FileName);
                    form.Add(new StringContent(job.JobId.ToString()), "jobId");

                    var resp = await _httpClient.PostAsync("/api/upload", form);

                    return resp.IsSuccessStatusCode;
                }
            }
        }
    }
}
