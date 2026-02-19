using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Json.Schema;
using Railbird.Core.Common;
using Railbird.Core.Hrs.Models;

namespace Railbird.Core.Hrs.Validation;

public static class HrsValidator
{
    public static Result<Hand> Validate(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Result<Hand>.Failure(new[] { "Input JSON was empty." });
        }

        JsonNode? node;
        try
        {
            node = JsonNode.Parse(json);
        }
        catch (JsonException ex)
        {
            return Result<Hand>.Failure(new[] { $"Invalid JSON: {ex.Message}" });
        }

        if (node is null)
        {
            return Result<Hand>.Failure(new[] { "Input JSON was null." });
        }

        var schema = HrsSchemaProvider.GetSchema();
        var options = new EvaluationOptions
        {
            OutputFormat = OutputFormat.List,
            RequireFormatValidation = true
        };

        var results = schema.Evaluate(node, options);
        if (!results.IsValid)
        {
            var errors = new List<string>();
            if (results.Details != null)
            {
                foreach (var detail in results.Details.Where(d => !d.IsValid))
                {
                    var loc = detail.InstanceLocation?.ToString() ?? "(root)";
                    if (detail.HasErrors && detail.Errors != null)
                    {
                        foreach (var err in detail.Errors)
                        {
                            errors.Add($"{loc}: {err.Key} - {err.Value}");
                        }
                    }
                    else
                    {
                        errors.Add($"{loc}: Schema validation error");
                    }
                }
            }

            if (errors.Count == 0)
            {
                errors.Add("Schema validation failed.");
            }

            return Result<Hand>.Failure(errors);
        }

        try
        {
            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = false
            };
            jsonOptions.Converters.Add(new JsonStringEnumConverter());

            var hand = JsonSerializer.Deserialize<Hand>(json, jsonOptions);
            if (hand == null)
            {
                return Result<Hand>.Failure(new[] { "Failed to deserialize hand JSON." });
            }

            return Result<Hand>.Success(hand);
        }
        catch (JsonException ex)
        {
            return Result<Hand>.Failure(new[] { $"Failed to deserialize hand JSON: {ex.Message}" });
        }
    }
}
