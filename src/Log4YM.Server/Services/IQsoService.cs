using Log4YM.Contracts.Api;
using Log4YM.Contracts.Models;

namespace Log4YM.Server.Services;

public interface IQsoService
{
    Task<QsoResponse?> GetByIdAsync(string id);
    Task<PaginatedQsoResponse> GetQsosAsync(QsoSearchRequest request);
    Task<QsoResponse> CreateAsync(CreateQsoRequest request);
    Task<QsoResponse?> UpdateAsync(string id, UpdateQsoRequest request);
    Task<bool> DeleteAsync(string id);
    Task<QsoStatistics> GetStatisticsAsync();
}
