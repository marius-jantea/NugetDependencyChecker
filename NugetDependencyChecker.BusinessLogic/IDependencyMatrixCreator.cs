using NugetDependencyChecker.BusinessLogic.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NugetDependencyChecker.BusinessLogic
{
    public interface IDependencyMatrixCreator
    {
        Task CreateDependencyMatrix(IEnumerable<Package> packages);
    }
}
