
using CsvHelper.Configuration;

namespace CsvImport.ConsoleApp;

public sealed class AspNetRoleCsvMap : ClassMap<AspNetRoleCsvRow>
{
    public AspNetRoleCsvMap()
    {
        Map(m => m.Name).Name("Name");
        Map(m => m.Id).Name("Id");
        Map(m => m.ConcurrencyStamp).Name("ConcurrencyStamp");
    }
}
