using NugetDependencyChecker.BusinessLogic.Models;

namespace NugetDependencyChecker.BusinessLogic
{
    public interface IDependencyDiagramCreator
    {
        Task CreateDependencyDiagram(IEnumerable<Package> packages);
    }
}
