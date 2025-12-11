using Log4YM.Contracts.Models;

namespace Log4YM.Server.Services;

public interface ISpotService
{
    Task<IEnumerable<Spot>> GetRecentAsync(int limit = 100);
    Task<IEnumerable<Spot>> GetByBandAsync(double minFreq, double maxFreq, int limit = 50);
    Task<Spot> CreateAsync(Spot spot);
    Task<int> GetCountAsync();
}
