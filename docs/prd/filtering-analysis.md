# Current Filtering Approach Analysis

## Document Information
- **Date:** 2026-02-28
- **Author:** Claude Code
- **Purpose:** Validate the current "new DXCC" and "new band" filtering approach

---

## Executive Summary

The current filtering approach in Log4YM is **valid and well-designed**. The `SpotStatusService` correctly implements "new DXCC" and "new band" detection using in-memory HashSets with proper normalization. The implementation aligns with amateur radio conventions and can be reused/refactored for the statistics tab.

---

## Current Implementation Analysis

### 1. SpotStatusService Architecture

**Location:** `src/Log4YM.Server/Services/SpotStatusService.cs`

**Purpose:** Real-time detection of "new DXCC" and "new band" status for DX spots

**Data Structures:**
```csharp
private HashSet<string> _workedCountries = new(StringComparer.OrdinalIgnoreCase);
private HashSet<string> _workedCountryBands = new(StringComparer.OrdinalIgnoreCase);
private HashSet<string> _workedCountryBandModes = new(StringComparer.OrdinalIgnoreCase);
```

**Status Return Values:**
- `"newDxcc"` - Never worked this country/DXCC entity before
- `"newBand"` - Worked the country but not on this band
- `"worked"` - Already worked this exact country+band+mode combination
- `null` - Country+band worked but not with this specific mode

**Cache Keys:**
- Countries: Normalized country name (e.g., "United States")
- Country+Band: `"{normalizedCountry}:{band}"` (e.g., "United States:20m")
- Country+Band+Mode: `"{normalizedCountry}:{band}:{normalizedMode}"` (e.g., "United States:20m:SSB")

---

## Validation Assessment

### ✅ VALID: Country Name Normalization

**Implementation:**
```csharp
private static readonly Dictionary<string, string> CountryAliases = new(StringComparer.OrdinalIgnoreCase)
{
    ["UAE"] = "United Arab Emirates",
    ["Trinidad & Tobago"] = "Trinidad and Tobago",
    ["Ivory Coast"] = "Cote d'Ivoire",
};
```

**Why Valid:**
- Handles naming inconsistencies between DX cluster feeds and ADIF/cty.dat
- Uses case-insensitive comparison (important for callsign case variations)
- Can be extended as needed for other aliases

**Recommendation:** ✅ Keep as-is. Consider extracting to shared utility if needed for statistics tab.

---

### ✅ VALID: Mode Normalization

**Implementation:**
```csharp
private static string NormalizeMode(string mode)
{
    var upper = mode.ToUpperInvariant();
    return upper switch
    {
        "USB" or "LSB" => "SSB",
        "PSK31" or "PSK63" or "PSK125" => "PSK",
        _ => upper,
    };
}
```

**Why Valid:**
- Follows amateur radio conventions (USB/LSB are both phone modes under SSB category)
- PSK variants grouped logically
- Uses uppercase for consistency
- Other modes (CW, FT8, RTTY, etc.) preserved as-is

**Recommendation:** ✅ Keep as-is. Consider adding more PSK variants (PSK63, PSK250) or JT variants (JT65, JT9) if needed in future.

---

### ✅ VALID: Dual Country Resolution

**Implementation:**
The service indexes QSOs by **both**:
1. The country name stored in the QSO (from user entry or ADIF import)
2. The country resolved via `CtyService.GetCountryFromCallsign()` (from cty.dat prefix matching)

**Code:**
```csharp
// Index by QSO-stored country
if (!string.IsNullOrEmpty(country))
{
    var normalizedCountry = NormalizeCountryName(country);
    _workedCountries.Add(normalizedCountry);
    _workedCountryBands.Add($"{normalizedCountry}:{band}");
}

// Also index by CtyService-resolved country (handles naming mismatches)
var (ctyCountry, _) = CtyService.GetCountryFromCallsign(callsign);
if (!string.IsNullOrEmpty(ctyCountry) && !string.Equals(ctyCountry, country, StringComparison.OrdinalIgnoreCase))
{
    var normalizedCtyCountry = NormalizeCountryName(ctyCountry);
    _workedCountries.Add(normalizedCtyCountry);
    _workedCountryBands.Add($"{normalizedCtyCountry}:{band}");
}
```

**Why Valid:**
- Handles data quality issues (user may enter "Germany" but cty.dat has "Fed. Rep. of Germany")
- Ensures spots are matched even with naming inconsistencies
- Prevents false "new DXCC" alerts due to naming mismatches
- Only adds CtyService country if it differs from stored country (avoids duplicates)

**Recommendation:** ✅ Excellent approach. This should be the model for statistics calculations.

---

### ✅ VALID: Cache Rebuild on Startup

**Implementation:**
- Service implements `IHostedService` with `StartAsync()` that builds cache from all QSOs
- Can be manually invalidated via `InvalidateCacheAsync()` (e.g., after bulk import)
- Uses scoped service provider to access database

**Why Valid:**
- Ensures cache is always up-to-date on application start
- Handles QSOs logged before service was running
- Thread-safe with `lock (_cacheLock)`
- Efficient in-memory lookups for real-time spot status

