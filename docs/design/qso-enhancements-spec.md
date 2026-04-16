# QSO Details Enhancement - Design Specification

**Version:** 1.0
**Date:** 2026-04-16
**Author:** Claude (AI)
**Status:** Draft - Pending Review

## Table of Contents

- [Executive Summary](#executive-summary)
- [Design Philosophy](#design-philosophy)
- [Current State Analysis](#current-state-analysis)
- [Proposed Enhancements](#proposed-enhancements)
- [Data Model Changes](#data-model-changes)
- [UI/UX Design](#uiux-design)
- [Backend Changes](#backend-changes)
- [ADIF Mapping](#adif-mapping)
- [Implementation Phases](#implementation-phases)
- [Testing Strategy](#testing-strategy)
- [Migration Strategy](#migration-strategy)

---

## Executive Summary

This specification outlines enhancements to Log4YM's QSO capture and data model to support additional fields commonly used by amateur radio operators for awards tracking, propagation analysis, and integration with external services (LoTW, Club Log, QRZ).

### Key Goals

1. **Simplicity First**: Default UI remains clean and focused on essential QSO capture
2. **Progressive Disclosure**: Advanced fields available through expandable sections
3. **Smart Defaults**: Leverage existing integrations (QRZ lookup) to auto-populate data
4. **ADIF Compliance**: Full support for ADIF 3.1.4 specification
5. **Backward Compatibility**: Existing QSOs remain functional; new fields are optional

---

## Design Philosophy

### Principle: Simple by Default, Powerful by Choice

The design follows a **tiered disclosure** approach:

```
┌─────────────────────────────────┐
│   Core Fields (Always Visible)   │  ← Essential for every QSO
├───────────────────────────────────┤
│   Common Fields (Expandable)     │  ← Used frequently, hidden by default
├───────────────────────────────────┤
│   Advanced Fields (Expandable)   │  ← Award tracking, propagation
├───────────────────────────────────┤
│   External Services (Tab)         │  ← LoTW, Club Log, QRZ sync
└───────────────────────────────────┘
```

### UI Expansion Pattern

- **Collapsed by default**: Show only essential fields
- **Expandable sections**: Click to reveal grouped fields
- **Persistent state**: Remember user's expansion preferences
- **Visual indicators**: Show when auto-populated data is available

---

## Current State Analysis

### Existing QSO Model (Qso.cs)

**Currently Supported:**
- Core QSO data: Callsign, Date/Time, Band, Mode, Frequency, RST
- Station info: QTH, Grid (in StationInfo)
- QSL status: Basic structure exists (QslStatus with LoTW/eQSL)
- Contest info: Basic structure exists (ContestInfo)
- Name, Country, Grid, DXCC, Continent (first-class fields)
- QRZ sync status (QrzLogId, QrzSyncedAt, QrzSyncStatus)

**Gaps Identified:**
- IOTA not captured
- DXCC prefix not stored separately
- QSL delivery methods not tracked (Bureau/Direct)
- Club Log integration not implemented
- Awards tracking fields missing (Grid, ARRL, IOTA, ITU, CQ, Prefix)
- Propagation data not captured (K-index, A-index, SSN, SFI)
- Distance calculation not implemented
- Name auto-population works but grid locator not auto-populated in form

### Current Frontend (LogEntryPlugin.tsx)

**Currently Supported:**
- Callsign, Band, Mode, Frequency
- RST Sent/Received with +dB enhancements
- Name (with QRZ auto-fill and lock/unlock)
- Comment, Notes
- Timestamp (with lock/unlock for auto-update)
- QRZ integration for auto-population

**Gaps:**
- No grid locator input field
- No IOTA field
- No QTH field (different from station QTH)
- No contest field
- No propagation fields
- No awards tracking UI

---

## Proposed Enhancements

### Phase 1: Essential New Fields (High Priority)

These fields provide immediate value and are commonly used:

1. **Grid Locator (DX Station)** - Auto-populate from QRZ, allow manual override
2. **IOTA Reference** - Manual entry with validation (e.g., EU-005)
3. **QTH (DX Station)** - Auto-populate from QRZ, allow manual override
4. **DXCC Prefix** - Auto-populate from callsign parsing
5. **Contest ID** - Expand existing contest support

### Phase 2: QSL & External Integration (Medium Priority)

Enhance tracking of QSL cards and external service status:

6. **QSL Delivery Methods**
   - Sent via: Bureau / Direct / Electronic
   - Received via: Bureau / Direct / Electronic
7. **LoTW Status** - Enhanced tracking (already partially implemented)
8. **Club Log Status** - New integration point

### Phase 3: Awards & Advanced Tracking (Lower Priority)

For serious award chasers and contest operators:

9. **Awards Tracking Fields**
   - Grid Award Status
   - ARRL Award Status
   - IOTA Award Status
   - ITU Zone Award Status
   - CQ Zone Award Status
   - Prefix Award Status
10. **Propagation Data**
    - K-index (geomagnetic)
    - A-index (geomagnetic)
    - SSN (Solar Sunspot Number)
    - SFI (Solar Flux Index)
11. **Distance Calculation** - Auto-calculate from grid locators

---

## Data Model Changes

### Enhanced QSO Model (Qso.cs)

```csharp
public class Qso
{
    // ... existing fields ...

    // Phase 1: Essential new fields
    [BsonElement("qth")]
    public string? Qth { get; set; }  // DX station QTH (city/location)

    [BsonElement("iota")]
    public string? Iota { get; set; }  // IOTA reference (e.g., EU-005)

    [BsonElement("dxcc_prefix")]
    public string? DxccPrefix { get; set; }  // DXCC prefix (e.g., EI, G, W)

    [BsonElement("distance_km")]
    public double? DistanceKm { get; set; }  // Auto-calculated from grids

    // Phase 2: QSL enhancements (extend existing QslStatus)
    // See QslStatus changes below

    // Phase 3: Awards tracking
    [BsonElement("awards")]
    public AwardTracking? Awards { get; set; }

    // Phase 3: Propagation data
    [BsonElement("propagation")]
    public PropagationData? Propagation { get; set; }

    // Club Log sync tracking (following QRZ pattern)
    [BsonElement("clubLogSyncedAt")]
    public DateTime? ClubLogSyncedAt { get; set; }

    [BsonElement("clubLogSyncStatus")]
    [BsonRepresentation(BsonType.String)]
    public SyncStatus ClubLogSyncStatus { get; set; } = SyncStatus.NotSynced;
}

public class QslStatus
{
    // ... existing fields ...

    // Phase 2: Delivery method tracking
    [BsonElement("sentVia")]
    public string? SentVia { get; set; }  // "Bureau", "Direct", "Electronic", or null

    [BsonElement("rcvdVia")]
    public string? RcvdVia { get; set; }  // "Bureau", "Direct", "Electronic", or null

    // Enhanced LoTW tracking
    [BsonElement("lotwSentDate")]
    public DateTime? LotwSentDate { get; set; }

    [BsonElement("lotwRcvdDate")]
    public DateTime? LotwRcvdDate { get; set; }

    // Club Log tracking
    [BsonElement("clubLog")]
    public ClubLogStatus? ClubLog { get; set; }
}

// Phase 2: Club Log status
public class ClubLogStatus
{
    [BsonElement("uploaded")]
    public bool? Uploaded { get; set; }

    [BsonElement("uploadedDate")]
    public DateTime? UploadedDate { get; set; }

    [BsonElement("confirmed")]
    public bool? Confirmed { get; set; }
}

// Phase 3: Awards tracking
public class AwardTracking
{
    [BsonElement("gridConfirmed")]
    public bool? GridConfirmed { get; set; }

    [BsonElement("arrlConfirmed")]
    public bool? ArrlConfirmed { get; set; }

    [BsonElement("iotaConfirmed")]
    public bool? IotaConfirmed { get; set; }

    [BsonElement("ituConfirmed")]
    public bool? ItuConfirmed { get; set; }

    [BsonElement("cqConfirmed")]
    public bool? CqConfirmed { get; set; }

    [BsonElement("prefixConfirmed")]
    public bool? PrefixConfirmed { get; set; }
}

// Phase 3: Propagation data
public class PropagationData
{
    [BsonElement("kIndex")]
    public int? KIndex { get; set; }  // 0-9

    [BsonElement("aIndex")]
    public int? AIndex { get; set; }  // 0-400

    [BsonElement("ssn")]
    public int? Ssn { get; set; }  // Solar Sunspot Number (0-300+)

    [BsonElement("sfi")]
    public int? Sfi { get; set; }  // Solar Flux Index (typically 60-300)
}
```

### Grid Locator Enhancement

Current behavior:
- Grid locator is fetched from QRZ and stored in `focusedCallsignInfo`
- It's shown in the callsign info card but not in an editable field

Enhanced behavior:
- Add a `grid` field to the form (similar to `name`)
- Auto-populate from QRZ lookup
- Allow manual entry/override
- Validate format (e.g., IO91ab, FN31pr)

---

## UI/UX Design

### Updated LogEntryPlugin.tsx Structure

```
┌─────────────────────────────────────┐
│ Log Entry                     [⚡]  │  ← Existing header with radio toggle
├─────────────────────────────────────┤
│ Callsign   [W1XYZ    ]             │  ← Core section (always visible)
│ Band [20m] Mode [SSB]              │
│                                     │
│ [QRZ Info Card]                    │  ← If callsign lookup successful
│                                     │
│ Name [John Doe]          [🔓]      │  ← Lock toggle (existing)
│ Grid Locator [FN42ab]    [🔓]      │  ← NEW: Grid with lock toggle
│                                     │
│ Freq [14.250] RST 59 / 59         │  ← Existing
│                                     │
│ ▼ Additional Fields                │  ← EXPANDABLE SECTION (collapsed)
├─────────────────────────────────────┤
│ QTH [Boston, MA]                   │  ← When expanded
│ IOTA [NA-003]                      │
│ Contest [CQ WW DX]                 │
│ Comment [Nice signal...]           │
│ Notes [Personal notes...]          │
├─────────────────────────────────────┤
│ ▼ Advanced Tracking                │  ← EXPANDABLE SECTION (collapsed)
├─────────────────────────────────────┤
│ ▶ QSL & External Services          │  ← EXPANDABLE SECTION (collapsed)
├─────────────────────────────────────┤
│ ⏰ 2026-04-16 21:30 UTC [🔓]       │  ← Existing timestamp
│                                     │
│ [Log QSO]           [Clear]        │  ← Existing actions
└─────────────────────────────────────┘
```

### Expandable Section: "Additional Fields"

When expanded:

```tsx
<div className="space-y-2 border-t border-glass-100 pt-2 mt-2">
  <button
    onClick={() => setShowAdditionalFields(!showAdditionalFields)}
    className="w-full text-left text-xs font-ui text-dark-300 hover:text-dark-200 flex items-center gap-1"
  >
    {showAdditionalFields ? <ChevronDown /> : <ChevronRight />}
    Additional Fields
  </button>

  {showAdditionalFields && (
    <>
      {/* QTH */}
      <div>
        <label className="text-xs font-ui text-dark-300 mb-1 flex items-center gap-1">
          <MapPin className="w-3 h-3" />
          QTH (Location)
          {qthFromQrz && <span className="w-1.5 h-1.5 rounded-full bg-accent-primary" title="From QRZ" />}
        </label>
        <input
          type="text"
          value={formData.qth}
          onChange={(e) => setFormData(prev => ({ ...prev, qth: e.target.value }))}
          placeholder="City, State/Province"
          className="glass-input w-full text-sm"
        />
      </div>

      {/* IOTA */}
      <div>
        <label className="text-xs font-ui text-dark-300 mb-1 flex items-center gap-1">
          <Globe className="w-3 h-3" />
          IOTA Reference
        </label>
        <input
          type="text"
          value={formData.iota}
          onChange={(e) => setFormData(prev => ({ ...prev, iota: e.target.value.toUpperCase() }))}
          placeholder="EU-005, NA-003, etc."
          className="glass-input w-full text-sm font-mono uppercase"
        />
      </div>

      {/* Contest */}
      <div>
        <label className="text-xs font-ui text-dark-300 mb-1 flex items-center gap-1">
          <Trophy className="w-3 h-3" />
          Contest
        </label>
        <input
          type="text"
          value={formData.contest}
          onChange={(e) => setFormData(prev => ({ ...prev, contest: e.target.value }))}
          placeholder="CQ WW DX, ARRL DX, etc."
          className="glass-input w-full text-sm"
        />
      </div>

      {/* Comment (moved here from always-visible) */}
      {/* Notes (moved here from always-visible) */}
    </>
  )}
</div>
```

### Expandable Section: "QSL & External Services"

```tsx
<div className="space-y-2 border-t border-glass-100 pt-2 mt-2">
  <button
    onClick={() => setShowQslTracking(!showQslTracking)}
    className="w-full text-left text-xs font-ui text-dark-300 hover:text-dark-200 flex items-center gap-1"
  >
    {showQslTracking ? <ChevronDown /> : <ChevronRight />}
    QSL & External Services
  </button>

  {showQslTracking && (
    <>
      {/* QSL Card Status */}
      <div className="grid grid-cols-2 gap-2">
        <div>
          <label className="text-xs font-ui text-dark-300 mb-1">QSL Sent Via</label>
          <select className="glass-input w-full text-sm">
            <option value="">--</option>
            <option value="Bureau">Bureau</option>
            <option value="Direct">Direct</option>
            <option value="Electronic">Electronic</option>
          </select>
        </div>
        <div>
          <label className="text-xs font-ui text-dark-300 mb-1">QSL Rcvd Via</label>
          <select className="glass-input w-full text-sm">
            <option value="">--</option>
            <option value="Bureau">Bureau</option>
            <option value="Direct">Direct</option>
            <option value="Electronic">Electronic</option>
          </select>
        </div>
      </div>

      {/* LoTW Status (read-only, synced from LoTW service) */}
      <div className="bg-dark-700/30 rounded p-2 text-xs">
        <div className="flex items-center justify-between">
          <span className="text-dark-300">LoTW Status:</span>
          <span className={lotwStatus ? "text-accent-success" : "text-dark-400"}>
            {lotwStatus || "Not synced"}
          </span>
        </div>
      </div>

      {/* Club Log Status (read-only, synced from Club Log service) */}
      <div className="bg-dark-700/30 rounded p-2 text-xs">
        <div className="flex items-center justify-between">
          <span className="text-dark-300">Club Log:</span>
          <span className={clubLogStatus ? "text-accent-success" : "text-dark-400"}>
            {clubLogStatus || "Not uploaded"}
          </span>
        </div>
      </div>
    </>
  )}
</div>
```

### Expandable Section: "Advanced Tracking"

```tsx
<div className="space-y-2 border-t border-glass-100 pt-2 mt-2">
  <button
    onClick={() => setShowAdvancedTracking(!showAdvancedTracking)}
    className="w-full text-left text-xs font-ui text-dark-300 hover:text-dark-200 flex items-center gap-1"
  >
    {showAdvancedTracking ? <ChevronDown /> : <ChevronRight />}
    Advanced Tracking
  </button>

  {showAdvancedTracking && (
    <>
      {/* Propagation Data */}
      <div className="space-y-1">
        <label className="text-xs font-ui text-dark-300">Propagation Data</label>
        <div className="grid grid-cols-4 gap-1">
          <input type="number" placeholder="K" min="0" max="9" className="glass-input text-xs" />
          <input type="number" placeholder="A" min="0" max="400" className="glass-input text-xs" />
          <input type="number" placeholder="SSN" min="0" className="glass-input text-xs" />
          <input type="number" placeholder="SFI" min="60" className="glass-input text-xs" />
        </div>
        <div className="text-[10px] text-dark-400 flex justify-between px-1">
          <span>K-index</span>
          <span>A-index</span>
          <span>Sunspot</span>
          <span>Solar Flux</span>
        </div>
      </div>

      {/* Award Confirmations (checkboxes) */}
      <div className="space-y-1">
        <label className="text-xs font-ui text-dark-300">Award Confirmations</label>
        <div className="grid grid-cols-3 gap-1 text-xs">
          <label className="flex items-center gap-1">
            <input type="checkbox" className="glass-checkbox" />
            <span>Grid</span>
          </label>
          <label className="flex items-center gap-1">
            <input type="checkbox" className="glass-checkbox" />
            <span>ARRL</span>
          </label>
          <label className="flex items-center gap-1">
            <input type="checkbox" className="glass-checkbox" />
            <span>IOTA</span>
          </label>
          <label className="flex items-center gap-1">
            <input type="checkbox" className="glass-checkbox" />
            <span>ITU</span>
          </label>
          <label className="flex items-center gap-1">
            <input type="checkbox" className="glass-checkbox" />
            <span>CQ</span>
          </label>
          <label className="flex items-center gap-1">
            <input type="checkbox" className="glass-checkbox" />
            <span>Prefix</span>
          </label>
        </div>
      </div>

      {/* Distance (auto-calculated, read-only) */}
      {calculatedDistance && (
        <div className="bg-dark-700/30 rounded p-2 text-xs">
          <div className="flex items-center justify-between">
            <span className="text-dark-300">Distance:</span>
            <span className="text-gray-200 font-mono">
              {calculatedDistance.toFixed(0)} km / {(calculatedDistance * 0.621371).toFixed(0)} mi
            </span>
          </div>
        </div>
      )}
    </>
  )}
</div>
```

### User Preferences

Store expansion state in user settings:

```typescript
interface QsoFormPreferences {
  showAdditionalFields: boolean;
  showQslTracking: boolean;
  showAdvancedTracking: boolean;
}
```

---

## Backend Changes

### Services to Update

#### 1. QsoService.cs

Add methods for:
- DXCC prefix parsing from callsign
- Distance calculation from grid locators
- Grid locator validation

```csharp
public class QsoService
{
    public string? ExtractDxccPrefix(string callsign)
    {
        // Implement callsign prefix extraction logic
        // Examples: W1ABC -> W, G4XYZ -> G, EI6LF -> EI
    }

    public double? CalculateDistance(string? grid1, string? grid2)
    {
        // Implement Maidenhead grid distance calculation
        // Returns distance in kilometers, or null if either grid is invalid
    }

    public bool IsValidGridLocator(string? grid)
    {
        // Validate Maidenhead grid format (e.g., IO91ab, FN31pr)
    }
}
```

#### 2. AdifService.cs

Update ADIF export/import to handle new fields:

```csharp
// ADIF Field Mappings (Phase 1)
<QTH:length>value          → Qso.Qth
<IOTA:length>value         → Qso.Iota
<GRIDSQUARE:length>value   → Qso.Grid (already exists)
<DISTANCE:length>value     → Qso.DistanceKm

// ADIF Field Mappings (Phase 2)
<QSL_SENT_VIA:length>value → Qso.Qsl.SentVia
<QSL_RCVD_VIA:length>value → Qso.Qsl.RcvdVia
<LOTW_QSLSDATE:length>value → Qso.Qsl.LotwSentDate
<LOTW_QSLRDATE:length>value → Qso.Qsl.LotwRcvdDate

// ADIF Field Mappings (Phase 3)
<K_INDEX:length>value      → Qso.Propagation.KIndex
<A_INDEX:length>value      → Qso.Propagation.AIndex
<SUN_SPOT_NUMBER:length>value → Qso.Propagation.Ssn (note: field is SUN_SPOT_NUMBER, not SSN)
<SFI:length>value          → Qso.Propagation.Sfi

// Award fields (non-standard, store in AdifExtra)
<APP_LOG4YM_GRID_CONFIRMED:1>Y    → Qso.Awards.GridConfirmed
<APP_LOG4YM_ARRL_CONFIRMED:1>Y    → Qso.Awards.ArrlConfirmed
// etc.
```

#### 3. QrzService.cs

Enhance to return additional fields:

```csharp
public class CallsignLookupResult
{
    // ... existing fields ...
    public string? Qth { get; set; }  // City/location from QRZ
    // QRZ already provides grid, name, country
}
```

---

## ADIF Mapping

### ADIF 3.1.4 Field Reference

| Field Name | ADIF Tag | Data Type | QSO Model Property | Notes |
|------------|----------|-----------|-------------------|-------|
| Grid Locator | `GRIDSQUARE` | Grid | `Qso.Grid` | Already supported |
| IOTA Reference | `IOTA` | IOTA Ref | `Qso.Iota` | **NEW** |
| QTH | `QTH` | String | `Qso.Qth` | **NEW** |
| DXCC Prefix | N/A (derived) | - | `Qso.DxccPrefix` | Calculated |
| Distance | `DISTANCE` | Number | `Qso.DistanceKm` | **NEW** (auto-calc) |
| Contest ID | `CONTEST_ID` | String | `Qso.Contest.ContestId` | Existing |
| QSL Sent Via | `QSL_SENT_VIA` | Enumeration | `Qso.Qsl.SentVia` | **ENHANCED** |
| QSL Rcvd Via | `QSL_RCVD_VIA` | Enumeration | `Qso.Qsl.RcvdVia` | **ENHANCED** |
| LoTW QSL Sent Date | `LOTW_QSLSDATE` | Date | `Qso.Qsl.LotwSentDate` | **NEW** |
| LoTW QSL Rcvd Date | `LOTW_QSLRDATE` | Date | `Qso.Qsl.LotwRcvdDate` | **NEW** |
| K-Index | `K_INDEX` | Number | `Qso.Propagation.KIndex` | **NEW** |
| A-Index | `A_INDEX` | Number | `Qso.Propagation.AIndex` | **NEW** |
| Solar Flux | `SFI` | Number | `Qso.Propagation.Sfi` | **NEW** |
| Sunspot Number | `SUN_SPOT_NUMBER` | Number | `Qso.Propagation.Ssn` | **NEW** |

### QSL_VIA Enumeration Values

ADIF specifies the following values for `QSL_SENT_VIA` and `QSL_RCVD_VIA`:
- `B` - Bureau
- `D` - Direct
- `E` - Electronic
- `M` - Manager

We'll store the full text ("Bureau", "Direct", "Electronic") internally and convert to/from single-letter codes during ADIF import/export.

---

## Implementation Phases

### Phase 1: Essential Fields (2-3 days)

**Goal**: Add most commonly used fields to capture panel

**Tasks**:
1. Update `Qso.cs` model with new fields (Qth, Iota, DxccPrefix, DistanceKm)
2. Update ADIF import/export in `AdifService.cs`
3. Add grid locator field to `LogEntryPlugin.tsx` with QRZ auto-fill
4. Add expandable "Additional Fields" section with QTH, IOTA, Contest
5. Implement DXCC prefix extraction service method
6. Implement distance calculation service method
7. Update API DTO (`CreateQsoRequest`) to accept new fields
8. Add database indexes for new searchable fields

**Deliverables**:
- Enhanced QSO model with Phase 1 fields
- Updated capture panel with grid locator and expandable section
- ADIF import/export support for new fields
- Auto-calculation of DXCC prefix and distance

### Phase 2: QSL & External Services (3-4 days)

**Goal**: Enhance QSL tracking and add external service integration placeholders

**Tasks**:
1. Update `QslStatus` class with SentVia, RcvdVia, enhanced LoTW dates
2. Add `ClubLogStatus` class and Club Log sync fields to Qso
3. Add "QSL & External Services" expandable section to UI
4. Update ADIF import/export for QSL via fields
5. Design Club Log API integration (similar to QRZ pattern)
6. Add settings for Club Log API credentials
7. Implement Club Log sync status tracking

**Deliverables**:
- Enhanced QSL tracking with delivery methods
- Club Log integration framework (sync later)
- LoTW integration preparation (full integration separate project)
- Updated UI with QSL tracking section

### Phase 3: Advanced Tracking (2-3 days)

**Goal**: Add awards and propagation data for advanced users

**Tasks**:
1. Add `AwardTracking` and `PropagationData` classes
2. Add "Advanced Tracking" expandable section to UI
3. Update ADIF import/export for propagation fields
4. Implement validation for propagation data ranges
5. Add filters to log history for award confirmations
6. Update statistics panel to show propagation trends (optional)

**Deliverables**:
- Complete awards tracking system
- Propagation data capture
- Enhanced filtering capabilities

---

## Testing Strategy

### Unit Tests

**Model Tests (C#)**:
- Validate DXCC prefix extraction for various callsign formats
- Test distance calculation accuracy
- Test grid locator validation
- Test ADIF field mapping for all new fields

**Service Tests (C#)**:
- Test ADIF import with new fields
- Test ADIF export with new fields
- Test QSO creation with all field combinations
- Test backward compatibility (old QSOs still work)

### Integration Tests

**API Tests**:
- POST `/api/qso` with all new fields
- Verify database storage of new fields
- Verify ADIF export includes new fields
- Verify ADIF import populates new fields

**Frontend Tests (React Testing Library)**:
- Test expandable section state management
- Test grid locator auto-population from QRZ
- Test QTH auto-population from QRZ
- Test distance calculation display
- Test form validation

### Manual Testing Checklist

- [ ] Import ADIF file with new fields
- [ ] Export ADIF file and verify new fields present
- [ ] QRZ lookup populates grid locator
- [ ] Distance auto-calculates when both grids present
- [ ] Expandable sections remember state
- [ ] Form validation works for IOTA format
- [ ] QSL via dropdowns function correctly
- [ ] Propagation data validates ranges

---

## Migration Strategy

### Database Migration

**Approach**: Additive changes only (no breaking changes)

All new fields are optional (`string?`, `int?`, `bool?`), so existing QSOs remain valid without migration.

**MongoDB Considerations**:
- No schema enforcement, so new fields simply won't exist on old documents
- Queries handle null/missing fields gracefully
- Indexes should be added for commonly searched fields

**Recommended Indexes**:
```csharp
// Add indexes for new searchable fields
collection.Indexes.CreateOne(new CreateIndexModel<Qso>(
    Builders<Qso>.IndexKeys.Ascending(x => x.Iota)
));

collection.Indexes.CreateOne(new CreateIndexModel<Qso>(
    Builders<Qso>.IndexKeys.Ascending(x => x.Qth)
));

collection.Indexes.CreateOne(new CreateIndexModel<Qso>(
    Builders<Qso>.IndexKeys.Ascending(x => x.DxccPrefix)
));
```

### User Settings Migration

Add new preferences to user settings:
```json
{
  "qsoFormPreferences": {
    "showAdditionalFields": false,
    "showQslTracking": false,
    "showAdvancedTracking": false
  }
}
```

---

## Open Questions & Decisions Needed

### 1. Grid Locator Auto-Population

**Question**: Should the grid locator field be auto-populated and locked like Name, or always editable?

**Options**:
- A) Lock by default with unlock toggle (consistent with Name field)
- B) Always editable but show indicator when from QRZ
- C) Auto-fill on first lookup, then allow manual override without lock

**Recommendation**: Option A (lock by default) for UI consistency

---

### 2. Contest Field Format

**Question**: Should Contest be a free-text field or a dropdown with predefined contests?

**Options**:
- A) Free-text field (ADIF allows any string)
- B) Dropdown with common contests + "Other" option
- C) Autocomplete from previously entered contests

**Recommendation**: Option A (free-text) initially, Option C (autocomplete) in future enhancement

---

### 3. Distance Unit

**Question**: Should distance be stored/displayed in km, miles, or both?

**Options**:
- A) Store in km (SI unit), display both with user preference
- B) Store user's preference (km or mi)
- C) Always store km, always display both

**Recommendation**: Option C (store km, display both) for international compatibility

---

### 4. DXCC Prefix Calculation

**Question**: Should DXCC prefix be auto-calculated on save or stored from a lookup table?

**Options**:
- A) Calculate on save from callsign parsing
- B) Populate from DXCC entity lookup (more accurate)
- C) Allow manual override in advanced section

**Recommendation**: Option B with Option C (lookup with manual override)

---

### 5. Propagation Data Source

**Question**: Should propagation data be manually entered or auto-fetched from a service?

**Options**:
- A) Manual entry only
- B) Auto-fetch from service (e.g., NOAA Space Weather)
- C) Both (auto-fetch with manual override)

