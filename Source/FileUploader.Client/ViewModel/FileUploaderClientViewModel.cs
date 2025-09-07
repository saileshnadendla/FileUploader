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
            OnUploadFiles = new DelegateCommand(OnUploadFilesImpln, () => true);
            DownloadCommand = new DelegateCommand<FileItem>(OnDownloadFile);

            _ = PreFillAvailableData();
            _ = SubscribeToJobStatus();

            Files = new ObservableCollection<FileItem>(Files.Where(x => x.JobId != Guid.Empty && !string.IsNullOrEmpty(x.FileName)).ToList());
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

        private async void OnUploadFilesImpln()
        {
            foreach (var f in Files.Where(x => !x.IsDone))
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

                    await _redisHelper.PushToRedis(payload);
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
            try
            {
                var response = await _httpClientHelper.GetAsync(file);

                if (!response.IsSuccessStatusCode)
                {
                    MessageBox.Show("Failed to download file: " + response.ReasonPhrase);
                    return;
                }

                var fileBytes = await response.Content.ReadAsByteArrayAsync();
                _fileDialogService.SaveSelectedFile(file.FileName, fileBytes);
                
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message);
            }
        }

        
        private async Task PreFillAvailableData()
        {
            var completedJobs = await _redisHelper.GetCompletedJobs();
            if (completedJobs != null && completedJobs.Any())
            {
                foreach (var item in completedJobs)
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
        }

        private async Task SubscribeToJobStatus()
        { 
            _redisHelper.UpdateAvailable += OnRedisUpdateAvailable;
            await _redisHelper.SubscribeToJobStatus();
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
