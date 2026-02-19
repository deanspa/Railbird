using Railbird.Core.Hrs.Validation;
using Xunit;

namespace Railbird.Core.Tests;

public sealed class SchemaValidationTests
{
    [Theory]
    [InlineData("HAND_RECORDING_EXAMPLE_1.json")]
    [InlineData("HAND_RECORDING_EXAMPLE_2.json")]
    [InlineData("HAND_RECORDING_EXAMPLE_3.json")]
    public void ExampleHandsValidate(string fileName)
    {
        var json = File.ReadAllText(Path.Combine(FindRepoRoot(), "examples", "hands", "v1", fileName));
        var result = HrsValidator.Validate(json);
        Assert.True(result.IsSuccess, string.Join("\n", result.Errors));
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