**Recommendation**: Option A (manual) for Phase 3, Option C in future enhancement

---

### 6. Awards Tracking Scope

**Question**: Should award confirmations be per-QSO flags or a separate awards tracking system?

**Options**:
- A) Per-QSO boolean flags (simple, matches request)
- B) Separate awards database with references to QSOs (complex, more powerful)
- C) Hybrid: flags on QSO, aggregated awards report

**Recommendation**: Option C (flags + report) for flexibility

---

## Risks & Mitigation

### Risk 1: UI Complexity

**Risk**: Adding many fields could make UI overwhelming

**Mitigation**:
- Use progressive disclosure (expandable sections)
- Default to collapsed state
- Remember user preferences
- Keep core workflow simple

### Risk 2: Performance

**Risk**: Distance calculation on every QSO could slow down UI

**Mitigation**:
- Calculate once on save, store result
- Use debounced calculation during editing
- Only calculate if both grids present

### Risk 3: ADIF Compatibility

**Risk**: ADIF export/import could break with new fields

**Mitigation**:
- Extensive testing with ADIF validator
- Test import from popular logging software
- Test export to popular logging software
- Maintain backward compatibility

### Risk 4: External Service Integration

**Risk**: Club Log/LoTW APIs could change or be unavailable

**Mitigation**:
- Implement as separate services with retry logic
- Graceful degradation if service unavailable
- Store last sync timestamp for user visibility
- Provide manual override options

