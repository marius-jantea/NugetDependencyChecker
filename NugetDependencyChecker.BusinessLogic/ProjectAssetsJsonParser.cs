using Newtonsoft.Json.Linq;
using NugetDependencyChecker.BusinessLogic.Models;

namespace NugetDependencyChecker.BusinessLogic
{
    public class Parser
    {
        private readonly string jsonPath;
        private readonly string packageFilterPrefix;
        private readonly IList<Package> allPackages;

        public Parser(string projectAssetsJsonPath, string packageFilterPrefix)
        {
            jsonPath = projectAssetsJsonPath;
            this.packageFilterPrefix = packageFilterPrefix;
            allPackages = new List<Package>();
        }

        public IEnumerable<Package> Parse()
        {
            var packageInfo = File.ReadAllText(jsonPath);
            JObject obj = JObject.Parse(packageInfo);
            JObject targets = (JObject)obj["targets"][".NETStandard,Version=v2.1"];

            foreach (JProperty package in targets.Children())
            {
                string nameVersion = package.Name;
                string[] nameVersionArr = nameVersion.Split('/');
                string packageName = nameVersionArr[0];
                string packageVersion = nameVersionArr[1];

                var listOfDependencies = new List<Package>();

                JObject dependencies = (JObject)package.First["dependencies"];

                if (dependencies != null)
                {
                    foreach (var dependency in dependencies)
                    {
                        listOfDependencies.Add(new Package(dependency.Key, dependency.Value.ToString()));
                    }
                }

                listOfDependencies = listOfDependencies.Where(x => x.Name.StartsWith(packageFilterPrefix, StringComparison.OrdinalIgnoreCase)).ToList();
                allPackages.Add(new Package(packageName, packageVersion, listOfDependencies));
            }

            var result = GetFilteredPackages(packageFilterPrefix);
            foreach (var package in result)
            {
                foreach (var dependency in allPackages.Where(x => x.Dependencies.Any(y => y.Name.Equals(package.Name))))
                {
                    var dependencyFromDependency = dependency.Dependencies.First(x => x.Name.Equals(package.Name));
                    package.PackagesThatUseThisPackage.Add(new ChildPackage(dependency.Name, dependencyFromDependency.Version, dependency.Version));
                }
            }
            return result;
        }

        private IEnumerable<Package> GetFilteredPackages(string relevantPackagesPrefix)
        {
            return string.IsNullOrEmpty(relevantPackagesPrefix) ? allPackages : allPackages.Where(x => x.Name.StartsWith(relevantPackagesPrefix, StringComparison.OrdinalIgnoreCase)).ToList();
        }

    }
}