**Recommendation:** ✅ Keep as-is. Statistics calculations can reuse similar aggregation logic.

---

## Potential Improvements

### 1. DXCC Code vs Country Name

**Current:** Service uses country **names** (strings) as keys
**ARRL Standard:** DXCC entities have numeric codes (1-999)

**Consideration:**
- Current approach works but may have edge cases with entity name changes
- DXCC codes are more stable (e.g., USSR deleted but code preserved for Honor Roll)
- Using codes would align with ADIF standard (`DXCC` field is integer)

**Recommendation for Statistics Tab:**
- Use DXCC codes as primary keys when available
- Fall back to country names for QSOs without DXCC code
- Maintain country name → DXCC code mapping

**Impact:** Low priority for SpotStatusService (name-based works fine), higher priority for statistics tab (need alignment with ARRL entity list)

---

### 2. Confirmation Status Not Considered

**Current:** SpotStatusService only tracks "worked" status, not "confirmed"
**For Statistics:** Need to distinguish worked vs confirmed (especially for DXCC matrix color-coding)

**Recommendation:**
- SpotStatusService is correct as-is (spots show what's worked, not what's confirmed)
- Statistics tab needs separate logic for confirmation tracking
- Use `Qsl.Lotw.Rcvd`, `Qsl.Eqsl.Rcvd`, `Qsl.Rcvd` fields from QSO model

---

### 3. Mode Categories for Awards

**Current:** Mode normalization is basic (USB/LSB → SSB, PSK variants → PSK)
**DXCC Rules:** Awards available for Phone, CW, Digital (3 major categories)

**Mapping:**
- **Phone:** SSB, AM, FM
- **CW:** CW (Morse code)
- **Digital:** RTTY, PSK31, FT8, FT4, JT65, MFSK, OLIVIA, etc.

**Recommendation:**
- Add a `GetModeCategory()` utility method for award categorization
- Keep existing `NormalizeMode()` for spot matching (different purpose)
- Example:
  ```csharp
  public static string GetModeCategory(string mode)
  {
      return mode.ToUpperInvariant() switch
      {
          "CW" => "CW",
          "SSB" or "USB" or "LSB" or "AM" or "FM" => "Phone",
          _ => "Digital"  // RTTY, PSK*, FT8, FT4, etc.
      };
  }
  ```

---

## Conclusion

### Overall Assessment: ✅ VALID AND WELL-DESIGNED

The current filtering approach in `SpotStatusService` is:
1. **Algorithmically correct** - Proper use of HashSets for O(1) lookups
2. **Data-quality resilient** - Handles naming inconsistencies via dual indexing
3. **Mode-aware** - Normalizes modes appropriately for tracking
4. **Thread-safe** - Uses proper locking for concurrent access
5. **Efficient** - In-memory cache with lazy initialization

### Recommendations for Statistics Tab Implementation

1. **Reuse Normalization Logic**
   - Extract `NormalizeCountryName()` and `NormalizeMode()` to shared utility class
   - Use same normalization in statistics calculations for consistency

2. **Add DXCC Code Support**
   - Use DXCC integer codes as primary keys for entity tracking
   - Maintain country name as secondary key for display and fallback

3. **Add Confirmation Tracking**
   - Create separate HashSets or database queries for confirmed entities
   - Distinguish LoTW, eQSL, and paper QSL confirmations per ARRL rules

4. **Add Mode Category Mapping**
   - Implement `GetModeCategory()` for Phone/CW/Digital classification
   - Use for DXCC award category filtering

5. **Consider Service Refactoring**
   - Extract shared logic into `AwardsService` or `DxccService`
   - SpotStatusService can depend on shared service for consistency
   - Statistics calculations use same shared service

### No Blockers Found

There are **no fundamental issues** with the current filtering approach that would block statistics tab implementation. The architecture is solid and can be extended naturally.

---

## Appendix: Test Coverage Validation

### Recommended Test Cases

1. **Country Name Normalization**
   - ✅ Test aliases ("UAE" → "United Arab Emirates")
   - ✅ Test case insensitivity ("usa" vs "USA")
   - ✅ Test unknown countries (pass-through)

2. **Mode Normalization**
   - ✅ Test USB/LSB → SSB
   - ✅ Test PSK variants → PSK
   - ✅ Test other modes (CW, FT8, RTTY) preserved

3. **Dual Country Indexing**
   - ✅ Test QSO country matches CtyService resolution
   - ✅ Test QSO country differs from CtyService (both should be indexed)
   - ✅ Test invalid callsign (CtyService returns null)

4. **Spot Status Detection**
   - ✅ Test "newDxcc" (never worked country)
   - ✅ Test "newBand" (country worked but not on this band)
   - ✅ Test "worked" (exact country+band+mode match)
   - ✅ Test null (country+band worked, different mode)

5. **Cache Invalidation**
   - ✅ Test cache rebuild after QSO added
   - ✅ Test cache rebuild after bulk import
   - ✅ Test thread safety (concurrent access)

### Existing Test Coverage

**TODO:** Check if tests exist at `src/Log4YM.Server.Tests/Services/SpotStatusServiceTests.cs`

If tests don't exist, recommend creating them before refactoring for statistics tab.

---

**End of Document**
