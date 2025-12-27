using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using CsvHelper;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace HomeLab.Cli.Services.Output;

/// <summary>
/// Implementation of IOutputFormatter for exporting data in multiple formats.
/// Supports JSON, CSV, YAML, and Table (human-readable) formats.
/// </summary>
public class OutputFormatter : IOutputFormatter
{
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly ISerializer _yamlSerializer;

    public OutputFormatter()
    {
        // Configure JSON serializer
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter() }
        };

        // Configure YAML serializer
        _yamlSerializer = new SerializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();
    }

    public string Format<T>(T data, OutputFormat format)
    {
        return format switch
        {
            OutputFormat.Json => FormatJson(data),
            OutputFormat.Yaml => FormatYaml(data),
            OutputFormat.Csv => FormatCsvSingle(data),
            OutputFormat.Table => FormatTable(data),
            _ => throw new ArgumentException($"Unsupported format: {format}")
        };
    }

    public string FormatCollection<T>(IEnumerable<T> data, OutputFormat format)
    {
        return format switch
        {
            OutputFormat.Json => FormatJson(data),
            OutputFormat.Yaml => FormatYaml(data),
            OutputFormat.Csv => FormatCsv(data),
            OutputFormat.Table => FormatTable(data),
            _ => throw new ArgumentException($"Unsupported format: {format}")
        };
    }

    public async Task SaveToFileAsync<T>(T data, OutputFormat format, string filePath)
    {
        var formatted = data is System.Collections.IEnumerable enumerable && !(data is string)
            ? FormatCollection(enumerable.Cast<object>(), format)
            : Format(data, format);

        await File.WriteAllTextAsync(filePath, formatted);
    }

    private string FormatJson<T>(T data)
    {
        return JsonSerializer.Serialize(data, _jsonOptions);
    }

    private string FormatYaml<T>(T data)
    {
        return _yamlSerializer.Serialize(data);
    }

    private string FormatCsv<T>(IEnumerable<T> data)
    {
        using var writer = new StringWriter();
        using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);

        csv.WriteRecords(data);
        return writer.ToString();
    }

    private string FormatCsvSingle<T>(T data)
    {
        // For single object, create a collection with one item
        return FormatCsv(new[] { data });
    }

    private string FormatTable<T>(T data)
    {
        // Table format is handled by Spectre.Console in the commands
        // This is just a fallback that returns JSON for programmatic use
        return FormatJson(data);
    }
}
