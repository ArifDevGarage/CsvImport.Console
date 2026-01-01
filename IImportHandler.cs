namespace CsvImport.ConsoleApp;

public interface IImportHandler
{
    string EntityName { get; }
    Task RunAsync(string filePath, CancellationToken ct);
}
