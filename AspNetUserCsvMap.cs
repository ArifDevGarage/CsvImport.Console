using CsvHelper.Configuration;

namespace CsvImport.ConsoleApp;

public sealed class AspNetUserCsvMap : ClassMap<AspNetUserCsvRow>
{
    public AspNetUserCsvMap()
    {
        Map(m => m.Id).Name("Id");
        Map(m => m.UserName).Name("UserName");
        Map(m => m.Email).Name("Email");
        Map(m => m.PhoneNumber).Name("PhoneNumber");

        Map(m => m.EmailConfirmed)
            .Name("EmailConfirmed")
            .TypeConverterOption.BooleanValues(true, true, "true", "1", "yes", "y")
            .TypeConverterOption.BooleanValues(false, true, "false", "0", "no", "n");

        Map(m => m.Roles).Name("Roles");
        Map(m => m.Password).Name("Password");
    }
}
