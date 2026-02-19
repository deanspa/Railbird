namespace Railbird.Core.Hrs.Validation;

public static class SchemaLocator
{
    public static string FindSchemaPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "docs", "specs", "hrs", "v1", "HAND_RECORDING_STANDARD.schema.json");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException("Could not locate HRS schema file. Expected docs/specs/hrs/v1/HAND_RECORDING_STANDARD.schema.json in the repo tree.");
    }
}