---

## Success Criteria

### Phase 1 Complete When:
- [ ] User can enter grid locator with QRZ auto-fill
- [ ] User can enter IOTA and QTH in expandable section
- [ ] DXCC prefix auto-calculates from callsign
- [ ] Distance auto-calculates from grids
- [ ] ADIF import/export works with new fields
- [ ] Existing QSOs work without new fields

### Phase 2 Complete When:
- [ ] User can track QSL sent/received via Bureau/Direct/Electronic
- [ ] Club Log sync status shown (even if not yet syncing)
- [ ] LoTW dates captured in ADIF import/export
- [ ] QSL tracking UI is intuitive and clear

### Phase 3 Complete When:
- [ ] User can enter all propagation data (K, A, SSN, SFI)
- [ ] User can track award confirmations per QSO
- [ ] Distance displays in both km and miles
- [ ] ADIF export includes all advanced fields

---

## Future Enhancements (Out of Scope)

These are logical next steps but beyond the current specification:

1. **LoTW Full Integration**: Automated sync with LoTW (requires LoTW API work)
2. **Club Log Full Integration**: Automated upload and confirmation checking
3. **Propagation Auto-Fetch**: Fetch K/A/SFI from space weather services
4. **Awards Report**: Dedicated panel showing progress toward awards
5. **DXCC Entity Lookup**: Reference database for accurate DXCC info
6. **Grid Locator Picker**: Map-based grid square selector
7. **Contest Templates**: Pre-filled exchange formats for common contests
8. **QSL Card Design/Print**: Generate printable QSL cards
9. **Advanced Filtering**: Filter log by IOTA, propagation conditions, etc.
10. **Statistics Enhancement**: Propagation correlation analysis

