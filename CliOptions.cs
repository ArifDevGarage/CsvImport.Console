namespace CsvImport.ConsoleApp;

public sealed class CliOptions
{
    public string? Entity { get; private set; }
    public string? FilePath { get; private set; }
    public bool IsValid => !string.IsNullOrWhiteSpace(Entity) && !string.IsNullOrWhiteSpace(FilePath);

    public static CliOptions Parse(string[] args)
    {
        var opt = new CliOptions();

        for (int i = 0; i < args.Length; i++)
        {
            var a = args[i];

            if (string.Equals(a, "--entity", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                opt.Entity = args[++i];
                continue;
            }

            if (string.Equals(a, "--file", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                opt.FilePath = args[++i];
                continue;
            }
        }

        return opt;
    }
}
