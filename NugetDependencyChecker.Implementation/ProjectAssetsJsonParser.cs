using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NugetDependencyChecker.BusinessLogic;
using NugetDependencyChecker.BusinessLogic.Models;

namespace NugetDependencyChecker.Implementation
{
    public class ProjectAssetsJsonParser : IPackageDetailsGetter
    {
        private const string targetsJsonKey = "targets";
        private const string dependenciesJsonKey = "dependencies";

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
            var jsonObject = JObject.Parse(packageInfo);
            var targets = jsonObject[targetsJsonKey] as JObject;

            if (targets == null)
            {
                return allPackages;
            }

            foreach (var targetFramework in targets.Properties())
            {
                var packages = targetFramework.Value as JObject;
                if (packages == null)
                {
                    continue;
                }

                // Iterate through packages for the current target framework
                foreach (var packageProperty in packages.Properties())
                {
                    string nameVersion = packageProperty.Name;

                    (string packageName, string packageVersion) = GetPackageNameAndVersion(nameVersion);

                    if (!StringStartsWithPrefix(packageName, packageFilterPrefix))
                    {
                        continue;
                    }

                    var listOfDependencies = new List<Package>();
                    var packageValue = packageProperty.Value as JObject;
                    var dependencies = packageValue?[dependenciesJsonKey] as JObject;

                    if (dependencies != null)
                    {
                        foreach (var dependency in dependencies)
                        {
                            if (StringStartsWithPrefix(dependency.Key, packageFilterPrefix))
                            {
                                listOfDependencies.Add(new Package(dependency.Key, dependency.Value?.ToString() ?? string.Empty));
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

        private (string name, string version) GetPackageNameAndVersion(string packageName)
        {
            var nameVersionArr = packageName.Split('/');
            if (nameVersionArr.Length < 2)
            {
                return (name: packageName, version: string.Empty);
            }

            return (name: nameVersionArr[0], version: nameVersionArr[1]);
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
