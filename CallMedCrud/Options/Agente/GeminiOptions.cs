namespace MKSANCrud.Options;

public sealed class GeminiOptions
{
    public const string SectionName = "Gemini";

    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "gemini-3.1-flash-lite";
    public int TimeoutSeconds { get; set; } = 60;
    public int MaxToolRounds { get; set; } = 8;
    public int MaxHistoryMessages { get; set; } = 30;
}
