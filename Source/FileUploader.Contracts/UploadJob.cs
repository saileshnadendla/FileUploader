using System;
using System.IO;

namespace FileUploader.Contracts
{
    public class UploadJob
    {
        public string FilePath { get; set; }

        public Guid JobId { get; }

        public string FileName { get; }

        public string FileSize { get; }

        public int Attempt { get; set; }

        public UploadJob(Guid jobId, string filePath, string fileName, string fileSize, int attempt = 0)
        {
            JobId = jobId;
            FilePath = filePath;
            FileName = fileName;
            FileSize = fileSize;
            Attempt = attempt;
        }

    }
}
