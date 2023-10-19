using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace NugetDependencyChecker.BusinessLogic.Models
{
    public class ChildPackage
    {
        public string Name { get; set; }
        public string ParentVersion { get; set; }
        public string Version { get; }

        public ChildPackage(string name, string parentVersion, string version)
        {
            Name = name;
            ParentVersion = parentVersion;
            Version = version;
        }
    }
}
