using FileUploader.API.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileUploader.API_uTest.Controllers
{
    internal class DownloadControllerTests
    {
        private Mock<ILogger<DownloadController>> _loggerMock;
        private string _testDirectory;
        private DownloadController _controller;

        [SetUp]
        public void SetUp()
        {
            _loggerMock = new Mock<ILogger<DownloadController>>();
            _testDirectory = Path.Combine(Path.GetTempPath(), "UploadControllerTests");
            Directory.CreateDirectory(_testDirectory);

            _controller = new DownloadController(_loggerMock.Object, _testDirectory);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_testDirectory))
                Directory.Delete(_testDirectory, true);
        }

        [Test]
        public void DownloadFile_FileNotFound_ReturnsNotFound()
        {
            // Act
            var result = _controller.DownloadFile("doesnotexist.txt");

            // Assert
            Assert.IsInstanceOf<NotFoundObjectResult>(result);
            var notFound = (NotFoundObjectResult)result;
            Assert.That(notFound.Value, Is.EqualTo("File not found"));
        }

        [Test]
        public void DownloadFile_FileExists_ReturnsFileResult()
        {
            // Arrange
            var fileName = "test.txt";
            var filePath = Path.Combine(_testDirectory, fileName);
            File.WriteAllText(filePath, "Hello world");

            // Act
            var result = _controller.DownloadFile(fileName);

            // Assert
            Assert.IsInstanceOf<FileContentResult>(result);
            var fileResult = (FileContentResult)result;
            Assert.That(fileResult.FileDownloadName, Is.EqualTo(fileName));
            Assert.That(fileResult.ContentType, Is.EqualTo("application/octet-stream"));
            Assert.That(fileResult.FileContents, Is.Not.Null.And.Length.GreaterThan(0));
        }
    }
}
