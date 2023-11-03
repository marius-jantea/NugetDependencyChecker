using System.Diagnostics;
using System.Drawing;
using System.Text;
using NugetDependencyChecker.BusinessLogic;
using NugetDependencyChecker.BusinessLogic.Models;
using OfficeOpenXml;

namespace NugetDependencyChecker.ConsoleApp
{
    internal class Program
    {
        private static int numberOfDependenciesThatCanBeRemoved;
        private static int totalNumberOfDependencies;

        const string projectAssetsJsonPath = "project.assets.json";
        private static IEnumerable<Package> relevantPackages = new List<Package>();

        static void Main(string[] args)
        {
            var jsonPath = GetProjectAssetsJsonFilePath(args);

            var packageFilterPrefix = GetPackageFilterPrefix(args);

            var projectAssetsJsonParser = new Parser(jsonPath, packageFilterPrefix);
            relevantPackages = projectAssetsJsonParser.Parse();

            WriteDependencyMatrixToExcel(relevantPackages);

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

            string json = System.Text.Json.JsonSerializer.Serialize(relevantPackages);
            File.WriteAllText(@"path.json", json);

            var relevantPackagesDotOutput = GetDotOutput(relevantPackages);
            File.WriteAllText(@"originalPackages.dot", relevantPackagesDotOutput.ToString());
            GeneratePngFromDotFile("originalPackages.dot");

            RemoveDirectDependenciesThatAreTransient();
            var optimizedPackagesDotOutput = GetDotOutput(relevantPackages);
            File.WriteAllText(@"optimizedPackages.dot", optimizedPackagesDotOutput.ToString());
            GeneratePngFromDotFile("optimizedPackages.dot");
        }

        private static void RemoveDirectDependenciesThatAreTransient()
        {
            foreach (var package in relevantPackages)
            {
                totalNumberOfDependencies += package.Dependencies.Count();
                var allTransientDependencies = GetAllTransientDependenciesForPackage(package);
                foreach (var dependency in package.Dependencies.ToList())
                {
                    if (allTransientDependencies.Any(x => x.Name.Equals(dependency.Name)))
                    {
                        package.Dependencies.Remove(dependency);
                        numberOfDependenciesThatCanBeRemoved++;
                    }
                }
            }
        }

        private static void GeneratePngFromDotFile(string dotFilename)
        {
            if (!IsDotInstalled())
            {
                Console.WriteLine("Graphviz (dot) is not installed on your system.");
                Console.WriteLine("You can download and install it from:");
                Console.WriteLine("https://graphviz.gitlab.io/download/");

                // Optionally, you can open a web browser to the download page
                // OpenGraphvizDownloadPage();

                Console.WriteLine("Press any key to exit.");
                Console.ReadKey();
                return;
            }

            string command = $"dot -Tpng -Kcirco {dotFilename} -o {dotFilename.Split(".")[0]}.png"; // Replace with your desired terminal command
            ProcessStartInfo processStartInfo = CreateProcessStartInfo(command);

            // Create and start the process
            using (Process process = new Process())
            {
                process.StartInfo = processStartInfo;
                Console.WriteLine("Started dot png generation");
                process.Start();

                // Read the output (stdout and stderr) of the command
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();

                process.WaitForExit();

                // Display the output and error messages
                Console.WriteLine("Output:");
                Console.WriteLine(output);

                if (!string.IsNullOrWhiteSpace(error))
                {
                    Console.WriteLine("Error:");
                    Console.WriteLine(error);
                }
            }
        }

        static bool IsDotInstalled()
        {
            try
            {
                using (Process process = new Process())
                {
                    process.StartInfo.FileName = "dot";
                    process.StartInfo.Arguments = "--help";
                    process.StartInfo.RedirectStandardError = true;
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.CreateNoWindow = true;
                    process.Start();

                    process.WaitForExit();

                    return process.ExitCode == 0;
                }
            }
            catch (Exception ex)
            {
                // Handle the exception (e.g., log or display an error message)
                Console.WriteLine($"An error occurred: {ex.Message}");
                return false;
            }
        }

        static void OpenGraphvizDownloadPage()
        {
            Process.Start("https://graphviz.gitlab.io/download/");
        }


        private static ProcessStartInfo CreateProcessStartInfo(string command)
        {

            // Create a process start info
            ProcessStartInfo processStartInfo = new ProcessStartInfo
            {
                FileName = GetShellName(),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                Arguments = $"-c \"{command}\""
            };
            return processStartInfo;
        }

        static string GetShellName()
        {
            if (IsWindows())
            {
                return "cmd.exe";
            }
            else if (IsMacOS() || IsLinux())
            {
                return "/bin/bash"; // On macOS and many Linux distributions
            }
            else
            {
                throw new NotSupportedException("Unsupported operating system.");
            }
        }

