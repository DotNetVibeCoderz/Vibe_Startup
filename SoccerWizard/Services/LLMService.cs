#nullable disable
using SoccerWizard.Services.LLM;

namespace SoccerWizard.Services;

/// <summary>
/// Facade LLM Service — mendelagasikan ke SemanticKernelService.
/// Tetap kompatibel dengan semua existing code (Chat, Predict, Sentiment).
/// </summary>
public class LLMService
{
    private readonly SemanticKernelService _sk;

    public LLMService(SemanticKernelService sk)
    {
        _sk = sk;
    }

    public async Task<string> ChatAsync(string userMessage, string context = "")
        => await _sk.ChatAsync(userMessage, context);

    public async Task<string> ChatWithImagesAsync(string userMessage, List<string> imageUrls, string systemPrompt = "")
        => await _sk.ChatWithImagesAsync(userMessage, imageUrls, systemPrompt);

    public async Task<(double score, string label, string summary)> AnalyzeSentimentAsync(string text)
        => await _sk.AnalyzeSentimentAsync(text);

    public async Task<string> GenerateTextPredictionAsync(Models.Match match, Models.Team home, Models.Team away)
        => await _sk.GenerateTextPredictionAsync(match, home, away);
}
