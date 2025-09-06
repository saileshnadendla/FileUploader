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

}