---

## Appendix A: ADIF Field Reference

### Commonly Used ADIF Fields (Supported)

| ADIF Tag | Description | Type | Log4YM Field |
|----------|-------------|------|--------------|
| `CALL` | Contacted station callsign | String | `Callsign` |
| `QSO_DATE` | QSO date (YYYYMMDD) | Date | `QsoDate` |
| `TIME_ON` | QSO start time (HHMMSS) | Time | `TimeOn` |
| `BAND` | Band (e.g., 20m) | Enumeration | `Band` |
| `MODE` | Mode (e.g., SSB) | Enumeration | `Mode` |
| `FREQ` | Frequency in MHz | Number | `Frequency` |
| `RST_SENT` | Signal report sent | String | `RstSent` |
| `RST_RCVD` | Signal report received | String | `RstRcvd` |
| `NAME` | Contacted operator name | String | `Name` |
| `GRIDSQUARE` | Maidenhead grid locator | Grid | `Grid` |
| `COUNTRY` | DXCC entity name | String | `Country` |
| `DXCC` | DXCC entity code | Number | `Dxcc` |
| `CONT` | Continent | Enumeration | `Continent` |
| `QTH` | City/location | String | `Qth` **NEW** |
| `IOTA` | Islands On The Air ref | String | `Iota` **NEW** |
| `DISTANCE` | Distance in km | Number | `DistanceKm` **NEW** |
| `COMMENT` | QSO comment | String | `Comment` |
| `NOTES` | Private notes | String | `Notes` |

