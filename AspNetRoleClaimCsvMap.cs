using CsvHelper.Configuration;

namespace CsvImport.ConsoleApp;

public sealed class AspNetRoleClaimCsvMap : ClassMap<AspNetRoleClaimCsvRow>
{
    public AspNetRoleClaimCsvMap()
    {
        Map(m => m.RoleId).Name("RoleId");
        Map(m => m.ClaimType).Name("ClaimType");
        Map(m => m.ClaimValue).Name("ClaimValue");
    }
}
