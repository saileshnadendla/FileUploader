using FileUploader.API.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using System.Text;

namespace FileUploader.API_uTest.Controllers
{
    [TestFixture]
    public class UploadControllerTests
    {
        private Mock<ILogger<UploadController>> _loggerMock;
        private string _testDirectory;
        private UploadController _controller;

        [SetUp]
        public void SetUp()
        {
            _loggerMock = new Mock<ILogger<UploadController>>();
            _testDirectory = Path.Combine(Path.GetTempPath(), "UploadControllerTests");
            Directory.CreateDirectory(_testDirectory);

            _controller = new UploadController(_loggerMock.Object, _testDirectory);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_testDirectory))
                Directory.Delete(_testDirectory, true);
        }

        private IFormFile CreateFormFile(string fileName, string content)
        {
            var bytes = Encoding.UTF8.GetBytes(content);
            var stream = new MemoryStream(bytes);
            return new FormFile(stream, 0, bytes.Length, "file", fileName);
        }

        [Test]
        public async Task Upload_ShouldReturnBadRequest_WhenFileIsNull()
        {
            // Act
            var result = await _controller.Upload(null, "job123");

            // Assert
            Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
            var badRequest = result as BadRequestObjectResult;
            Assert.That(badRequest?.Value, Is.EqualTo("file missing"));
        }

        [Test]
        public async Task Upload_ShouldReturnBadRequest_WhenFileIsEmpty()
        {
            var emptyFile = CreateFormFile("empty.txt", string.Empty);

            var result = await _controller.Upload(emptyFile, "job123");

            Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
            var badRequest = result as BadRequestObjectResult;
            Assert.That(badRequest?.Value, Is.EqualTo("file missing"));
        }

        [Test]
        public async Task Upload_ShouldReturnOk_WhenFileIsValid()
        {
            var file = CreateFormFile("test.txt", "Hello World");

            var result = await _controller.Upload(file, "job123");

            Assert.That(result, Is.InstanceOf<OkResult>());

            // Verify file was written
            var expectedPath = Path.Combine(_testDirectory, "job123_test.txt");
            Assert.That(File.Exists(expectedPath), Is.True);

            var content = await File.ReadAllTextAsync(expectedPath);
            Assert.That(content, Is.EqualTo("Hello World"));
        }

        [Test]
        public async Task Upload_ShouldReturnUnprocessableEntity_WhenExceptionThrown()
        {
            var file = CreateFormFile("test.txt", "Hello World");

            // Force an exception by disposing test folder
            Directory.Delete(_testDirectory, true);

            var result = await _controller.Upload(file, "job123");

            Assert.That(result, Is.InstanceOf<UnprocessableEntityObjectResult>());
            var ue = result as UnprocessableEntityObjectResult;
            Assert.That(ue?.Value?.ToString(), Does.Contain("Unhandled exception"));
        }
    }
}
