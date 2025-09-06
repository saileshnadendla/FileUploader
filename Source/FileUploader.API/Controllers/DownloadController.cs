using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FileUploader.API.Controllers
{
    [ApiController]
    [Route("api")]
    [AllowAnonymous]
    public class DownloadController : ControllerBase
    {
        private readonly string _inbox;
        private readonly ILogger<DownloadController> _logger;

        public DownloadController(ILogger<DownloadController> logger)
        {
            _logger = logger;
            _inbox = Path.Combine(AppContext.BaseDirectory, "Inbox");
            Directory.CreateDirectory(_inbox);
        }

        // constructor for testing
        internal DownloadController(ILogger<DownloadController> logger, string inbox)
        {
            _logger = logger;
            _inbox = inbox;
        }

        /// <summary>
        /// Get File from filename
        /// </summary>
        [HttpGet("download/{fileName}")]
        public IActionResult DownloadFile(string fileName)
        {
            var filePath = Path.Combine(_inbox, fileName);
            _logger.LogInformation("Trying to fetch file from: " + filePath);

            if (!System.IO.File.Exists(filePath))
            {
                return NotFound("File not found");
            }

            // Return file as a download
            var contentType = "application/octet-stream"; // generic for binary files
            var fileBytes = System.IO.File.ReadAllBytes(filePath);
            return File(fileBytes, contentType, fileName);
        }
    }
}
