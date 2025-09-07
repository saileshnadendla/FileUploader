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

    public class UploadJobBuilder
    {
        private string filepath;
        private Guid jobId;
        private string fileName;
        private string fileSize;
        private int attempt = 0;

        public UploadJobBuilder WithFilePath(string path)
        {
            this.filepath = path;
            return this;
        }

        public UploadJobBuilder WithJobId(Guid id)
        {
            this.jobId = id;
            return this;
        }

        public UploadJobBuilder WithFileName(string name)
        {
            this.fileName = name;
            return this;
        }

        public UploadJobBuilder WithFileSize(string size)
        {
            this.fileSize = size;
            return this;
        }

        public UploadJobBuilder WithAttempt(int a)
        {
            this.attempt = a;
            return this;
        }

        public UploadJob Build()
        {
            return new UploadJob(jobId, filepath, fileName, fileSize, attempt);
        }
    }
}
