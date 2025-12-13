namespace Log4YM.Server.Services;

/// <summary>
/// Helper class for frequency to amateur band mapping
/// </summary>
public static class BandHelper
{
    private static readonly (long LowerHz, long UpperHz, string Band)[] BandRanges =
    {
        (1_800_000, 2_000_000, "160m"),
        (3_500_000, 4_000_000, "80m"),
        (5_330_500, 5_405_000, "60m"),
        (7_000_000, 7_300_000, "40m"),
        (10_100_000, 10_150_000, "30m"),
        (14_000_000, 14_350_000, "20m"),
        (18_068_000, 18_168_000, "17m"),
        (21_000_000, 21_450_000, "15m"),
        (24_890_000, 24_990_000, "12m"),
        (28_000_000, 29_700_000, "10m"),
        (50_000_000, 54_000_000, "6m"),
        (144_000_000, 148_000_000, "2m"),
        (420_000_000, 450_000_000, "70cm"),
    };

    /// <summary>
    /// Get the amateur band for a given frequency in Hz
    /// </summary>
    public static string GetBand(long frequencyHz)
    {
        foreach (var (lower, upper, band) in BandRanges)
        {
            if (frequencyHz >= lower && frequencyHz <= upper)
            {
                return band;
            }
        }
        return "Unknown";
    }

    /// <summary>
    /// Get the amateur band for a given frequency in MHz
    /// </summary>
    public static string GetBandFromMhz(double frequencyMhz)
    {
        return GetBand((long)(frequencyMhz * 1_000_000));
    }

    /// <summary>
    /// Check if a frequency is within amateur bands
    /// </summary>
    public static bool IsAmateurBand(long frequencyHz)
    {
        return GetBand(frequencyHz) != "Unknown";
    }
}
