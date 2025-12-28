using FluentAssertions;
using HomeLab.Cli.Services.Output;
using Xunit;

namespace HomeLab.Cli.Tests.Services;

/// <summary>
/// Unit tests for OutputFormatter service.
/// Tests JSON, CSV, YAML formatting with single objects and collections.
/// </summary>
public class OutputFormatterTests
{
    private readonly OutputFormatter _formatter;

    public OutputFormatterTests()
    {
        _formatter = new OutputFormatter();
    }

    #region Test Data

    private class TestData
    {
        public string Name { get; set; } = string.Empty;
        public int Value { get; set; }
        public bool IsActive { get; set; }
    }

    private static List<TestData> GetTestCollection() => new()
    {
        new TestData { Name = "First", Value = 100, IsActive = true },
        new TestData { Name = "Second", Value = 200, IsActive = false },
        new TestData { Name = "Third", Value = 300, IsActive = true }
    };

    #endregion

    #region JSON Tests

    [Fact]
    public void Format_Json_SingleObject_ShouldReturnValidJson()
    {
        // Arrange
        var data = new TestData { Name = "Test", Value = 42, IsActive = true };

        // Act
        var result = _formatter.Format(data, OutputFormat.Json);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("\"name\": \"Test\"");
        result.Should().Contain("\"value\": 42");
        result.Should().Contain("\"isActive\": true");
    }

    [Fact]
    public void FormatCollection_Json_MultipleObjects_ShouldReturnValidJsonArray()
    {
        // Arrange
        var data = GetTestCollection();

        // Act
        var result = _formatter.FormatCollection(data, OutputFormat.Json);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("\"name\": \"First\"");
        result.Should().Contain("\"name\": \"Second\"");
        result.Should().Contain("\"name\": \"Third\"");
        result.Should().StartWith("[");
        result.Should().EndWith("]");
    }

    #endregion

    #region CSV Tests

    [Fact]
    public void Format_Csv_SingleObject_ShouldReturnCsvWithHeaders()
    {
        // Arrange
        var data = new TestData { Name = "Test", Value = 42, IsActive = true };

        // Act
        var result = _formatter.Format(data, OutputFormat.Csv);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("Name,Value,IsActive");
        result.Should().Contain("Test,42,True");
    }

    [Fact]
    public void FormatCollection_Csv_MultipleObjects_ShouldReturnCsvWithAllRows()
    {
        // Arrange
        var data = GetTestCollection();

        // Act
        var result = _formatter.FormatCollection(data, OutputFormat.Csv);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("Name,Value,IsActive");
        result.Should().Contain("First,100,True");
        result.Should().Contain("Second,200,False");
        result.Should().Contain("Third,300,True");
    }

    [Fact]
    public void FormatCollection_Csv_EmptyCollection_ShouldReturnHeadersOnly()
    {
        // Arrange
        var data = new List<TestData>();

        // Act
        var result = _formatter.FormatCollection(data, OutputFormat.Csv);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("Name,Value,IsActive");
    }

    #endregion

    #region YAML Tests

    [Fact]
    public void Format_Yaml_SingleObject_ShouldReturnValidYaml()
    {
        // Arrange
        var data = new TestData { Name = "Test", Value = 42, IsActive = true };

        // Act
        var result = _formatter.Format(data, OutputFormat.Yaml);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("name: Test");
        result.Should().Contain("value: 42");
        result.Should().Contain("isActive: true");
    }

    [Fact]
    public void FormatCollection_Yaml_MultipleObjects_ShouldReturnValidYamlArray()
    {
        // Arrange
        var data = GetTestCollection();

        // Act
        var result = _formatter.FormatCollection(data, OutputFormat.Yaml);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("- name: First");
        result.Should().Contain("- name: Second");
        result.Should().Contain("- name: Third");
    }

    #endregion

    #region Table Tests

    [Fact]
    public void Format_Table_ShouldFallbackToJson()
    {
        // Arrange
        var data = new TestData { Name = "Test", Value = 42, IsActive = true };

        // Act
        var result = _formatter.Format(data, OutputFormat.Table);

        // Assert - Table format falls back to JSON
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("\"name\": \"Test\"");
    }

    #endregion

    #region File Export Tests

    [Fact]
    public async Task SaveToFileAsync_Json_ShouldCreateFile()
    {
        // Arrange
        var data = new TestData { Name = "Test", Value = 42, IsActive = true };
        var tempFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.json");

        try
        {
            // Act
            await _formatter.SaveToFileAsync(data, OutputFormat.Json, tempFile);

            // Assert
            File.Exists(tempFile).Should().BeTrue();
            var content = await File.ReadAllTextAsync(tempFile);
            content.Should().Contain("\"name\": \"Test\"");
        }
        finally
        {
            // Cleanup
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public async Task SaveToFileAsync_Csv_Collection_ShouldCreateFile()
    {
        // Arrange
        var data = GetTestCollection();
        var tempFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.csv");

        try
        {
            // Act
            await _formatter.SaveToFileAsync(data, OutputFormat.Csv, tempFile);

            // Assert
            File.Exists(tempFile).Should().BeTrue();
            var content = await File.ReadAllTextAsync(tempFile);
            content.Should().Contain("Name,Value,IsActive");
            content.Should().Contain("First,100,True");
        }
        finally
        {
            // Cleanup
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public void Format_UnsupportedFormat_ShouldThrowArgumentException()
    {
        // Arrange
        var data = new TestData { Name = "Test", Value = 42, IsActive = true };
        var invalidFormat = (OutputFormat)999;

        // Act & Assert
        var act = () => _formatter.Format(data, invalidFormat);
        act.Should().Throw<ArgumentException>()
            .WithMessage("Unsupported format: 999");
    }

    [Fact]
    public void FormatCollection_UnsupportedFormat_ShouldThrowArgumentException()
    {
        // Arrange
        var data = GetTestCollection();
        var invalidFormat = (OutputFormat)999;

        // Act & Assert
        var act = () => _formatter.FormatCollection(data, invalidFormat);
        act.Should().Throw<ArgumentException>()
            .WithMessage("Unsupported format: 999");
    }

    #endregion
}
