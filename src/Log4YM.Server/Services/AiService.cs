using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Log4YM.Contracts.Api;
using Log4YM.Contracts.Models;
using Log4YM.Server.Core.Database;

namespace Log4YM.Server.Services;

public class AiService : IAiService
{
    private readonly ISettingsRepository _settingsRepository;
    private readonly IQsoService _qsoService;
    private readonly IQrzService _qrzService;
    private readonly HttpClient _httpClient;
    private readonly ILogger<AiService> _logger;

    public AiService(
        ISettingsRepository settingsRepository,
        IQsoService qsoService,
        IQrzService qrzService,
        IHttpClientFactory httpClientFactory,
        ILogger<AiService> logger)
    {
        _settingsRepository = settingsRepository;
        _qsoService = qsoService;
        _qrzService = qrzService;
        _httpClient = httpClientFactory.CreateClient("AI");
        _logger = logger;
    }

    public async Task<GenerateTalkPointsResponse> GenerateTalkPointsAsync(GenerateTalkPointsRequest request)
    {
        var settings = await _settingsRepository.GetAsync() ?? new UserSettings();
        var aiSettings = settings.Ai;

        if (string.IsNullOrEmpty(aiSettings.ApiKey))
        {
            throw new InvalidOperationException("AI API key not configured");
        }

        // Gather context data
        var previousQsos = await GetPreviousQsosAsync(request.Callsign);
        var qrzProfile = aiSettings.IncludeQrzProfile
            ? await GetQrzProfileAsync(request.Callsign)
            : null;

        // Build prompt
        var context = BuildTalkPointsContext(
            settings.Station.Callsign,
            request.Callsign,
            previousQsos,
            qrzProfile,
            request.CurrentBand,
            request.CurrentMode
        );

        // Call LLM
        var response = await CallLlmAsync(aiSettings, context);

        // Parse response
        var talkPoints = ParseTalkPoints(response);

        return new GenerateTalkPointsResponse(
            request.Callsign,
            previousQsos,
            qrzProfile,
            talkPoints,
            response
        );
    }

    public async Task<ChatResponse> ChatAsync(ChatRequest request)
    {
        var settings = await _settingsRepository.GetAsync() ?? new UserSettings();
        var aiSettings = settings.Ai;

        if (string.IsNullOrEmpty(aiSettings.ApiKey))
        {
            throw new InvalidOperationException("AI API key not configured");
        }

        // Gather context for this callsign
        var previousQsos = await GetPreviousQsosAsync(request.Callsign);
        var qrzProfile = aiSettings.IncludeQrzProfile
            ? await GetQrzProfileAsync(request.Callsign)
            : null;

        // Build context with conversation history
        var messages = BuildChatMessages(
            settings.Station.Callsign,
            request.Callsign,
            previousQsos,
            qrzProfile,
            request.Question,
            request.ConversationHistory
        );

        // Call LLM with conversation
        var response = await CallLlmWithMessagesAsync(aiSettings, messages);

        return new ChatResponse(response);
    }

    public async Task<TestApiKeyResponse> TestApiKeyAsync(TestApiKeyRequest request)
    {
        try
        {
            var testPrompt = "Reply with 'OK' if you can read this.";
            var response = await CallLlmAsync(
                new AiSettings
                {
                    Provider = request.Provider,
                    ApiKey = request.ApiKey,
                    Model = request.Model
                },
                testPrompt
            );

            return new TestApiKeyResponse(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "API key test failed");
            return new TestApiKeyResponse(false, ex.Message);
        }
    }

    private async Task<List<PreviousQsoSummary>> GetPreviousQsosAsync(string callsign)
    {
        var searchRequest = new QsoSearchRequest(
            Callsign: callsign,
            Name: null,
            Band: null,
            Mode: null,
            FromDate: null,
            ToDate: null,
            Limit: 10,
            Skip: 0
        );

        var result = await _qsoService.GetQsosAsync(searchRequest);

        return result.Items
            .OrderByDescending(q => q.QsoDate)
            .Select(q => new PreviousQsoSummary(
                q.QsoDate,
                q.Band,
                q.Mode,
                q.RstSent,
                q.RstRcvd,
                q.Comment
            ))
            .ToList();
    }

