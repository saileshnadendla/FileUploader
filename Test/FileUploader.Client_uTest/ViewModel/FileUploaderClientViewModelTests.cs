using FileUploader.Client.Helpers;
using FileUploader.Client.Model;
using FileUploader.Client.ViewModel;
using FileUploader.Contracts;
using Moq;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml.Linq;
using static System.Runtime.InteropServices.JavaScript.JSType;


namespace FileUploader.Client_uTest.ViewModel
{
    [TestFixture]
    public class FileUploaderClientViewModelTests
    {
        private Mock<IHttpClientHelper> _httpClientHelperMock;
        private Mock<IFileDialogHelper> _fileDialogHelperMock;
        private Mock<IRedisHelper> _redisHelperMock;
        private FileUploaderClientViewModel sut;

        [SetUp]
        public void Setup()
        {
            _httpClientHelperMock = new Mock<IHttpClientHelper>();
            _fileDialogHelperMock = new Mock<IFileDialogHelper>();
            _redisHelperMock = new Mock<IRedisHelper>();
        }

        [Test]
        public void PreFillAvailableData_ViewModel_CompletedJobsArePreFilled()
        {
            //Arrange
            var uploadUpdate = new UploadUpdateBuilder()
                                .WithGuid(Guid.NewGuid())
                                .WithFileName("temp.txt")
                                .WithStatus(UploadStatusKind.Completed)
                                .WithPercentage(100)
                                .WithFileSize("100")
                                .Build();

            _redisHelperMock.Setup(x => x.GetCompletedJobs()).ReturnsAsync(new List<string> { JsonSerializer.Serialize(uploadUpdate) });

            //Act
            sut = new FileUploaderClientViewModel(_httpClientHelperMock.Object, _fileDialogHelperMock.Object, _redisHelperMock.Object);
            Task.Delay(100);

            //Assert
            _redisHelperMock.Verify(x => x.SubscribeToJobStatus(), Times.Once);
            _redisHelperMock.Verify(x => x.GetCompletedJobs(), Times.Once);
            Assert.That(sut.Files.Count, Is.EqualTo(1));
            Assert.That(sut.Files[0].FileName, Is.EqualTo("temp.txt"));
        }

        [Test]
        public void RedisUpdateAvailable_ViewModel_StatusIsUpdated()
        {
            //Arrange
            var guid = Guid.NewGuid();
            var fileItem = new FileItem
            {
                JobId = guid,
                Status = "Queued",
                FileName = "temp.txt"
            };
            sut = new FileUploaderClientViewModel(_httpClientHelperMock.Object, _fileDialogHelperMock.Object, _redisHelperMock.Object);
            sut.Files.Add(fileItem);

            var uploadUpdate = new UploadUpdateBuilder()
                                .WithGuid(guid)
                                .WithFileName("temp.txt")
                                .WithStatus(UploadStatusKind.Completed)
                                .WithPercentage(100)
                                .WithFileSize("100")
                                .Build();

            //Act
            _redisHelperMock.Raise(x => x.UpdateAvailable += null, this, uploadUpdate);

            //Assert
            _redisHelperMock.Verify(x => x.SubscribeToJobStatus(), Times.Once);
            _redisHelperMock.Verify(x => x.GetCompletedJobs(), Times.Once);
            Assert.That(sut.Files.Count, Is.EqualTo(1));
            Assert.That(sut.Files[0].Status == UploadStatusKind.Completed.ToString());
        }

        [Test]
        public void DownloadFile_ViewModel_DownloadDialogIsShown()
        {
            //Arrange
            var fileItem = new FileItem()
            {
                FileName = "saveFile.txt"
            };
            var httpResponseMessage = new HttpResponseMessage();
            httpResponseMessage.StatusCode = System.Net.HttpStatusCode.OK;
            httpResponseMessage.Content = new ByteArrayContent(Encoding.UTF8.GetBytes("hello world"));
            var byteArray = httpResponseMessage.Content.ReadAsByteArrayAsync().Result;
            _httpClientHelperMock.Setup(x => x.GetAsync(fileItem)).ReturnsAsync(httpResponseMessage);
            sut = new FileUploaderClientViewModel(_httpClientHelperMock.Object, _fileDialogHelperMock.Object, _redisHelperMock.Object);

            //Act
            sut.DownloadCommand.Execute(fileItem);

            //Assert
            _fileDialogHelperMock.Verify(x => x.SaveSelectedFile("saveFile.txt", byteArray), Times.Once);
        }

        [Test]
        public void UploadFiles_ViewModel_SelectedUnProcessedFilesAreUploaded()
        {
            //Arrange
            var job1Id = Guid.NewGuid();
            var job3Id = Guid.NewGuid();
            var files = new List<FileItem>();
            var payloads = new List<string>();
            files.Add(new FileItem()
            { 
                FileName = "file1.txt",
                JobId = job1Id,
                Status = "Ready",
                IsDone = false
            });
            files.Add(new FileItem()
            {
                FileName = "file2.txt",
                JobId = Guid.NewGuid(),
                Status = UploadStatusKind.Completed.ToString(),
                IsDone = true
            });
            files.Add(new FileItem()
            {
                FileName = "file3.txt",
                JobId = job3Id,
                Status = "Ready",
                IsDone = false
            });
            sut = new FileUploaderClientViewModel(_httpClientHelperMock.Object, _fileDialogHelperMock.Object, _redisHelperMock.Object);
            files.ForEach(file => sut.Files.Add(file));
            _redisHelperMock.Setup(x => x.PushToRedis(It.IsAny<string>())).Callback((string payload) => payloads.Add(payload));

            //Act
            sut.OnUploadFiles.Execute();

            //Assert
            _redisHelperMock.Verify(x => x.PushToRedis(It.IsAny<string>()), Times.Exactly(2));
            Assert.That(payloads.Count, Is.EqualTo(2));
            Assert.True(payloads[0].Contains(job1Id.ToString()));
            Assert.True(payloads[1].Contains(job3Id.ToString()));
        }

        [Test]
        public void SelectFiles_ViewModel_SelectFilesDialogIsShown()
        {
            //Arrange
            var tempPath = Path.GetTempPath();
            var tempFile = Path.GetTempFileName();
            var fakeFiles = new List<string> { Path.Combine(tempPath, tempFile) };
            _fileDialogHelperMock.Setup(x => x.SelectFiles()).Returns(fakeFiles.ToArray());
            sut = new FileUploaderClientViewModel(_httpClientHelperMock.Object, _fileDialogHelperMock.Object, _redisHelperMock.Object);

            //Act
            sut.OnSelectFiles.Execute();

            //Assert
            _fileDialogHelperMock.Verify(x => x.SelectFiles(), Times.Once);
            Assert.That(sut.Files.Count, Is.EqualTo(1));
            Assert.That(sut.Files[0].FileName, Is.EqualTo(Path.GetFileName(tempFile)));
        }
    }
}
