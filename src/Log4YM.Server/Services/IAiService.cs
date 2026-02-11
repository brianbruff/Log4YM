using Log4YM.Contracts.Api;

namespace Log4YM.Server.Services;

public interface IAiService
{
    Task<GenerateTalkPointsResponse> GenerateTalkPointsAsync(GenerateTalkPointsRequest request);
    Task<ChatResponse> ChatAsync(ChatRequest request);
    Task<TestApiKeyResponse> TestApiKeyAsync(TestApiKeyRequest request);
}