    private async Task<QrzProfileSummary?> GetQrzProfileAsync(string callsign)
    {
        try
        {
            var profile = await _qrzService.LookupCallsignAsync(callsign);
            if (profile == null) return null;

            // Build a simple bio from available fields
            var bio = profile.Email != null ? $"Email: {profile.Email}" : null;

            return new QrzProfileSummary(
                profile.Name,
                $"{profile.City}, {profile.State}, {profile.Country}".Trim(' ', ','),
                profile.Grid,
                bio,
                null // QRZ XML doesn't have an "interests" field directly
            );
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get QRZ profile for {Callsign}", callsign);
            return null;
        }
    }

    private string BuildTalkPointsContext(
        string myCallsign,
        string theirCallsign,
        List<PreviousQsoSummary> previousQsos,
        QrzProfileSummary? qrzProfile,
        string? currentBand,
        string? currentMode)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are a ham radio operating assistant. Generate concise, practical talk points for a QSO.");
        sb.AppendLine();
        sb.AppendLine($"My callsign: {myCallsign}");
        sb.AppendLine($"Their callsign: {theirCallsign}");

        if (!string.IsNullOrEmpty(currentBand))
            sb.AppendLine($"Current band: {currentBand}");
        if (!string.IsNullOrEmpty(currentMode))
            sb.AppendLine($"Current mode: {currentMode}");

        if (qrzProfile != null)
        {
            sb.AppendLine();
            sb.AppendLine("Their QRZ Profile:");
            if (!string.IsNullOrEmpty(qrzProfile.Name))
                sb.AppendLine($"  Name: {qrzProfile.Name}");
            if (!string.IsNullOrEmpty(qrzProfile.Location))
                sb.AppendLine($"  Location: {qrzProfile.Location}");
            if (!string.IsNullOrEmpty(qrzProfile.Grid))
                sb.AppendLine($"  Grid: {qrzProfile.Grid}");
        }

        if (previousQsos.Any())
        {
            sb.AppendLine();
            sb.AppendLine($"Previous QSOs with {theirCallsign} ({previousQsos.Count}):");
            foreach (var qso in previousQsos.Take(5))
            {
                sb.Append($"  - {qso.QsoDate} on {qso.Band} {qso.Mode}");
                if (!string.IsNullOrEmpty(qso.Comment))
                    sb.Append($" — \"{qso.Comment}\"");
                sb.AppendLine();
            }
        }
        else
        {
            sb.AppendLine();
            sb.AppendLine($"No previous QSOs with {theirCallsign}.");
        }

        sb.AppendLine();
        sb.AppendLine("Generate 3-5 natural talk points. Each point should be a complete sentence suggesting what to mention during the contact. Focus on personal connections, previous conversations, and shared interests.");

