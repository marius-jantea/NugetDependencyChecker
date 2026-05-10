using System.Diagnostics;
using System.Text;
using NugetDependencyChecker.BusinessLogic.Models;
using NugetDependencyChecker.BusinessLogic;

namespace NugetDependencyChecker.Implementation;

public class DotDependencyDiagramCreator : IDependencyDiagramCreator
{
    private readonly string? _filePath;
    private const string GraphvizLayoutEngine = "fdp";
    private readonly DependencyDiagramMode _diagramMode;
    private static readonly Lazy<bool> IsDotInstalledCache = new(CheckDotInstalled);
    private static readonly string[] NodeColorPalette =
    {
        "#1f77b4", "#d62728", "#2ca02c", "#ff7f0e", "#9467bd", "#17becf", "#8c564b", "#e377c2"
    };

    public enum DependencyDiagramMode
    {
        Package,
        Repository,
        Both
    }

    public DotDependencyDiagramCreator(string? filePath = null, DependencyDiagramMode diagramMode = DependencyDiagramMode.Both)
    {
        _filePath = filePath;
        _diagramMode = diagramMode;
    }

    public async Task CreateDependencyDiagram(IEnumerable<Package> packages)
    {
        try
        {
            var packageList = packages.ToList();
            var defaultOutputBaseName = $"DependencyDiagram_{DateTime.UtcNow.Ticks}";
            var packageOutputPath = _filePath ?? $"{defaultOutputBaseName}.svg";
            var repositoryOutputPath = _filePath != null
                ? BuildOutputPath(_filePath, "_repositories")
                : $"{defaultOutputBaseName}_repositories.svg";

            if (_diagramMode == DependencyDiagramMode.Package || _diagramMode == DependencyDiagramMode.Both)
            {
                CreateDiagram(packageList, packageOutputPath);
            }

            if (_diagramMode == DependencyDiagramMode.Repository || _diagramMode == DependencyDiagramMode.Both)
            {
                var repositoryPackages = AggregatePackagesByRepository(packageList);
                CreateDiagram(repositoryPackages, repositoryOutputPath);
            }

            await Task.CompletedTask;

        }
        catch
        {
            throw new Exception("An error has occured while generating dot dependency diagram.");
        }
    }

    private void CreateDiagram(IEnumerable<Package> packages, string? outputFilePath)
    {
        var randomFileName = Path.GetRandomFileName() + ".dot";
        var relevantPackagesDotOutput = GetDotOutput(packages);

        File.WriteAllText(randomFileName, relevantPackagesDotOutput.ToString());
        var svgOutputPath = BuildImagePathWithExtension(outputFilePath, ".svg");

        GenerateImageFromDotFile(randomFileName, svgOutputPath, "svg", false);
    }

