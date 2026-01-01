using Microsoft.Extensions.Logging;

namespace CsvImport.ConsoleApp;

public sealed class ImportDispatcher
{
    private readonly IEnumerable<IImportHandler> _handlers;
    private readonly ILogger<ImportDispatcher> _logger;

    public ImportDispatcher(IEnumerable<IImportHandler> handlers, ILogger<ImportDispatcher> logger)
    {
        _handlers = handlers;
        _logger = logger;
    }

    public async Task RunAsync(string entityName, string filePath, CancellationToken ct)
    {
        var handler = _handlers.FirstOrDefault(h =>
            h.EntityName.Equals(entityName, StringComparison.OrdinalIgnoreCase));

        if (handler == null)
        {
            var known = string.Join(", ", _handlers.Select(h => h.EntityName));
            throw new InvalidOperationException($"Unknown entity '{entityName}'. Known: {known}");
        }

        _logger.LogInformation("Running importer for entity '{Entity}' using file '{File}'", entityName, filePath);
        await handler.RunAsync(filePath, ct);
    }
}
