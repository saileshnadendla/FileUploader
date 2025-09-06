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

namespace FileUploader.API_uTest
{
    internal class HealthCheckTests
    {
        private Mock<ILogger<HealthCheckController>> _loggerMock;
        private HealthCheckController _controller;

        [SetUp]
        public void SetUp()
        {
            _loggerMock = new Mock<ILogger<HealthCheckController>>();
            _controller = new HealthCheckController(_loggerMock.Object);
        }

        [Test]
        public void Health_ReturnsOk()
        {
            // Act
            var result = _controller.Health();

            // Assert
            Assert.IsInstanceOf<OkObjectResult>(result);
            var ok = (OkObjectResult)result;
            Assert.That(ok.Value, Is.Not.Null);
            Assert.That(ok.Value!.ToString(), Does.Contain("ok"));
        }
    }
}
