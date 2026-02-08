namespace HomeLab.Cli.Models.AI;

/// <summary>
/// Response from an LLM API call.
/// </summary>
public class LlmResponse
{
    public string Content { get; set; } = string.Empty;
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public string Model { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? Error { get; set; }
}
