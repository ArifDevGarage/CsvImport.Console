
using CsvHelper.Configuration;

namespace CsvImport.ConsoleApp;

public sealed class AspNetUserClaimCsvRow
{
    public string? UserId { get; set; }
    public string? ClaimType { get; set; }
    public string? ClaimValue { get; set; }
}

public sealed class AspNetUserClaimCsvMap : ClassMap<AspNetUserClaimCsvRow>
{
    public AspNetUserClaimCsvMap()
    {
        Map(m => m.UserId).Name("UserId");
        Map(m => m.ClaimType).Name("ClaimType");
        Map(m => m.ClaimValue).Name("ClaimValue");
    }
}