    private static List<Package> AggregatePackagesByRepository(IEnumerable<Package> packages)
    {
        var packageList = packages.ToList();
        var packageLookup = packageList
            .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);
        var repositoryDependencies = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var package in packageList)
        {
            var sourceRepository = GetRepositoryName(package.Name);
            if (!repositoryDependencies.ContainsKey(sourceRepository))
            {
                repositoryDependencies[sourceRepository] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            foreach (var dependency in package.Dependencies)
            {
                if (!packageLookup.TryGetValue(dependency.Name, out var dependencyPackage))
                {
                    continue;
                }

                var targetRepository = GetRepositoryName(dependencyPackage.Name);
                if (!sourceRepository.Equals(targetRepository, StringComparison.OrdinalIgnoreCase))
                {
                    repositoryDependencies[sourceRepository].Add(targetRepository);
                }
            }
        }

        return repositoryDependencies
            .OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .Select(x => new Package(
                x.Key,
                "repository",
                x.Value
                    .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                    .Select(name => new Package(name, "repository"))
                    .ToList()))
            .ToList();
    }

    private static string? BuildOutputPath(string? filePath, string suffix)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return null;
        }

        var directory = Path.GetDirectoryName(filePath);
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);
        var extension = Path.GetExtension(filePath);
        var repositoryFileName = $"{fileNameWithoutExtension}{suffix}{extension}";
        return string.IsNullOrWhiteSpace(directory)
            ? repositoryFileName
            : Path.Combine(directory, repositoryFileName);
    }

    private static StringBuilder GetDotOutput(IEnumerable<Package> packages)
    {
        var packageList = packages.ToList();
        var dotOutput = new StringBuilder();
        var rootPackageColors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var packageLookup = packageList
            .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);
        var referencingCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var package in packageList)
        {
            foreach (var dependency in package.Dependencies)
            {
                if (!referencingCounts.ContainsKey(dependency.Name))
                {
                    referencingCounts[dependency.Name] = 0;
                }

                referencingCounts[dependency.Name]++;
            }
        }

        dotOutput.AppendLine("digraph G {");
        dotOutput.AppendLine($" layout=\"{GraphvizLayoutEngine}\";");
        dotOutput.AppendLine(" graph [overlap=prism, splines=line, bgcolor=\"#ffffff\", sep=0.3];");
        dotOutput.AppendLine(" node [shape=circle, fixedsize=true, width=1.0, height=1.0, fontname=\"Helvetica\", fontsize=9, fontcolor=\"#111827\", style=filled, fillcolor=\"#ffffff\"];");
        dotOutput.AppendLine(" edge [style=solid, arrowsize=0.8, penwidth=1.5];");

        for (var i = 0; i < packageList.Count; i++)
        {
            var relevantPackage = packageList[i];
            referencingCounts.TryGetValue(relevantPackage.Name, out var countOfIncomingDependencies);
            relevantPackage.Guid = $"a{i}i{countOfIncomingDependencies}o{relevantPackage.Dependencies.Count}";
            relevantPackage.RootPackageName = GetPackageRootName(relevantPackage.Name);
            if (!rootPackageColors.ContainsKey(relevantPackage.RootPackageName))
            {
                rootPackageColors.Add(relevantPackage.RootPackageName, NodeColorPalette[rootPackageColors.Count % NodeColorPalette.Length]);
            }
        }

        foreach (var package in packageList)
        {
            dotOutput.AppendLine(
                $"{package.Guid} [penwidth=2 color=\"{rootPackageColors[package.RootPackageName]}\", label=\"{EscapeDotLabel(package.Guid)}\"];");
            foreach (var dependency in package.Dependencies)
            {
                var packageFromList = packageLookup.GetValueOrDefault(dependency.Name);
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
                    var color = mainPackageRootName.Equals(dependencyPackageRootName) ? "#374151" : "#111827";
                    dotOutput.AppendLine($"{package.Guid} -> {dependency.Guid} [color=\"{color}\"];");
                }
            }
        }

        dotOutput.AppendLine("}");
        return dotOutput;
    }

    private static string GetPackageRootName(string packageName)
    {
        return string.Join("", packageName.Split(".").Take(3));
    }

    private static string GetRepositoryName(string packageName)
    {
        var segments = packageName.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 3)
        {
            return packageName;
        }

        return string.Join('.', segments.Take(3));
    }

    private static string EscapeDotLabel(string value)
    {
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }


    private static string BuildImagePathWithExtension(string? outputFilePath, string extension)
    {
        if (string.IsNullOrWhiteSpace(outputFilePath))
        {
            return string.Empty;
        }

        var directory = Path.GetDirectoryName(outputFilePath);
        var filename = Path.GetFileNameWithoutExtension(outputFilePath);
        var normalizedExtension = extension.StartsWith('.') ? extension : $".{extension}";
        var outputName = $"{filename}{normalizedExtension}";

        return string.IsNullOrWhiteSpace(directory) ? outputName : Path.Combine(directory, outputName);
    }

    private void GenerateImageFromDotFile(string dotFilename, string? outputFilePath, string outputFormat, bool setDpi)
    {
        if (!IsDotInstalledCache.Value)
        {
            Console.WriteLine("Graphviz (dot) is not installed on your system.");
            Console.WriteLine("You can download and install it from:");
            Console.WriteLine("https://graphviz.gitlab.io/download/");

            Console.WriteLine("Press any key to exit.");
            Console.ReadKey();
            return;
        }

        var outputExtension = outputFormat.StartsWith("svg", StringComparison.OrdinalIgnoreCase) ? ".svg" : ".png";
        var outputPath = outputFilePath;
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            outputPath = $"{Path.GetFileNameWithoutExtension(dotFilename)}{outputExtension}";
        }

        ProcessStartInfo processStartInfo = CreateProcessStartInfo(dotFilename, outputPath, outputFormat, setDpi);

        using (Process process = new Process())
        {
            process.StartInfo = processStartInfo;
            Console.WriteLine($"Started dot {outputFormat} generation");
            process.Start();
            var standardOutput = process.StandardOutput.ReadToEnd();
            var standardError = process.StandardError.ReadToEnd();
            process.WaitForExit();
            if (!string.IsNullOrWhiteSpace(standardOutput))
            {
                Console.WriteLine(standardOutput);
            }

            if (!string.IsNullOrWhiteSpace(standardError))
            {
                Console.WriteLine($"Error: {standardError}");
            }

            if (process.ExitCode != 0)
            {
                throw new Exception($"dot failed for {outputFormat} output with exit code {process.ExitCode}.");
            }

            Console.WriteLine($"Finished dot {outputFormat} generation");
        }
    }

    static bool CheckDotInstalled()
    {
        try
        {
            using (Process process = new Process())
            {
                process.StartInfo.FileName = "dot";
                process.StartInfo.Arguments = "-V";
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
            Console.WriteLine($"An error occurred: {ex.Message}");
            return false;
        }
    }

    private static ProcessStartInfo CreateProcessStartInfo(string dotFilename, string outputPath, string outputFormat, bool setDpi)
    {
        // For SVG, skip DPI settings to improve performance; DPI is only relevant for raster formats
        var dpiArgument = (setDpi && !outputFormat.Contains("svg", StringComparison.OrdinalIgnoreCase)) 
            ? " -Gdpi=220" 
            : string.Empty;

        return new ProcessStartInfo
        {
            FileName = "dot",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = false,
            Arguments = $"-T{outputFormat}{dpiArgument} -K{GraphvizLayoutEngine} \"{dotFilename}\" -o \"{outputPath}\""
        };
    }
}