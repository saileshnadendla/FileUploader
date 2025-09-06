using FileUploader.Contracts;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FileUploader.Client.Model
{
    public class FileItem : INotifyPropertyChanged
    {
        public Guid JobId { get; set; }
        public string FilePath { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string SizeMB { get; set; } = "0";

        public bool IsAvailableForDownload { get => this.Status == UploadStatusKind.Completed.ToString(); }

        private string _status = "Ready";
        public string Status { get => _status; set { _status = value; OnPropertyChanged(nameof(Status)); OnPropertyChanged(nameof(IsAvailableForDownload)); } }

        private int _progress;
        public int Progress { get => _progress; set { _progress = value; OnPropertyChanged(nameof(Progress)); } }

        public bool IsDone { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
        void OnPropertyChanged(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }
}
