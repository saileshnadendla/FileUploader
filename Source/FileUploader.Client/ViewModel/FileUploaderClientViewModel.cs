using FileUploader.Client.Helpers;
using FileUploader.Client.Model;
using FileUploader.Contracts;
using Prism.Commands;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
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

        private readonly IHttpClientHelper _httpClientHelper;
        private readonly IFileDialogHelper _fileDialogService;
        private readonly IRedisHelper _redisHelper;

        public FileUploaderClientViewModel() : this(new HttpClientHelper(), new FileDialogHelper(), new RedisHelper())
        {
        }

        public FileUploaderClientViewModel(IHttpClientHelper httpClientHelper, IFileDialogHelper fileDialogService, IRedisHelper redisHelper)
        {
            _httpClientHelper = httpClientHelper;
            _fileDialogService = fileDialogService;
            _redisHelper = redisHelper;

            OnSelectFiles = new DelegateCommand(OnSelectFilesImpln, () => true);
            OnUploadFiles = new DelegateCommand(() => _ = OnUploadFilesImplnAsync(), () => true);
            DownloadCommand = new DelegateCommand<FileItem>(file => _ = OnDownloadFileAsync(file));

            _ =  PreFillAvailableData();
            _ = SubscribeToJobStatus();
        }

        private void OnSelectFilesImpln()
        {
            var selectedFiles = _fileDialogService.SelectFiles();
            if (selectedFiles == null || selectedFiles.Count() == 0)
            {
                return;
            }

            foreach (var p in selectedFiles)
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

        private async Task OnUploadFilesImplnAsync()
        {
            var filesToUpload = Files.Where(x => !x.IsDone && (x.Status == "Ready")).ToList();

            foreach (var f in filesToUpload)
            {
                try
                {
                    f.Status = "Queued";
                    f.Progress = 0;

                    var job = new UploadJobBuilder()
                                .WithJobId(f.JobId)
                                .WithFilePath(f.FilePath)
                                .WithFileName(f.FileName)
                                .WithFileSize(f.SizeMB)
                                .Build();

                    var payload = JsonSerializer.Serialize(job);

                    bool success = await _redisHelper.PushToRedis(payload);

                    if (success)
                    {
                        f.Status = "Queued";
                    }
                    else
                    {
                        f.Status = "Failed to enqueue";
                    }
                }
                catch (Exception ex)
                {
                    f.Status = "Failed to enqueue";
                    Console.WriteLine($"Error enqueueing file {f.FileName}: {ex}");
                }
            }

            var offlineCount = _redisHelper.GetOfflineQueueCount();
            if (offlineCount > 0)
            {
                await Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    Console.WriteLine($"Info: {offlineCount} jobs queued offline and will be processed when Redis is available");
                });
            }
        }

        private async Task OnDownloadFileAsync(FileItem file)
        {
            try
            {
                var response = await _httpClientHelper.GetAsync(file);

                if (!response.IsSuccessStatusCode)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show("Failed to download file: " + response.ReasonPhrase);
                    });
                    return;
                }

                var fileBytes = await response.Content.ReadAsByteArrayAsync();
                _fileDialogService.SaveSelectedFile(file.FileName, fileBytes);

            }
            catch (Exception ex)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show("Error: " + ex.Message);
                });
            }
        }

        private async Task PreFillAvailableData()
        {
            try
            {
                var completedJobs = await _redisHelper.GetCompletedJobs();
                if (completedJobs != null && completedJobs.Any())
                {
                    foreach (var item in completedJobs)
                    {
                        var itemStr = (string)item;
                        var uploadUpdate = JsonSerializer.Deserialize<UploadUpdate>(itemStr);

                        if (uploadUpdate?.JobId != Guid.Empty && !string.IsNullOrEmpty(uploadUpdate.FileName))
                        {
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
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error pre-filling data: {ex.Message}");
            }
        }

        private async Task SubscribeToJobStatus()
        {
            try
            {
                await _redisHelper.SubscribeToJobStatus();
                _redisHelper.UpdateAvailable += OnRedisUpdateAvailable;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error subscribing to job status: {ex.Message}");
            }
        }

        private void OnRedisUpdateAvailable(object sender, UploadUpdate upd)
        {
            var item = Files.FirstOrDefault(f => f.JobId == upd.JobId);
            if (item == null) return;

            item.Status = upd.Status.ToString();
            item.Progress = upd.ProgressPercent;
            if (upd.Status == UploadStatusKind.Completed || upd.Status == UploadStatusKind.Failed)
                item.IsDone = true;
        }
    }
}