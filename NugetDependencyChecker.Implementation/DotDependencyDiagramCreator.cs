using System.Diagnostics;
using System.Drawing;
using System.Text;
using NugetDependencyChecker.BusinessLogic.Models;
using NugetDependencyChecker.BusinessLogic;

namespace NugetDependencyChecker.Implementation;

public class DotDependencyDiagramCreator : IDependencyDiagramCreator
{
    public Task CreateDependencyDiagram(IEnumerable<Package> packages)
    {
        try
        {
            var randomFileName = Path.GetRandomFileName() + ".dot";
            var relevantPackagesDotOutput = GetDotOutput(packages);

            File.WriteAllText(randomFileName, relevantPackagesDotOutput.ToString());
            GeneratePngFromDotFile(randomFileName);
            return Task.CompletedTask;
        }
        catch
        {
            return Task.FromException(new Exception("An error has occured while generating dot dependency diagram."));
        }
    }

    private static StringBuilder GetDotOutput(IEnumerable<Package> packages)
    {
        var dotOutput = new StringBuilder();
        Dictionary<string, Color> rootPackageColors = new Dictionary<string, Color>();
        dotOutput.AppendLine("digraph G {");
        dotOutput.AppendLine(" layout=\"twopi\";");
        dotOutput.AppendLine(" ranksep=3 // set radius, in inches");
        dotOutput.AppendLine(" node [shape = circle];");
        dotOutput.AppendLine(" edge [style=solid];");
        dotOutput.AppendLine(" graph [overlap=false, splines=true];");
        dotOutput.AppendLine(" //overlap = scale;");
        dotOutput.AppendLine(" edge [concentrate=true]");
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
                    dotOutput.AppendLine($"{package.Guid} -> {dependency.Guid} [color=\"{color}\"];");
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


    private static void GeneratePngFromDotFile(string dotFilename)
    {
        if (!IsDotInstalled())
        {
            Console.WriteLine("Graphviz (dot) is not installed on your system.");
            Console.WriteLine("You can download and install it from:");
            Console.WriteLine("https://graphviz.gitlab.io/download/");

            Console.WriteLine("Press any key to exit.");
            Console.ReadKey();
            return;
        }

        string command =
        $"dot -Tpng -Kcirco {dotFilename} -o {dotFilename.Split(".")[0]}.png";
        ProcessStartInfo processStartInfo = CreateProcessStartInfo(command);

        using (Process process = new Process())
        {
            process.StartInfo = processStartInfo;
            process.OutputDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                {
                    Console.WriteLine(e.Data);
                }
            };
            process.ErrorDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                {
                    Console.WriteLine($"Error: {e.Data}");
                }
            };
            Console.WriteLine("Started dot png generation");
            process.Start();
            process.WaitForExit();
            Console.WriteLine("Finished dot png generation");
        }
    }

    static bool IsDotInstalled()
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

    private static ProcessStartInfo CreateProcessStartInfo(string command)
    {
        var arguments = string.Empty;
        if (IsMacOS() || IsLinux())
        {
            arguments = $"-c \"{command}\"";
        }
        else
        {
            arguments = $"/C {command}";
        }

        return new ProcessStartInfo
        {
            FileName = GetShellName(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = false,
            Arguments = arguments
        };
    }

    static string GetShellName()
    {
        if (IsWindows())
        {
            return "cmd.exe";
        }

        if (IsMacOS() || IsLinux())
        {
            return "/bin/bash";
        }

        throw new NotSupportedException("Unsupported operating system.");
    }

    private static bool IsWindows()
    {
        int platform = (int)Environment.OSVersion.Platform;
        return (platform != 4 && platform != 6 && platform != 128);
    }

    private static bool IsMacOS()
    {
        return Environment.OSVersion.Platform == PlatformID.MacOSX;
    }

    private static bool IsLinux()
    {
        return Environment.OSVersion.Platform == PlatformID.Unix;
    }
}