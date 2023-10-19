using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace NugetDependencyChecker.BusinessLogic.Models
{
    public class Package
    {
        public string Name { get; set; }
        public string Version { get; set; }
        public IList<Package> Dependencies { get; private set; }
        public ObservableCollection<ChildPackage> PackagesThatUseThisPackage { get; private set; }
        public int NumberOfPackagesThatUseThisPackage { get; private set; }
        public string Guid { get; set; }
        public string RootPackageName { get; set; }

        public Package(string name, string version) : this(name, version, new List<Package>())
        {

        }

        private void UpdateNumberOfPackagesThatUseThisPackage(object? sender, NotifyCollectionChangedEventArgs e)
        {
            NumberOfPackagesThatUseThisPackage = PackagesThatUseThisPackage.Count();
        }

        public Package(string name, string version, IList<Package> dependencies)
        {
            Name = name;
            Version = version;
            Dependencies = dependencies;
            PackagesThatUseThisPackage = new ObservableCollection<ChildPackage>();
            PackagesThatUseThisPackage.CollectionChanged += UpdateNumberOfPackagesThatUseThisPackage;
        }
    }
}
