# POTA Plugin Fix - API Endpoint Update

**Date**: 2026-04-07
**Branch**: `feature/pota_fix`
**Issue**: POTA plugin no longer working

## Root Cause

The POTA plugin was using an outdated API endpoint that is no longer functional:
- **Old endpoint**: `https://api.pota.app/spot/`
- **New endpoint**: `https://api.pota.app/spot/activator`

## Solution

Based on the OpenHamClock implementation (which solved the same issue), we updated Log4YM to use the correct endpoint.

### Changes Made

1. **Updated API Endpoint** (`PotaController.cs:36`)
   - Changed from `/spot/` to `/spot/activator`

2. **Added Timezone Handling** (`PotaController.cs:60-67`)
   - POTA API returns UTC timestamps without the 'Z' suffix, violating ISO 8601
   - This causes JavaScript to interpret timestamps as local time instead of UTC
   - Added defensive code to append 'Z' suffix if not present

### Code Changes

**File**: `src/Log4YM.Server/Controllers/PotaController.cs`

```csharp
// Line 36: Updated endpoint
var response = await httpClient.GetAsync("https://api.pota.app/spot/activator");

// Lines 60-67: Timezone handling
// POTA API returns UTC timestamps without 'Z' suffix, violating ISO 8601
// Ensure timestamps are properly interpreted as UTC
if (!string.IsNullOrEmpty(spot.SpotTime) &&
    !spot.SpotTime.EndsWith("Z", StringComparison.OrdinalIgnoreCase))
{
    spot.SpotTime += "Z";
}
```

## Reference

This fix was inspired by OpenHamClock's implementation:
- Repository: https://github.com/accius/openhamclock
- File: `server/routes/spots.js` (line 18)
- File: `src/hooks/usePOTASpots.js` (lines 97-103)

## Testing

Build verification:
- ✅ Backend builds successfully with no errors
- ⚠️ Live testing requires access to POTA API (not available in sandbox)

## Additional Improvements (Future Consideration)

OpenHamClock implements several additional robustness features that Log4YM could consider:

1. **Better Spot Filtering**
   - Filter out QRT (operator signed off) spots
   - Filter out spots expiring within 60 seconds
   - More aggressive age filtering (>60 minutes)

2. **Frequency Conversion**
   - Convert frequency from kHz to MHz for consistency

3. **Stale Cache Fallback**
   - Return stale cache (up to 10 minutes old) on API errors

4. **Longer Cache TTL**
   - Use 90-second cache vs current approach

These improvements are documented in `/docs/plans/pota-improvements-plan.md` for future implementation.
