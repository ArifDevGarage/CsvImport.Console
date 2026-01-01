using CsvHelper.Configuration;

namespace CsvImport.ConsoleApp;

public sealed class CustomerCsvMap : ClassMap<CustomerCsvRow>
{
    public CustomerCsvMap()
    {
        Map(m => m.Code).Name("Code");
        Map(m => m.Name).Name("Name");
        Map(m => m.Email).Name("Email");
    }
}
