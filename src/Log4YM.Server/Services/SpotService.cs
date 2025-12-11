using Log4YM.Contracts.Models;
using Log4YM.Server.Core.Database;

namespace Log4YM.Server.Services;

public class SpotService : ISpotService
{
    private readonly ISpotRepository _repository;

    public SpotService(ISpotRepository repository)
    {
        _repository = repository;
    }

    public async Task<IEnumerable<Spot>> GetRecentAsync(int limit = 100)
    {
        return await _repository.GetRecentAsync(limit);
    }

    public async Task<IEnumerable<Spot>> GetByBandAsync(double minFreq, double maxFreq, int limit = 50)
    {
        return await _repository.GetByBandAsync(minFreq, maxFreq, limit);
    }

    public async Task<Spot> CreateAsync(Spot spot)
    {
        return await _repository.CreateAsync(spot);
    }

    public async Task<int> GetCountAsync()
    {
        return await _repository.GetCountAsync();
    }
}
