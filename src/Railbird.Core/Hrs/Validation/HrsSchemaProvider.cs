using Json.Schema;

namespace Railbird.Core.Hrs.Validation;

public static class HrsSchemaProvider
{
    private static readonly Lazy<JsonSchema> Schema = new(LoadSchema);

    public static JsonSchema GetSchema() => Schema.Value;

    private static JsonSchema LoadSchema()
    {
        var path = SchemaLocator.FindSchemaPath();
        var json = File.ReadAllText(path);
        return JsonSchema.FromText(json);
    }
}
