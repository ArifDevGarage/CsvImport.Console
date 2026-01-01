using CsvHelper.Configuration;

namespace CsvImport.ConsoleApp;

public sealed class AspNetUserTokenCsvRow
{
    public string? UserId { get; set; }
    public string? LoginProvider { get; set; }
    public string? Name { get; set; }
    public string? Value { get; set; }
}

public sealed class AspNetUserTokenCsvMap : ClassMap<AspNetUserTokenCsvRow>
{
    public AspNetUserTokenCsvMap()
    {
        Map(m => m.UserId).Name("UserId");
        Map(m => m.LoginProvider).Name("LoginProvider");
        Map(m => m.Name).Name("Name");
        Map(m => m.Value).Name("Value");
    }
}
