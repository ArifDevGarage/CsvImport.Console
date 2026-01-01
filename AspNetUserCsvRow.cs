namespace CsvImport.ConsoleApp;

public sealed class AspNetUserCsvRow
{
    public string? Id { get; set; }
    public string? UserName { get; set; }
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public bool? EmailConfirmed { get; set; }

    // Comma/semicolon separated role names: "Admin;Finance"
    public string? Roles { get; set; }

    // Optional plain password from CSV; will be hashed into PasswordHash
    public string? Password { get; set; }
}