### QSL-Related ADIF Fields

| ADIF Tag | Description | Type | Log4YM Field |
|----------|-------------|------|--------------|
| `QSL_SENT` | QSL sent status | Enumeration | `Qsl.Sent` |
| `QSL_RCVD` | QSL received status | Enumeration | `Qsl.Rcvd` |
| `QSLMSG` | QSL card message | String | - |
| `QSL_SENT_VIA` | QSL sent via | Enumeration | `Qsl.SentVia` **NEW** |
| `QSL_RCVD_VIA` | QSL received via | Enumeration | `Qsl.RcvdVia` **NEW** |
| `LOTW_QSLSDATE` | LoTW QSL sent date | Date | `Qsl.LotwSentDate` **NEW** |
| `LOTW_QSLRDATE` | LoTW QSL received date | Date | `Qsl.LotwRcvdDate` **NEW** |
| `EQSL_QSL_SENT` | eQSL sent status | Enumeration | `Qsl.Eqsl.Sent` |
| `EQSL_QSL_RCVD` | eQSL received status | Enumeration | `Qsl.Eqsl.Rcvd` |

### Propagation ADIF Fields

| ADIF Tag | Description | Type | Log4YM Field |
|----------|-------------|------|--------------|
| `K_INDEX` | Geomagnetic K-index | Number | `Propagation.KIndex` **NEW** |
| `A_INDEX` | Geomagnetic A-index | Number | `Propagation.AIndex` **NEW** |
| `SFI` | Solar Flux Index | Number | `Propagation.Sfi` **NEW** |
| `SUN_SPOT_NUMBER` | Sunspot number | Number | `Propagation.Ssn` **NEW** |
| `MAX_BURSTS` | Solar max bursts | Number | - |
| `ANT_AZ` | Antenna azimuth | Number | - |
| `ANT_EL` | Antenna elevation | Number | - |