        return sb.ToString();
    }

    private List<object> BuildChatMessages(
        string myCallsign,
        string theirCallsign,
        List<PreviousQsoSummary> previousQsos,
        QrzProfileSummary? qrzProfile,
        string question,
        List<ChatMessage>? conversationHistory)
    {
        var messages = new List<object>();

        // System message with context
        var systemContent = new StringBuilder();
        systemContent.AppendLine($"You are a ham radio operating assistant helping {myCallsign} with information about {theirCallsign}.");

        if (qrzProfile != null)
        {
            systemContent.AppendLine($"\nTheir profile: {qrzProfile.Name}, {qrzProfile.Location}, Grid: {qrzProfile.Grid}");
        }

        if (previousQsos.Any())
        {
            systemContent.AppendLine($"\nPrevious QSOs: {previousQsos.Count} contacts");
            foreach (var qso in previousQsos.Take(3))
            {
                systemContent.Append($"- {qso.QsoDate} {qso.Band} {qso.Mode}");
                if (!string.IsNullOrEmpty(qso.Comment))
                    systemContent.Append($" ({qso.Comment})");
                systemContent.AppendLine();
            }
        }

        messages.Add(new { role = "system", content = systemContent.ToString() });

        // Add conversation history
        if (conversationHistory != null)
        {
            foreach (var msg in conversationHistory)
            {
                messages.Add(new { role = msg.Role, content = msg.Content });
            }
        }

        // Add current question
        messages.Add(new { role = "user", content = question });

        return messages;
    }

    private async Task<string> CallLlmAsync(AiSettings settings, string prompt)
    {
        var messages = new List<object>
        {
            new { role = "user", content = prompt }
        };

        return await CallLlmWithMessagesAsync(settings, messages);
    }

    private async Task<string> CallLlmWithMessagesAsync(AiSettings settings, List<object> messages)
    {
        if (settings.Provider.ToLower() == "anthropic")
        {
            return await CallAnthropicAsync(settings, messages);
        }
        else if (settings.Provider.ToLower() == "openai")
        {
            return await CallOpenAiAsync(settings, messages);
        }
        else
        {
            throw new InvalidOperationException($"Unsupported AI provider: {settings.Provider}");
        }
    }

    private async Task<string> CallAnthropicAsync(AiSettings settings, List<object> messages)
    {
        var request = new
        {
            model = settings.Model,
            max_tokens = 1024,
            messages = messages
        };

        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages");
        httpRequest.Headers.Add("x-api-key", settings.ApiKey);
        httpRequest.Headers.Add("anthropic-version", "2023-06-01");
        httpRequest.Content = content;

        var response = await _httpClient.SendAsync(httpRequest);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync();
        var responseObj = JsonSerializer.Deserialize<AnthropicResponse>(responseJson);

        return responseObj?.Content?.FirstOrDefault()?.Text ?? "No response from AI";
    }

    private async Task<string> CallOpenAiAsync(AiSettings settings, List<object> messages)
    {
        var request = new
        {
            model = settings.Model,
            messages = messages,
            max_tokens = 1024
        };

        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);
        httpRequest.Content = content;

        var response = await _httpClient.SendAsync(httpRequest);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync();
        var responseObj = JsonSerializer.Deserialize<OpenAiResponse>(responseJson);

        return responseObj?.Choices?.FirstOrDefault()?.Message?.Content ?? "No response from AI";
    }

    private List<string> ParseTalkPoints(string response)
    {
        // Simple parsing: split by lines and look for bullet points or numbered items
        var lines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var talkPoints = new List<string>();

        foreach (var line in lines)
        {
            // Look for lines that start with bullets, numbers, or just contain substantial text
            if (line.StartsWith("-") || line.StartsWith("•") ||
                line.StartsWith("*") || char.IsDigit(line[0]))
            {
                var point = line.TrimStart('-', '•', '*', ' ', '1', '2', '3', '4', '5', '6', '7', '8', '9', '0', '.', ')');
                if (!string.IsNullOrWhiteSpace(point))
                {
                    talkPoints.Add(point.Trim());
                }
            }
        }

        // If no bullet points found, return the whole response as one point
        if (talkPoints.Count == 0 && !string.IsNullOrWhiteSpace(response))
        {
            talkPoints.Add(response);
        }

        return talkPoints;
    }

    // Response models for deserialization
    private class AnthropicResponse
    {
        [JsonPropertyName("content")]
        public List<AnthropicContent>? Content { get; set; }
    }

    private class AnthropicContent
    {
        [JsonPropertyName("text")]
        public string? Text { get; set; }
    }

    private class OpenAiResponse
    {
        [JsonPropertyName("choices")]
        public List<OpenAiChoice>? Choices { get; set; }
    }

    private class OpenAiChoice
    {
        [JsonPropertyName("message")]
        public OpenAiMessage? Message { get; set; }
    }

    private class OpenAiMessage
    {
        [JsonPropertyName("content")]
        public string? Content { get; set; }
    }
}
