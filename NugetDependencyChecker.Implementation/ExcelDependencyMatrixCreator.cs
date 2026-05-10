using NugetDependencyChecker.BusinessLogic;
using NugetDependencyChecker.BusinessLogic.Models;
using OfficeOpenXml;

namespace NugetDependencyChecker.Implementation
{
    public class ExcelDependencyMatrixCreator : IDependencyMatrixCreator
    {
        private readonly string? _filePath;

        public ExcelDependencyMatrixCreator(string? filePath = null)
        {
            _filePath = filePath;
        }

        public Task CreateDependencyMatrix(IEnumerable<Package> packages)
        {
            try
            {
                var fileName = _filePath ?? $"DependencyMatrix_{DateTime.Now.Ticks}.xlsx";
                var packageList = packages.ToList();

                FileInfo excel = new(fileName);

                using ExcelPackage package = new ExcelPackage(excel);

                CreateRepositoryDependencyWorksheet(package.Workbook.Worksheets, packageList);
                CreatePackageDependencyWorksheet(package.Workbook.Worksheets, packageList);

                package.Save();

                return Task.CompletedTask;
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex);
                return Task.FromException(
                    new Exception("An error has occured while generating excel dependency matrix."));
            }
        }

        private static string GetRepositoryName(string packageName)
        {
            var parts = packageName.Split('.', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 3)
            {
                return string.Join('.', parts.Take(3));
            }

            return packageName;
        }

        private static void CreateRepositoryDependencyWorksheet(ExcelWorksheets worksheets, List<Package> packageList)
        {
            var repositories = packageList
                .Select(x => GetRepositoryName(x.Name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var repositoryIndex = repositories
                .Select((name, index) => new { name, index })
                .ToDictionary(x => x.name, x => x.index, StringComparer.OrdinalIgnoreCase);

            ExcelWorksheet worksheet = worksheets.Add("Repositories");
            PopulateHeaders(worksheet, repositories);

            var packageLookup = packageList
                .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);

            foreach (var sourcePackage in packageList)
            {
                var sourceRepository = GetRepositoryName(sourcePackage.Name);
                if (!repositoryIndex.TryGetValue(sourceRepository, out var sourceRepositoryIndex))
                {
                    continue;
                }

                foreach (var dependency in sourcePackage.Dependencies)
                {
                    if (!packageLookup.TryGetValue(dependency.Name, out var dependencyPackage))
                    {
                        continue;
                    }

                    var targetRepository = GetRepositoryName(dependencyPackage.Name);
                    if (!repositoryIndex.TryGetValue(targetRepository, out var targetRepositoryIndex))
                    {
                        continue;
                    }

                    if (!sourceRepository.Equals(targetRepository, StringComparison.OrdinalIgnoreCase))
                    {
                        worksheet.Cells[sourceRepositoryIndex + 2, targetRepositoryIndex + 2].Value = "dependency";
                    }
                }
            }
        }

        private static void CreatePackageDependencyWorksheet(ExcelWorksheets worksheets, List<Package> packageList)
        {
            var packageNames = packageList
                .Select(x => x.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var packageIndex = packageNames
                .Select((name, index) => new { name, index })
                .ToDictionary(x => x.name, x => x.index, StringComparer.OrdinalIgnoreCase);

            ExcelWorksheet worksheet = worksheets.Add("Packages");
            PopulateHeaders(worksheet, packageNames);

            foreach (var sourcePackage in packageList)
            {
                if (!packageIndex.TryGetValue(sourcePackage.Name, out var sourcePackageIndex))
                {
                    continue;
                }

                foreach (var dependency in sourcePackage.Dependencies)
                {
                    if (packageIndex.TryGetValue(dependency.Name, out var targetPackageIndex))
                    {
                        worksheet.Cells[sourcePackageIndex + 2, targetPackageIndex + 2].Value = "dependency";
                    }
                }
            }
        }

        private static void PopulateHeaders(ExcelWorksheet worksheet, List<string> items)
        {
            for (int i = 0; i < items.Count; i++)
            {
                worksheet.Cells[1, i + 2].Value = items[i];
                worksheet.Cells[i + 2, 1].Value = items[i];
            }
        }
    }
}