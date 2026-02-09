namespace Log4YM.Contracts.Api;

/// <summary>
/// Request to generate talk points for a callsign
/// </summary>
public record GenerateTalkPointsRequest(
    string Callsign,
    string? CurrentBand = null,
    string? CurrentMode = null
);

/// <summary>
/// Response containing generated talk points
/// </summary>
public record GenerateTalkPointsResponse(
    string Callsign,
    List<PreviousQsoSummary> PreviousQsos,
    QrzProfileSummary? QrzProfile,
    List<string> TalkPoints,
    string GeneratedText
);

/// <summary>
/// Summary of a previous QSO
/// </summary>
public record PreviousQsoSummary(
    string QsoDate,
    string Band,
    string Mode,
    string? RstSent,
    string? RstRcvd,
    string? Comment
);

/// <summary>
/// Summary of QRZ profile data
/// </summary>
public record QrzProfileSummary(
    string? Name,
    string? Location,
    string? Grid,
    string? Bio,
    string? Interests
);

/// <summary>
/// Request for a chat question about a callsign
/// </summary>
public record ChatRequest(
    string Callsign,
    string Question,
    List<ChatMessage>? ConversationHistory = null
);

/// <summary>
/// Response for a chat question
/// </summary>
public record ChatResponse(
    string Answer
);

/// <summary>
/// A message in the chat conversation
/// </summary>
public record ChatMessage(
    string Role,  // "user" or "assistant"
    string Content
);

/// <summary>
/// Request to test an API key
/// </summary>
public record TestApiKeyRequest(
    string Provider,  // "anthropic" or "openai"
    string ApiKey,
    string Model
);

/// <summary>
/// Response from testing an API key
/// </summary>
public record TestApiKeyResponse(
    bool IsValid,
    string? ErrorMessage = null
);
