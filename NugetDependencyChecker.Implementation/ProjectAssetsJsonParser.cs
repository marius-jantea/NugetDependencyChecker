using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NugetDependencyChecker.BusinessLogic;
using NugetDependencyChecker.BusinessLogic.Models;

namespace NugetDependencyChecker.Implementation
{
    public class ProjectAssetsJsonParser : IPackageDetailsGetter
    {
        private const string librariesJsonKey = "targets";
        private const string dependenciesJsonKey = "dependencies";
        private const char nameVersionSeparator = '/';

        private readonly string jsonPath;
        private readonly IList<Package> allPackages;

        public ProjectAssetsJsonParser(string projectAssetsJsonPath)
        {
            jsonPath = projectAssetsJsonPath;
            allPackages = new List<Package>();
        }

        public IEnumerable<Package> GetAllPackages(string packageFilterPrefix)
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

                    if (!StringStartsWithPrefix(packageName, packageFilterPrefix))
                    {
                        continue;
                    }
                    var listOfDependencies = new List<Package>();
                    JObject dependencies = (JObject)package.First["dependencies"];

                    if (dependencies != null)
                    {
                        foreach (var dependency in dependencies)
                        {
                            if (StringStartsWithPrefix(dependency.Key, packageFilterPrefix))
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

        private IEnumerable<Package> PackagesThatAreDependentOnPackage(string packageName)
        {
            return allPackages.Where(x => x.Dependencies.Any(y => y.Name.Equals(packageName))).ToList();
        }

        private bool StringStartsWithPrefix(string stringToBeVerified, string prefix)
        {
            if (string.IsNullOrEmpty(prefix))
            {
                return true;
            }
            return stringToBeVerified.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }
    }
}
