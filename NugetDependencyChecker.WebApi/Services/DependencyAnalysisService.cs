using NugetDependencyChecker.BusinessLogic;
using NugetDependencyChecker.BusinessLogic.Models;
using NugetDependencyChecker.Implementation;

namespace NugetDependencyChecker.WebApi.Services
{
    public interface IDependencyAnalysisService
    {
        Task<(byte[] fileBytes, string fileName)> GenerateDependencyMatrixAsync(
            Stream fileStream, 
            string packageFilterPrefix = "");

        Task<(byte[] fileBytes, string fileName)> GeneratePackageDependencyDiagramAsync(
            Stream fileStream,
            string packageFilterPrefix = "");

        Task<(byte[] fileBytes, string fileName)> GenerateRepositoryDependencyDiagramAsync(
            Stream fileStream, 
            string packageFilterPrefix = "");
    }

    public class DependencyAnalysisService : IDependencyAnalysisService
    {
        private readonly IDependencyMatrixCreator _matrixCreator;

        public DependencyAnalysisService(
            IDependencyMatrixCreator matrixCreator,
            IDependencyDiagramCreator diagramCreator)
        {
            _matrixCreator = matrixCreator;
        }

        public async Task<(byte[] fileBytes, string fileName)> GenerateDependencyMatrixAsync(
            Stream fileStream, 
            string packageFilterPrefix = "")
        {
            var tempInputPath = Path.GetTempFileName();
            var tempOutputPath = Path.Combine(Path.GetTempPath(), $"DependencyMatrix_{DateTime.Now.Ticks}.xlsx");

            try
            {
                // Save uploaded file
                await using (var fileStreamWriter = new FileStream(tempInputPath, FileMode.Create))
                {
                    await fileStream.CopyToAsync(fileStreamWriter);
                }

                // Parse packages
                var packageDetailsGetter = new ProjectAssetsJsonParser(tempInputPath);
                var packages = packageDetailsGetter.GetAllPackages(packageFilterPrefix);

                // Generate matrix
                var matrixCreatorWithPath = new ExcelDependencyMatrixCreator(tempOutputPath);
                await matrixCreatorWithPath.CreateDependencyMatrix(packages);

                // Read output
                var fileBytes = await File.ReadAllBytesAsync(tempOutputPath);
                return (fileBytes, "DependencyMatrix.xlsx");
            }
            finally
            {
                // Cleanup temporary files
                CleanupFile(tempInputPath);
                CleanupFile(tempOutputPath);
            }
        }

    public Task<(byte[] fileBytes, string fileName)> GeneratePackageDependencyDiagramAsync(
        Stream fileStream,
        string packageFilterPrefix = "")
    {
        return GenerateDependencyDiagramByModeAsync(
            fileStream,
            packageFilterPrefix,
            DotDependencyDiagramCreator.DependencyDiagramMode.Package,
            "DependencyDiagram_Packages.svg");
    }

    public Task<(byte[] fileBytes, string fileName)> GenerateRepositoryDependencyDiagramAsync(
        Stream fileStream,
        string packageFilterPrefix = "")
    {
        return GenerateDependencyDiagramByModeAsync(
            fileStream,
            packageFilterPrefix,
            DotDependencyDiagramCreator.DependencyDiagramMode.Repository,
            "DependencyDiagram_Repositories.svg");
    }

    private static async Task<(byte[] fileBytes, string fileName)> GenerateDependencyDiagramByModeAsync(
        Stream fileStream, 
        string packageFilterPrefix,
        DotDependencyDiagramCreator.DependencyDiagramMode mode,
        string outputFileName)
    {
        var tempInputPath = Path.GetTempFileName();
        var tempOutputPath = Path.Combine(Path.GetTempPath(), $"DependencyDiagram_{DateTime.Now.Ticks}.svg");

        try
        {
            // Save uploaded file
            await using (var fileStreamWriter = new FileStream(tempInputPath, FileMode.Create))
            {
                await fileStream.CopyToAsync(fileStreamWriter);
            }

            // Parse packages
            var packageDetailsGetter = new ProjectAssetsJsonParser(tempInputPath);
            var packages = packageDetailsGetter.GetAllPackages(packageFilterPrefix);

            // Generate diagram - use mode-specific creator with custom output path
            var diagramCreatorWithPath = new DotDependencyDiagramCreator(tempOutputPath, mode);
            await diagramCreatorWithPath.CreateDependencyDiagram(packages);

            // Read output
            var fileBytes = await File.ReadAllBytesAsync(tempOutputPath);
            return (fileBytes, outputFileName);
        }
        finally
        {
            // Cleanup temporary files
            CleanupFile(tempInputPath);
            CleanupFile(tempOutputPath);
        }
    }

        private static void CleanupFile(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
            catch
            {
                // Log if needed; don't fail if cleanup fails
            }
        }
    }
}
