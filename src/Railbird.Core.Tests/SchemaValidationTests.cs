using Railbird.Core.Hrs.Validation;
using Xunit;

namespace Railbird.Core.Tests;

public sealed class SchemaValidationTests
{
    [Fact]
    public void ExampleHandsValidate()
    {
        var examplesDir = Path.Combine(FindRepoRoot(), "examples", "hands", "v1");
        var files = Directory.GetFiles(examplesDir, "*.json", SearchOption.TopDirectoryOnly);
        Assert.NotEmpty(files);

        foreach (var file in files)
        {
            var json = File.ReadAllText(file);
            var result = HrsValidator.Validate(json);
            Assert.True(result.IsSuccess, $"{Path.GetFileName(file)}: {string.Join("\n", result.Errors)}");
        }
    }

    [Fact]
    public void InvalidJsonFails()
    {
        var result = HrsValidator.Validate("{}");
        Assert.False(result.IsSuccess);
        Assert.NotEmpty(result.Errors);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "examples", "hands", "v1");
            if (Directory.Exists(candidate))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repo root containing examples/hands/v1.");
    }
}
