using System.Net;
using Microsoft.AspNetCore.Mvc;
using NugetDependencyChecker.BusinessLogic;
using NugetDependencyChecker.BusinessLogic.Models;
using NugetDependencyChecker.Implementation;

namespace NugetDependencyChecker.WebApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DependencyCheckerController : ControllerBase
    {
        private const string DefaultProjectAssetsJsonPath = "project.assets.json";

        [HttpPost("create-matrix")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(FileContentResult))]
        [Produces("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")] // Excel content type

        public async Task<IActionResult> CreateDependencyMatrix(IFormFile file, [FromQuery] string packageFilterPrefix = "")
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest("No file provided.");
            }

            try
            {
                // Save the uploaded file to a temporary location
                var tempFilePath = Path.GetTempFileName();
                using (var stream = new FileStream(tempFilePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                // Parse the file to get relevant packages
                IPackageDetailsGetter projectAssetsJsonParser = new ProjectAssetsJsonParser(tempFilePath);
                var relevantPackages = projectAssetsJsonParser.GetAllPackages(packageFilterPrefix);

                // Create dependency matrix and save to a temporary file
                var outputFilePath = Path.Combine(Path.GetTempPath(), $"DependencyMatrix_{DateTime.Now.Ticks}.xlsx");
                IDependencyMatrixCreator dependencyMatrixCreator = new ExcelDependencyMatrixCreator(outputFilePath);
                await dependencyMatrixCreator.CreateDependencyMatrix(relevantPackages);

                // Return the file as a response
                var fileBytes = await System.IO.File.ReadAllBytesAsync(outputFilePath);
                var fileName = "DependencyMatrix.xlsx";

                return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
            catch (Exception ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
        }


        [HttpPost("create-diagram")]
        public async Task<IActionResult> CreateDependencyDiagram(IFormFile file, [FromQuery] string packageFilterPrefix = "")
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest("No file provided.");
            }

            try
            {
                // Save the uploaded file to a temporary location
                var tempFilePath = Path.GetTempFileName();
                using (var stream = new FileStream(tempFilePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                // Parse the file to get relevant packages
                IPackageDetailsGetter projectAssetsJsonParser = new ProjectAssetsJsonParser(tempFilePath);
                var relevantPackages = projectAssetsJsonParser.GetAllPackages(packageFilterPrefix);

                // Create dependency diagram and save to a temporary file
                var outputFilePath = Path.Combine(Path.GetTempPath(), "DependencyDiagram.png");
                IDependencyDiagramCreator dependencyDiagramCreator = new DotDependencyDiagramCreator(outputFilePath);
                await dependencyDiagramCreator.CreateDependencyDiagram(relevantPackages);

                // Return the file as a response
                var fileBytes = await System.IO.File.ReadAllBytesAsync(outputFilePath);
                var fileName = "DependencyDiagram.png";

                return File(fileBytes, "image/png", fileName);
            }
            catch (Exception ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
        }

    }
}