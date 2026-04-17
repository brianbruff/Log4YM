using Log4YM.Contracts.Api;

namespace Log4YM.Server.Services;

public interface ILotwService
{
    Task<LotwUploadResult> UploadAsync(LotwUploadFilter filter, CancellationToken cancellationToken);

    Task<LotwPreviewResponse> PreviewAsync(LotwUploadFilter filter);

    Task<LotwTestTqslResponse> TestTqslAsync(string path, CancellationToken cancellationToken);
}
