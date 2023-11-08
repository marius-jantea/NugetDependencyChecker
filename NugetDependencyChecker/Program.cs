using NugetDependencyChecker.BusinessLogic;
using NugetDependencyChecker.BusinessLogic.Models;
using NugetDependencyChecker.Implementation;

namespace NugetDependencyChecker.ConsoleApp
{
    internal class Program
    {
        const string projectAssetsJsonPath = "project.assets.json";
        private static IEnumerable<Package> relevantPackages = new List<Package>();

        static async Task Main(string[] args)
        {
            var jsonPath = GetProjectAssetsJsonFilePath(args);

            var packageFilterPrefix = GetPackageFilterPrefix(args);

            IPackageDetailsGetter projectAssetsJsonParser = new ProjectAssetsJsonParser(jsonPath);
            IDependencyMatrixCreator dependencyMatrixCreator = new ExcelDependencyMatrixCreator();
            IDependencyDiagramCreator dependencyDiagramCreator = new DotDependencyDiagramCreator();

            relevantPackages = projectAssetsJsonParser.GetAllPackages(packageFilterPrefix);

            await dependencyMatrixCreator.CreateDependencyMatrix(relevantPackages);

            OutputGeneralPackageInformationToConsole(packageFilterPrefix);

            await dependencyDiagramCreator.CreateDependencyDiagram(relevantPackages);

            RemoveDirectDependenciesThatAreTransient();
            await dependencyDiagramCreator.CreateDependencyDiagram(relevantPackages);
        }

        private static void OutputGeneralPackageInformationToConsole(string packageFilterPrefix)
        {
            Console.WriteLine($"There are {relevantPackages.Count()} {packageFilterPrefix}.* packages.");
            Console.WriteLine($"There are {relevantPackages.DistinctBy(x => x.Name).Count()} distinct packages.");
            Console.WriteLine($"There are {relevantPackages.Where(x => x.Name.Split(".").Count() == 3).Count()} main packages (..*)");
            var index = 0;
            foreach (var package in relevantPackages.OrderByDescending(x => x.NumberOfPackagesThatUseThisPackage))
            {
                Console.WriteLine("--------------------------------------------");
                var relevantDependencies = package.Dependencies.Where(x => x.Name.StartsWith(packageFilterPrefix));

                Console.WriteLine($"{index++}. {package.Name} - {package.Version} has {relevantDependencies.Count()} {packageFilterPrefix}.* dependencies and is referenced in {package.NumberOfPackagesThatUseThisPackage} packages.");
            }

            Console.WriteLine($"In average, {packageFilterPrefix}.* packages have {relevantPackages.Average(x => x.Dependencies.Count())} {packageFilterPrefix}.* dependencies");
            Console.WriteLine($"In average, {packageFilterPrefix}.* packages are referenced in {relevantPackages.Average(x => x.NumberOfPackagesThatUseThisPackage)} packages");

            for (int i = 0; i <= relevantPackages.Max(x => x.NumberOfPackagesThatUseThisPackage); i++)
            {
                var referencedPackages = relevantPackages.Where(x => x.NumberOfPackagesThatUseThisPackage == i);
                if (referencedPackages.Any())
                {
                    Console.WriteLine($"There are {referencedPackages.Count()} packages that are refered in {i} projects");
                }
            }

            var top = 10;

            Console.WriteLine("-----------------------------------------");
            Console.WriteLine($"Top {top} for most used packages inside the solution");

            foreach (var package in relevantPackages.OrderByDescending(x => x.NumberOfPackagesThatUseThisPackage).Take(top))
            {
                Console.WriteLine($"{package.Name} is referenced by another {package.NumberOfPackagesThatUseThisPackage} nuget packages with {package.PackagesThatUseThisPackage.Select(x => x.ParentVersion).Distinct().Count()} different versions");
            }

            Console.WriteLine("-----------------------------------------");
            Console.WriteLine($"Top {top} for packages used with the highest number of different versions inside the solution");

            foreach (var package in relevantPackages.
                                    OrderByDescending(x => x.PackagesThatUseThisPackage.Select(x => x.ParentVersion).Distinct().Count()).
                                    Take(top))
            {
                Console.WriteLine($"{package.Name} is referenced by another {package.NumberOfPackagesThatUseThisPackage} nuget packages with {package.PackagesThatUseThisPackage.Select(x => x.ParentVersion).Distinct().Count()} different versions");
                if (package.PackagesThatUseThisPackage.Any(x => x.ParentVersion.Split(".")[0] != package.Version.Split(".")[0]))
                {
                    Console.WriteLine($"Alert! {package.Name}");
                }
            }
        }

        private static void RemoveDirectDependenciesThatAreTransient()
        {
            foreach (var package in relevantPackages)
            {
                var allTransientDependencies = GetAllTransientDependenciesForPackage(package);
                foreach (var dependency in package.Dependencies.ToList())
                {
                    if (allTransientDependencies.Any(x => x.Name.Equals(dependency.Name)))
                    {
                        package.Dependencies.Remove(dependency);
                    }
                }
            }
        }


        private static IEnumerable<Package> GetAllTransientDependenciesForPackage(Package package)
        {
            var result = new List<Package>();
            foreach (var dependency in package.Dependencies)
            {
                var relevantDependency = relevantPackages.FirstOrDefault(x => x.Name.Equals(dependency.Name));
                if (relevantDependency != null)
                {
                    result.AddRange(relevantDependency.Dependencies);
                    result.AddRange(GetAllTransientDependenciesForPackage(dependency));
                }
            }

            return result;
        }



        private static string GetProjectAssetsJsonFilePath(string[] args)
        {
            var jsonPath = string.Empty;
            if (File.Exists(projectAssetsJsonPath))
            {
                jsonPath = projectAssetsJsonPath;
            }
            else if (args.Any() && !string.IsNullOrEmpty(args[0]))
            {
                jsonPath = args[0];
            }

            else
            {
                Console.WriteLine("Enter the project.assets.json path");
                jsonPath = Console.ReadLine();
            }

            return jsonPath ?? throw new Exception("jsonPath cannot be null");
        }

        private static string GetPackageFilterPrefix(string[] args)
        {
            string relevantPackagesPrefix = string.Empty;
            if (args.Any() && !string.IsNullOrEmpty(args[1]))
            {
                relevantPackagesPrefix = args[1];
            }
            else
            {
                Console.WriteLine("Enter the relevant package prefix");
                relevantPackagesPrefix = Console.ReadLine() ?? "";
            }

            return relevantPackagesPrefix;
        }
    }
}