namespace CsvImport.ConsoleApp;

public sealed class ImportSettings
{
    public string Provider { get; set; } = "SqlServer";      // SqlServer | Postgres | MySql
    public string ConnectionString { get; set; } = "";
    public int BatchSize { get; set; } = 500;
}