        private static bool IsWindows()
        {
            int platform = (int)Environment.OSVersion.Platform;
            return (platform != 4 && platform != 6 && platform != 128);
        }

        // Function to check if the OS is macOS
        private static bool IsMacOS()
        {
            return Environment.OSVersion.Platform == PlatformID.MacOSX;
        }

        // Function to check if the OS is Linux
        private static bool IsLinux()
        {
            return Environment.OSVersion.Platform == PlatformID.Unix;
        }

        private static StringBuilder GetDotOutput(IEnumerable<Package> packages)
        {
            var dotOutput = new StringBuilder();
            Dictionary<string, Color> rootPackageColors = new Dictionary<string, Color>();
            dotOutput.AppendLine("digraph G {");
            dotOutput.AppendLine("    layout=\"twopi\";");
            dotOutput.AppendLine("    ranksep=3 // set radius, in inches");
            dotOutput.AppendLine("    node [shape = circle];");
            dotOutput.AppendLine("    edge [style=solid];");
            dotOutput.AppendLine("    graph [overlap=false, splines=true];");
            dotOutput.AppendLine("    //overlap = scale;");
            dotOutput.AppendLine("    edge  [concentrate=true]");
            for (var i = 0; i < packages.Count(); i++)
            {
                var relevantPackage = packages.ElementAt(i);
                var allPackages = packages.Where(x => x.Dependencies.Any(y => y.Name.Equals(relevantPackage.Name))).ToList();
                relevantPackage.Guid = $"a{i}i{allPackages.Count()}o{relevantPackage.Dependencies.Count()}";
                relevantPackage.RootPackageName = GetPackageRootName(relevantPackage.Name);
                if (!rootPackageColors.ContainsKey(relevantPackage.RootPackageName))
                {
                    rootPackageColors.Add(relevantPackage.RootPackageName, GetRandomColor());
                }
            }
            foreach (var package in packages)
            {
                dotOutput.AppendLine($"{package.Guid} [penwitdh=20 color=\"{rootPackageColors[package.RootPackageName].Name}\"];");
                foreach (var dependency in package.Dependencies)
                {
                    var packageFromList = packages.FirstOrDefault(x => x.Name.Equals(dependency.Name));
                    if (packageFromList != null)
                    {
                        dependency.Guid = packageFromList.Guid;
                    }
                }

                var mainPackageRootName = GetPackageRootName(package.Name);
                foreach (var dependency in package.Dependencies)
                {
                    if (!string.IsNullOrEmpty(dependency.Guid))
                    {
                        var dependencyPackageRootName = GetPackageRootName(dependency.Name);
                        var color = mainPackageRootName.Equals(dependencyPackageRootName) ? "gray88" : "black";
                        dotOutput.AppendLine($"{package.Guid} -> {dependency.Guid}  [color=\"{color}\"];");
                    }
                }
            }

            dotOutput.AppendLine("}");
            return dotOutput;
        }

        private static Color GetRandomColor()
        {
            Random randomGen = new Random();
            KnownColor[] names = (KnownColor[])Enum.GetValues(typeof(KnownColor));
            KnownColor randomColorName = names[randomGen.Next(30, 166)];
            return Color.FromKnownColor(randomColorName);
        }

        private static string GetPackageRootName(string packageName)
        {
            return string.Join("", packageName.Split(".").Take(3));
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

        private static void WriteDependencyMatrixToExcel(IEnumerable<Package> relevantPackages)
        {
            var fileName = $"DependencyMatrix_{DateTime.Now.Ticks}.xlsx";

            FileInfo excel = new(fileName);

            using ExcelPackage package = new ExcelPackage(excel);

            ExcelWorksheet worksheetWork = package.Workbook.Worksheets.Add("Work");

            for (int i = 1; i <= relevantPackages.Count(); i++)
            {
                worksheetWork.Cells[1, i + 1].Value = relevantPackages.ElementAt(i - 1).Name;
            }

            for (int i = 1; i <= relevantPackages.Count(); i++)
            {
                worksheetWork.Cells[i + 1, 1].Value = relevantPackages.ElementAt(i - 1).Name;
            }

            for (int i = 1; i <= relevantPackages.Count(); i++)
            {
                var packageOnRow = relevantPackages.ElementAt(i - 1);
                foreach (var dependency in packageOnRow.Dependencies)
                {
                    var dependencyInRelevantPackages = relevantPackages.FirstOrDefault(x => x.Name.Equals(dependency.Name));
                    if (dependencyInRelevantPackages != null)
                    {
                        var indexOfDependency = relevantPackages.ToList().IndexOf(dependencyInRelevantPackages);

                        worksheetWork.Cells[i + 1, indexOfDependency + 2].Value = "dependency";
                    }
                }
            }

            package.Save();
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