using CsvHelper.Configuration;

namespace CsvImport.ConsoleApp;

public sealed class AspNetUserLoginCsvRow
{
    public string? LoginProvider { get; set; }
    public string? ProviderKey { get; set; }
    public string? ProviderDisplayName { get; set; }
    public string? UserId { get; set; }
}

public sealed class AspNetUserLoginCsvMap : ClassMap<AspNetUserLoginCsvRow>
{
    public AspNetUserLoginCsvMap()
    {
        Map(m => m.LoginProvider).Name("LoginProvider");
        Map(m => m.ProviderKey).Name("ProviderKey");
        Map(m => m.ProviderDisplayName).Name("ProviderDisplayName");
        Map(m => m.UserId).Name("UserId");
    }
}
