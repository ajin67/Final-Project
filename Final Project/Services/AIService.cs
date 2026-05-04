using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Final_Project.Models;

namespace Final_Project.Services;

/// <summary>
/// Handles AI text enhancement using an LM Studio compatible endpoint.
/// </summary>
public sealed class AIService
{
    private readonly HttpClient _httpClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIService"/> class.
    /// </summary>
    /// <returns>A constructed <see cref="AIService"/> object.</returns>
    public AIService()
    {
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri("http://localhost:1234/v1/")
        };
    }

    /// <summary>
    /// Enhances a generated phrase with an AI-produced technical finishing sentence.
    /// </summary>
    /// <param name="basePhrase">The base roast or praise phrase.</param>
    /// <param name="projectName">The repository name.</param>
    /// <param name="contributionResult">The contributor metrics.</param>
    /// <returns>A witty final phrase.</returns>
    public async Task<string> EnhancePhraseAsync(string basePhrase, string projectName, ContributionResult contributionResult)
    {
        string prompt = $"""
                        You are a witty senior engineer.
                        Take this phrase and add exactly one technical finishing sentence.

                        Phrase: {basePhrase}
                        Project: {projectName}
                        Contributor: {contributionResult.ContributorName}
                        Additions: {contributionResult.Additions}
                        Deletions: {contributionResult.Deletions}
                        ImportanceScore: {contributionResult.ImportanceScore:0.00}
                        ContributionPercent: {contributionResult.ContributionPercent:0.00}%
                        """;

        ChatRequest request = new(
            "local-model",
            [new ChatMessage("user", prompt)],
            0.8m,
            220);

        try
        {
            using HttpResponseMessage response = await _httpClient.PostAsJsonAsync("chat/completions", request);
            response.EnsureSuccessStatusCode();
            ChatResponse? chatResponse = await response.Content.ReadFromJsonAsync<ChatResponse>();

            string? aiContent = chatResponse?.Choices.FirstOrDefault()?.Message.Content?.Trim();
            return string.IsNullOrWhiteSpace(aiContent) ? basePhrase : aiContent;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return $"{basePhrase} (AI add-on skipped: {ex.Message})";
        }
    }

    private sealed record ChatRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("messages")] IReadOnlyList<ChatMessage> Messages,
        [property: JsonPropertyName("temperature")] decimal Temperature,
        [property: JsonPropertyName("max_tokens")] int MaxTokens);

    private sealed record ChatMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content);

    private sealed record ChatResponse([property: JsonPropertyName("choices")] IReadOnlyList<ChatChoice> Choices);

    private sealed record ChatChoice([property: JsonPropertyName("message")] ChatMessage Message);
}
