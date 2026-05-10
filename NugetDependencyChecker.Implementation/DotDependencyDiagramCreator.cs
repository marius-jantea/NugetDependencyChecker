using System.Diagnostics;
using System.Text;
using NugetDependencyChecker.BusinessLogic.Models;
using NugetDependencyChecker.BusinessLogic;

namespace NugetDependencyChecker.Implementation;

public class DotDependencyDiagramCreator : IDependencyDiagramCreator
{
    private readonly string? _filePath;
    private const string PackageLayoutEngine = "fdp";
    private const string RepositoryLayoutEngine = "dot";
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
        var layoutEngine = _diagramMode == DependencyDiagramMode.Repository
            ? RepositoryLayoutEngine
            : PackageLayoutEngine;

        File.WriteAllText(randomFileName, relevantPackagesDotOutput.ToString());
        var svgOutputPath = BuildImagePathWithExtension(outputFilePath, ".svg");

        GenerateImageFromDotFile(randomFileName, svgOutputPath, "svg", false, layoutEngine);
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

    private StringBuilder GetDotOutput(IEnumerable<Package> packages)
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

        var isRepositoryDiagram = _diagramMode == DependencyDiagramMode.Repository;
        dotOutput.AppendLine("digraph G {");
        var layoutEngine = isRepositoryDiagram ? RepositoryLayoutEngine : PackageLayoutEngine;
        dotOutput.AppendLine($" layout=\"{layoutEngine}\";");

            var graphAttributes = isRepositoryDiagram
                ? " graph [overlap=prism, splines=true, bgcolor=\"#ffffff\", sep=0.7, nodesep=0.8, size=\"14,14!\", ratio=fill, pad=0.4];"
                : " graph [overlap=prism, splines=true, bgcolor=\"#ffffff\", sep=0.3, nodesep=0.4, size=\"12,12!\", ratio=fill, pad=0.3];";
        var nodeAttributes = isRepositoryDiagram
            ? " node [shape=circle, fixedsize=true, width=13.0, height=13.0, fontname=\"Helvetica\", fontsize=170, fontcolor=\"#111827\", style=filled, fillcolor=\"#ffffff\"];"
            : " node [shape=circle, fixedsize=true, width=10.0, height=10.0, fontname=\"Helvetica\", fontsize=150, fontcolor=\"#111827\", style=filled, fillcolor=\"#ffffff\"];";
        var edgeAttributes = isRepositoryDiagram
            ? " edge [style=solid, arrowsize=4.5, penwidth=5.0, minlen=3, tailclip=true, headclip=true];"
            : " edge [style=solid, arrowsize=4.0, penwidth=4.5, minlen=2, tailclip=true, headclip=true];";
            var nodeBorderPenWidth = isRepositoryDiagram ? 24 : 21;

        dotOutput.AppendLine(graphAttributes);
        dotOutput.AppendLine(nodeAttributes);
        dotOutput.AppendLine(edgeAttributes);

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
                $"{package.Guid} [penwidth={nodeBorderPenWidth} color=\"{rootPackageColors[package.RootPackageName]}\", label=\"{EscapeDotLabel(package.Guid)}\"];");
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

    private void GenerateImageFromDotFile(string dotFilename, string? outputFilePath, string outputFormat, bool setDpi, string layoutEngine)
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

        ProcessStartInfo processStartInfo = CreateProcessStartInfo(dotFilename, outputPath, outputFormat, setDpi, layoutEngine);

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

    private static ProcessStartInfo CreateProcessStartInfo(string dotFilename, string outputPath, string outputFormat, bool setDpi, string layoutEngine)
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
            Arguments = $"-T{outputFormat}{dpiArgument} -K{layoutEngine} \"{dotFilename}\" -o \"{outputPath}\""
        };
    }
}