using FileUploader.Client.Model;
using FileUploader.Contracts;
using Microsoft.Win32;
using Prism.Commands;
using StackExchange.Redis;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

namespace FileUploader.Client.ViewModel
{
    internal class FileUploaderClientViewModel : IFileUploaderClientViewModel
    {
        public ObservableCollection<FileItem> Files { get; } = new ObservableCollection<FileItem>();

        public DelegateCommand OnSelectFiles { get; private set; }
        public DelegateCommand OnUploadFiles { get; private set; } 
        public DelegateCommand<FileItem> DownloadCommand { get; private set; }

        public FileUploaderClientViewModel()
        {
            OnSelectFiles = new DelegateCommand(OnSelectFilesImpln, () => true);
            OnUploadFiles = new DelegateCommand(OnUploadFilesImpln, () => true);
            DownloadCommand = new DelegateCommand<FileItem>(OnDownloadFile);

            _ = EnsureRedis();
            _ = SubscribeToJobStatus();

            Files = new ObservableCollection<FileItem>(Files.Where(x => x.JobId != Guid.Empty && !string.IsNullOrEmpty(x.FileName)).ToList());
        }

        private void OnSelectFilesImpln()
        {
            var dlg = new OpenFileDialog { Multiselect = true };
            if (dlg.ShowDialog() == true)
            {
                foreach (var p in dlg.FileNames)
                {
                    var fi = new FileInfo(p);
                    Files.Add(new FileItem
                    {
                        FilePath = p,
                        FileName = fi.Name,
                        FileSize = fi.Length,
                        SizeMB = (fi.Length / 1024d / 1024d).ToString("F2"),
                        Status = "Ready",
                        Progress = 0,
                        JobId = Guid.NewGuid()
                    });
                }
            }
        }

        private async void OnUploadFilesImpln()
        {
            if (_redis == null)
            {
                _redis = await ConnectionMultiplexer.ConnectAsync("localhost:6379");
            }

            foreach (var f in Files.Where(x => !x.IsDone))
            {
                try
                {
                    f.Status = "Queued";
                    f.Progress = 0;

                    var job = new UploadJob(f.JobId, f.FilePath, f.FileName, f.SizeMB, 0);
                    var payload = JsonSerializer.Serialize(job);

                    var db = _redis.GetDatabase();
                    await db.ListLeftPushAsync("upload:jobs", payload);
                }
                catch (Exception ex)
                {
                    f.Status = "Failed to enqueue";
                    Console.WriteLine(ex);
                }
            }
        }

        private async void OnDownloadFile(FileItem file)
        {
            string apiUrl = $"http://localhost:5000/api/download/{file.JobId}_{file.FileName}";

            using (var client = new HttpClient())
            {
                try
                {
                    var response = await client.GetAsync(apiUrl);

                    if (!response.IsSuccessStatusCode)
                    {
                        MessageBox.Show("Failed to download file: " + response.ReasonPhrase);
                        return;
                    }

                    var fileBytes = await response.Content.ReadAsByteArrayAsync();

                    var saveFileDialog = new SaveFileDialog();
                    saveFileDialog.FileName = file.FileName;
                    saveFileDialog.Filter = "All files (*.*)|*.*";

                    if (saveFileDialog.ShowDialog() == true)
                    {
                        File.WriteAllBytes(saveFileDialog.FileName, fileBytes);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error: " + ex.Message);
                }
            }
        }

        private ConnectionMultiplexer _redis;
        private ISubscriber _sub;
        private async Task EnsureRedis()
        {
            if (_redis == null)
            {
                _redis = await ConnectionMultiplexer.ConnectAsync("localhost:6379");
            }

            var db = _redis.GetDatabase();
            var items = await db.ListRangeAsync("upload:completedjobs", 0, -1);
            foreach (var item in items)
            {
                var itemStr = (string)item;
                var uploadUpdate = JsonSerializer.Deserialize<UploadUpdate>(itemStr);

                var file = new FileItem
                {
                    JobId = uploadUpdate.JobId,
                    FileName = uploadUpdate.FileName,
                    SizeMB = uploadUpdate.FileSize,
                    IsDone = true,
                    Status = uploadUpdate.Status.ToString(),
                    Progress = uploadUpdate.ProgressPercent
                };

                Files.Add(file);
            }
        }

        private async Task SubscribeToJobStatus()
        {
            if (_redis == null)
            {
                _redis = await ConnectionMultiplexer.ConnectAsync("localhost:6379");
            }

            _sub = _redis.GetSubscriber();
            await _sub.SubscribeAsync("upload:updates", (_, value) =>
            {
                try
                {
                    var upd = JsonSerializer.Deserialize<UploadUpdate>(value);
                    if (upd is null) return;
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        var item = Files.FirstOrDefault(f => f.JobId == upd.JobId);
                        if (item == null) return;
                        item.Status = upd.Status.ToString();
                        item.Progress = upd.ProgressPercent;
                        if (upd.Status == UploadStatusKind.Completed || upd.Status == UploadStatusKind.Failed)
                            item.IsDone = true;
                    });
                }
                catch { }
            });
        }
    }
}
