using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing.Constraints;

namespace FileUploader.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [AllowAnonymous]
    public class UploadController : ControllerBase
    {
        private readonly string _inbox;
        private readonly ILogger<UploadController> _logger;

        public UploadController(ILogger<UploadController> logger)
        {
            _logger = logger;
            _inbox = Path.Combine(AppContext.BaseDirectory, "Inbox");
            Directory.CreateDirectory(_inbox);
        }

        // constructor for testing
        internal UploadController(ILogger<UploadController> logger, string inbox)
        {
            _logger = logger;
            _inbox = inbox;
        }

        /// <summary>
        /// Upload a file to the server
        /// </summary>
        [HttpPost]
        [RequestSizeLimit(1024L * 1024L * 1024L)]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)] 
        public async Task<IActionResult> Upload([FromForm] IFormFile file, [FromForm] string jobId)
        {
            try
            {
                if (file is null || file.Length == 0)
                {
                    _logger.LogError("file is null or not supported");
                    return BadRequest("file missing");
                }

                var fileName = $"{jobId}_{file.FileName}";
                var tempFileName = Path.GetFileName(fileName);
                var tempPath = Path.Combine(_inbox, tempFileName);

                _logger.LogInformation("Trying to save file to: " + tempPath);

                await using (var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await file.CopyToAsync(fs);
                    await fs.FlushAsync();
                }

                _logger.LogInformation($"Handled uplaoding file with name {fileName} successfully");
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Unhandled exception while processing file {file.FileName} \n" + ex.Message);
                return UnprocessableEntity("Unhandled exception: " + ex.Message);
            }
        }
    }
}
