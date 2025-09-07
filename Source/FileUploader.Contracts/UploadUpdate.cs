using System;

namespace FileUploader.Contracts
{
    public class UploadUpdate
    {
        public Guid JobId { get; }

        public string FileName { get; }

        public UploadStatusKind Status { get; }

        public int ProgressPercent { get; }

        public string Error { get; }

        public string FileSize { get; }

        public UploadUpdate(Guid jobId, string fileName, UploadStatusKind status, int progressPercent, string fileSize, string error = null)
        {
            JobId = jobId;
            FileName = fileName;
            Status = status;
            ProgressPercent = progressPercent;
            Error = error;
            FileSize = fileSize;
        }
    }

    public class UploadUpdateBuilder
    {
        private Guid guid;
        private string fileName;
        private UploadStatusKind status;
        private int progressPercent;
        private string error;
        private string fileSize;

        public UploadUpdateBuilder WithGuid(Guid id)
        {
            this.guid = id;
            return this;
        }

        public UploadUpdateBuilder WithFileName(string name)
        {
            this.fileName = name;
            return this;
        }

        public UploadUpdateBuilder WithStatus(UploadStatusKind kind)
        {
            this.status = kind;
            return this;
        }

        public UploadUpdateBuilder WithPercentage(int progress)
        {
            this.progressPercent = progress;
            return this;
        }

        public UploadUpdateBuilder WithError(string message)
        {
            this.error = message;
            return this;
        }

        public UploadUpdateBuilder WithFileSize(string size)
        {
            this.fileSize = size;
            return this;
        }

        public UploadUpdate Build()
        {
            return new UploadUpdate(guid, fileName, status, progressPercent, fileSize, error);
        }
    }

}
