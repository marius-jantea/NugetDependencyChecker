using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NugetDependencyChecker.BusinessLogic.Models;

namespace NugetDependencyChecker.BusinessLogic
{
    public class Parser
    {
        private const string librariesJsonKey = "targets";
        private const string dependenciesJsonKey = "dependencies";
        private const char nameVersionSeparator = '/';

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
            var obj = JObject.Parse(packageInfo);

            var jsonObject = JsonConvert.DeserializeObject<dynamic>(packageInfo);
            foreach (var targetFramework in jsonObject.targets)
            {
                var packages = targetFramework.Value;

                // Iterate through packages for the current target framework
                foreach (var package in packages)
                {
                    string nameVersion = package.Name;
                    string[] nameVersionArr = nameVersion.Split('/');
                    string packageName = nameVersionArr[0];
                    string packageVersion = nameVersionArr[1];

                    if (!StringStartsWithPrefix(packageName))
                    {
                        continue;
                    }

                    var listOfDependencies = new List<Package>();

                    JObject dependencies = (JObject)package.First["dependencies"];

                    if (dependencies != null)
                    {
                        foreach (var dependency in dependencies)
                        {
                            if (StringStartsWithPrefix(dependency.Key))
                            {
                                listOfDependencies.Add(new Package(dependency.Key, dependency.Value.ToString()));
                            }
                        }
                    }


                    allPackages.Add(new Package(packageName, packageVersion, listOfDependencies));

                }

                foreach (var package in allPackages)
                {
                    foreach (var dependency in allPackages.Where(x => x.Dependencies.Any(y => y.Name.Equals(package.Name))))
                    {
                        var dependencyFromDependency = dependency.Dependencies.First(x => x.Name.Equals(package.Name));
                        package.PackagesThatUseThisPackage.Add(new ChildPackage(dependency.Name, dependencyFromDependency.Version, dependency.Version));
                    }
                }
            }
            return allPackages;
        }

        private bool NameStartsWithPrefix(string name)
        {
            return name.StartsWith(packageFilterPrefix, StringComparison.OrdinalIgnoreCase);
        }

        private IEnumerable<Package> PackagesThatAreDependentOnPackage(string packageName)
        {
            return allPackages.Where(x => x.Dependencies.Any(y => y.Name.Equals(packageName))).ToList();
        }

        private IEnumerable<Package> GetFilteredPackages(string relevantPackagesPrefix)
        {
            return string.IsNullOrEmpty(relevantPackagesPrefix) ? allPackages : allPackages.Where(x => StringStartsWithPrefix(x.Name)).ToList();
        }

        private bool StringStartsWithPrefix(string stringToBeVerified)
        {
            if (string.IsNullOrEmpty(packageFilterPrefix))
            {
                return true;
            }
            return stringToBeVerified.StartsWith(packageFilterPrefix, StringComparison.OrdinalIgnoreCase);
        }
    }
}