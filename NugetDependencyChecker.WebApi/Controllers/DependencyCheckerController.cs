using Microsoft.AspNetCore.Mvc;
using NugetDependencyChecker.WebApi.Services;

namespace NugetDependencyChecker.WebApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DependencyCheckerController : ControllerBase
    {
        private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB
        private const string AllowedFileName = "project.assets.json";
        private readonly IDependencyAnalysisService _analysisService;

        public DependencyCheckerController(IDependencyAnalysisService analysisService)
        {
            _analysisService = analysisService;
        }

        /// <summary>
        /// Generates a dependency matrix in Excel format from a project.assets.json file.
        /// </summary>
        /// <param name="file">The project.assets.json file</param>
        /// <param name="packageFilterPrefix">Optional package name prefix to filter dependencies</param>
        /// <returns>Excel file containing dependency matrix</returns>
        [HttpPost("create-matrix")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(FileContentResult))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [Produces("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
        public async Task<IActionResult> CreateDependencyMatrix(
            IFormFile file, 
            [FromQuery] string packageFilterPrefix = "")
        {
            var validationResult = ValidateUploadedFile(file);
            if (!validationResult.isValid)
            {
                return BadRequest(new { message = validationResult.errorMessage });
            }

            try
            {
                await using var stream = file.OpenReadStream();
                var (fileBytes, fileName) = await _analysisService.GenerateDependencyMatrixAsync(
                    stream, 
                    packageFilterPrefix);

                return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, 
                    new { message = "Failed to generate dependency matrix", error = ex.Message });
            }
        }

        /// <summary>
        /// Generates a package dependency diagram in SVG format from a project.assets.json file.
        /// </summary>
        /// <param name="file">The project.assets.json file</param>
        /// <param name="packageFilterPrefix">Optional package name prefix to filter dependencies</param>
        /// <returns>SVG file containing package dependency diagram</returns>
        [HttpPost("create-diagram-package")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(FileContentResult))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [Produces("image/svg+xml")]
        public async Task<IActionResult> CreatePackageDependencyDiagram(
            IFormFile file, 
            [FromQuery] string packageFilterPrefix = "")
        {
            var validationResult = ValidateUploadedFile(file);
            if (!validationResult.isValid)
            {
                return BadRequest(new { message = validationResult.errorMessage });
            }

            try
            {
                await using var stream = file.OpenReadStream();
                var (fileBytes, fileName) = await _analysisService.GeneratePackageDependencyDiagramAsync(
                    stream, 
                    packageFilterPrefix);

                return File(fileBytes, "image/svg+xml", fileName);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, 
                    new { message = "Failed to generate package dependency diagram", error = ex.Message });
            }
        }

        /// <summary>
        /// Generates a repository dependency diagram in SVG format from a project.assets.json file.
        /// </summary>
        /// <param name="file">The project.assets.json file</param>
        /// <param name="packageFilterPrefix">Optional package name prefix to filter dependencies</param>
        /// <returns>SVG file containing repository dependency diagram</returns>
        [HttpPost("create-diagram-repository")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(FileContentResult))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [Produces("image/svg+xml")]
        public async Task<IActionResult> CreateRepositoryDependencyDiagram(
            IFormFile file,
            [FromQuery] string packageFilterPrefix = "")
        {
            var validationResult = ValidateUploadedFile(file);
            if (!validationResult.isValid)
            {
                return BadRequest(new { message = validationResult.errorMessage });
            }

            try
            {
                await using var stream = file.OpenReadStream();
                var (fileBytes, fileName) = await _analysisService.GenerateRepositoryDependencyDiagramAsync(
                    stream,
                    packageFilterPrefix);

                return File(fileBytes, "image/svg+xml", fileName);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { message = "Failed to generate repository dependency diagram", error = ex.Message });
            }
        }

        private (bool isValid, string errorMessage) ValidateUploadedFile(IFormFile file)
        {
            if (file == null)
            {
                return (false, "No file provided.");
            }

            if (file.Length == 0)
            {
                return (false, "Uploaded file is empty.");
            }

            if (file.Length > MaxFileSizeBytes)
            {
                return (false, $"File size exceeds maximum allowed size of {MaxFileSizeBytes / (1024 * 1024)} MB.");
            }

            // Basic file name validation - expect project.assets.json
            if (!file.FileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                return (false, "File must be a JSON file.");
            }

            return (true, string.Empty);
        }
    }
}