---

## Appendix B: Grid Locator Reference

### Maidenhead Locator System

Grid squares use letters and digits to encode latitude/longitude:
- **Field**: 2 letters (AA to RR) - 20° x 10° regions
- **Square**: 2 digits (00 to 99) - 2° x 1° regions
- **Subsquare**: 2 letters (aa to xx) - 5' x 2.5' regions (optional)
- **Extended**: 2 digits (00 to 99) - 0.5' x 0.25' regions (rare)

**Examples**:
- `IO91` - 4-character grid (field + square)
- `IO91ab` - 6-character grid (most common)
- `IO91ab12` - 8-character grid (very precise)

**Validation**:
- Must start with 2 uppercase letters (A-R)
- Followed by 2 digits (0-9)
- Optionally 2 lowercase letters (a-x)
- Optionally 2 more digits (0-9)

---

## Appendix C: Distance Calculation Formula

### Haversine Formula (Great Circle Distance)

Given two grid locators, convert to lat/long then:

```
a = sin²(Δφ/2) + cos φ1 ⋅ cos φ2 ⋅ sin²(Δλ/2)
c = 2 ⋅ atan2(√a, √(1−a))
d = R ⋅ c
```

Where:
- φ = latitude
- λ = longitude
- R = Earth's radius (6371 km)
- Δφ = φ2 − φ1
- Δλ = λ2 − λ1

---

## Document History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0 | 2026-04-16 | Claude | Initial draft |

---

**End of Specification**
