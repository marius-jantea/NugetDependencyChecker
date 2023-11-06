using NugetDependencyChecker.BusinessLogic;
using NugetDependencyChecker.BusinessLogic.Models;
using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NugetDependencyChecker.Implementation
{
    public class ExcelDependencyMatrixCreator : IDependencyMatrixCreator
    {
        public Task CreateDependencyMatrix(IEnumerable<Package> packages)
        {
            try
            {
                var fileName = $"DependencyMatrix_{DateTime.Now.Ticks}.xlsx";

                FileInfo excel = new(fileName);

                using ExcelPackage package = new ExcelPackage(excel);

                ExcelWorksheet worksheetWork = package.Workbook.Worksheets.Add("Work");

                for (int i = 1; i <= packages.Count(); i++)
                {
                    worksheetWork.Cells[1, i + 1].Value = packages.ElementAt(i - 1).Name;
                }

                for (int i = 1; i <= packages.Count(); i++)
                {
                    worksheetWork.Cells[i + 1, 1].Value = packages.ElementAt(i - 1).Name;
                }

                for (int i = 1; i <= packages.Count(); i++)
                {
                    var packageOnRow = packages.ElementAt(i - 1);
                    foreach (var dependency in packageOnRow.Dependencies)
                    {
                        var dependencyInRelevantPackages = packages.FirstOrDefault(x => x.Name.Equals(dependency.Name));
                        if (dependencyInRelevantPackages != null)
                        {
                            var indexOfDependency = packages.ToList().IndexOf(dependencyInRelevantPackages);

                            worksheetWork.Cells[i + 1, indexOfDependency + 2].Value = "dependency";
                        }
                    }
                }

                package.Save();

                return Task.CompletedTask;
            }
            catch
            {
                return Task.FromException(new Exception("An error has occured while generating excel dependency matrix."));
            }
        }
    }
}
