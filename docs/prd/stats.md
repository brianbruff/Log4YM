# Statistics & Awards Panel — Product Requirements Document

## Overview

This PRD defines the requirements for a dedicated **Statistics & Awards** panel in Log4YM, addressing the user request ([Issue #191](https://github.com/brianbruff/Log4YM/issues/191)) for extended DXCC statistics with band/mode breakdowns, worked/confirmed distinctions, and award program progress tracking.

The panel is inspired by best-of-breed features from **QLog**, **N1MM+**, and **Log4OM**, and is designed to leverage Log4YM's existing data model and filtering infrastructure.

---

## Research Summary

### QLog (ok2cqr/qlog)

QLog is the most relevant open-source reference. Key features:

- **DXCC × Band matrix**: Rows = DXCC entities, columns = HF bands. Each cell shows W (worked) / C (confirmed via LoTW/QSL) with colour coding.
- **Filterable by**: band, mode, QSL method, continent, CQ zone, ITU zone, date range.
- **Award modules**: DXCC (All, CW, Phone, Digital), VUCC (grid squares on VHF/UHF), WAS (US states), WAZ (CQ zones), WPX (prefixes), IOTA, SOTA, POTA.
- **Spot coloring**: Uses the award database to color DX cluster spots new/worked/confirmed — the same architecture used by Log4YM's `SpotStatusService`.

### N1MM+

N1MM+ is primarily a contest logger. Its relevant statistics features:

- **Real-time QSO rate graph**: QSOs/hour over the last N hours, and a running 60-minute rate.
- **Band × Multiplier matrix**: Shows per-band multiplier counts for the active contest.
- **Band change history**: Timestamped log of operator band changes.
- **Score breakdown**: Per-band QSO counts, multiplier counts, total score.

These are contest-focused; the rate/trend concept is useful for general logging statistics.

### Log4OM 2/3

Log4OM has the most comprehensive award-tracking implementation:

- **Award programs**: DXCC, WAS, VUCC, WAZ, IOTA, SOTA, POTA, WWFF, WPX, WAC, 10-10, Worked All Continents.
- **Per-award views**: Each has worked/confirmed counts, per-band/mode filters, and progress bars.
- **QSL service integration**: LoTW, eQSL, Club Log, QRZ — with confirmation auto-imported and matched.
- **Statistics dashboard**: QSO totals by year, band, mode, continent; most-worked countries/prefixes; longest/shortest QSOs.

---

## Current State Analysis

### What Log4YM Already Has

| Feature | Implementation |
|---------|---------------|
| Spot status (newDxcc / newBand / worked) | `SpotStatusService` — in-memory HashSets, incrementally updated on every logged QSO |
| Total QSOs, unique callsigns, unique countries, unique grids, QSOs today | `QsoRepository.GetStatisticsAsync()` via MongoDB aggregation |
| QSOs by band / by mode aggregation | `QsoStatistics` DTO |
| Band + mode filtering in log history | `QsoSearchRequest` — callsign, name, band, mode, date, DXCC |
| QSL tracking (sent/rcvd, LoTW, eQSL) | `QslStatus` embedded in `Qso` model |
| CQ zone, ITU zone, continent, grid | `StationInfo` embedded sub-document |
| ADIF extra fields (IOTA, SOTA_REF, POTA_REF, etc.) | `Qso.AdifExtra` (BsonDocument — not first-class queryable) |

### What Is Missing

| Feature | Gap |
|---------|-----|
| Per-band DXCC counts | Not computed |
| Worked vs. confirmed per band | No aggregation; LoTW `Rcvd = "Y"` is stored but not surfaced in stats |
| DXCC entity × Band matrix view | No UI |
| CQ zone statistics | `StationInfo.CqZone` stored but not aggregated |
| Award progress (DXCC challenge count, etc.) | No award tracking layer |
| IOTA / SOTA / POTA tracking | Fields in `AdifExtra` only — not first-class queryable |
| QSO rate / trend chart | Not implemented |
| Top-N worked countries/prefixes | Not computed |
| Continent breakdown | `StationInfo.Continent` stored but not aggregated |

### Review of Current Filtering Approach

The `SpotStatusService` implements a **three-tier in-memory HashSet** approach:

```
_workedCountries           // "country" — set membership = any band/mode
_workedCountryBands       // "country:band"
_workedCountryBandModes   // "country:band:normalizedMode"
```

**Verdict: The approach is valid and should be extended, not replaced.**

Strengths:
- O(1) lookup per spot — critical for real-time DX cluster coloring (potentially hundreds of spots per second)
- Atomic rebuild via `InvalidateCacheAsync()` — safe for ADIF imports
- Incremental update via `OnQsoLogged()` — keeps cache live without full rebuild
- Dual-indexing (stored country name + cty.dat resolved name) handles naming inconsistencies robustly
- Mode normalization (USB/LSB→SSB, PSKxx→PSK) prevents false "new mode" results

Gaps to address:
1. **No confirmed layer** — the cache tracks "worked" but not "confirmed" (LoTW `Rcvd = "Y"`). A fourth set `_confirmedCountryBands` should be added for award tracking.
2. **No DXCC entity number indexing** — the cache uses country name strings. Using DXCC entity number (integer) as the primary key would be more reliable (country names can vary between cty.dat versions).
3. **No export of cache state** — the cache is purely internal. A read endpoint to expose the worked/confirmed state for the statistics panel is needed.

---

## Goals

### Primary Goal (Phase 1)
A **DXCC Statistics Panel** showing per-band worked and confirmed counts for every DXCC entity, with full sorting and filtering — matching the user's #1 priority.

### Secondary Goals (Phase 2+)
- VUCC (grid square) tracking with 2D map export
- IOTA program tracking
- POTA / SOTA tracking
- QSO rate / trend charts
- Award progress dashboards

---

## User Stories

| # | As a... | I want to... | So that... |
|---|---------|-------------|------------|
| US-1 | Ham operator | See a table of all DXCC entities I have worked, broken down by band | I know my DXCC totals per band at a glance |
| US-2 | Ham operator | Distinguish worked from LoTW-confirmed contacts per band | I know which DXCC entities count towards my DXCC award |
| US-3 | Ham operator | Filter the DXCC table by continent, CQ zone, band, or mode | I can focus on specific propagation paths or contest targets |
| US-4 | Ham operator | Sort the DXCC table by entity name, worked count, confirmed status | I can find entities I need most easily |
| US-5 | Ham operator | Click a DXCC row to see all QSOs with that entity | I can review individual contacts |
| US-6 | Ham operator | See my total DXCC count (all bands) and per-band DXCC counts | I can track award progress |
| US-7 | Ham operator | See which grid squares I have worked on VHF/UHF bands | I can track VUCC progress |
| US-8 | Ham operator | Export my worked grid squares for map visualization | I can share/visualize my VUCC coverage |

---

## Phase 1: DXCC Statistics Panel

### 1.1 UI Layout

```
┌─────────────────────────────────────────────────────────────────────────────┐
│  DXCC Statistics                                          [Filter ▼] [Export]│
├──────────────────┬──────────────────────────────────────────────────────────┤
│ Summary Bar:     │ Total DXCC: 209 · Confirmed: 187 · Needed: 91           │
├──────────────────┴──────────────────────────────────────────────────────────┤
│ Filters: [Continent ▼] [CQ Zone ▼] [Band ▼] [Mode ▼] [Status ▼] [Search  ]│
├───────────────────────┬──────┬──────┬──────┬──────┬──────┬──────┬──────────┤
│ DXCC Entity           │ 160m │  80m │  40m │  20m │  15m │  10m │ Confirmed│
├───────────────────────┼──────┼──────┼──────┼──────┼──────┼──────┼──────────┤
│ Albania               │  -   │  W   │  C   │  C   │  W   │  -   │    ✓     │
│ Algeria               │  -   │  -   │  W   │  W   │  -   │  -   │    -     │
│ Andorra               │  -   │  -   │  -   │  C   │  C   │  W   │    ✓     │
│ ...                   │  ... │  ... │  ... │  ... │  ... │  ... │   ...    │
└───────────────────────┴──────┴──────┴──────┴──────┴──────┴──────┴──────────┘
  W = Worked  C = Confirmed (LoTW/QSL)  - = Not worked
```

**Column visibility**: User can toggle which bands to show. Default: 160m, 80m, 40m, 20m, 17m, 15m, 12m, 10m (HF only). VHF/UHF available optionally.

**Status indicators**:
- `-` / blank — not worked on this band
- `W` (amber) — worked but not confirmed
- `C` (green) — confirmed (LoTW rcvd = "Y" or QSL rcvd)

**Sortable columns**: Entity name (A–Z), total bands worked, confirmed status, continent, CQ zone.

### 1.2 Filters

| Filter | Values |
|--------|--------|
| Continent | AF, AN, AS, EU, NA, OC, SA |
| CQ Zone | 1–40 |
| Band | All, 160m, 80m, 60m, 40m, 30m, 20m, 17m, 15m, 12m, 10m, 6m |
| Mode | All, CW, SSB, FT8, FT4, RTTY, PSK, Digital |
| Status | All, Worked (any), Confirmed, Not Worked, Needs Confirm |
| Search | Free text on entity name |

### 1.3 Summary Bar

- **Total DXCC worked** — distinct DXCC entities with at least one QSO (any band/mode)
- **Total DXCC confirmed** — distinct DXCC entities with at least one confirmed QSO
- **Needed for DXCC** — 300 (DXCC Honor Roll) minus confirmed count
- **Band breakdown badges**: e.g., `20m: 189W / 165C`

---

## Phase 2: VUCC Grid Square Statistics

### 2.1 VUCC Overview

VUCC (VHF/UHF Century Club) requires working stations in 100+ 4-character Maidenhead grid squares on 50 MHz and above.

### 2.2 UI Layout

```
┌─────────────────────────────────────────────────────────────────────────────┐
│  VUCC / Grid Statistics                         [Band ▼] [Export GeoJSON]   │
├─────────────────────────────────────────────────────────────────────────────┤
│ 6m: 247 grids worked · 2m: 89 grids · 70cm: 12 grids                       │
├────────────┬────────────────────────────────────────────────────────────────┤
│ Grid Map   │ Grid List Table                                                 │
│ [2D visual]│ Grid | Band | Worked | Confirmed | First QSO | Last QSO        │
│            │ IO91  | 2m   |   ✓    |     ✓     | 2024-03-14│ 2025-01-07    │
│            │ IO82  | 2m   |   ✓    |     -     | 2025-07-22│ 2025-07-22    │
└────────────┴────────────────────────────────────────────────────────────────┘
```

### 2.3 Data Requirements

- `Qso.Grid` (4 or 6 character Maidenhead) already stored
- Band stored on each QSO
- Need: grid + band aggregation query, confirmed distinction

### 2.4 Export

Export worked grids as:
- **CSV**: grid, band, worked, confirmed
- **GeoJSON**: polygon features for each worked grid square (for import into external 2D map tools)

---

## Phase 3: IOTA, SOTA, POTA Tracking

### 3.1 IOTA

Islands on the Air uses reference codes (e.g., `EU-005`). ADIF field: `IOTA`.

Currently stored in `Qso.AdifExtra`. Phase 3 requires:
- Promoting `IOTA` to a first-class indexed field on `Qso`
- Aggregation by IOTA reference: worked/confirmed per island group
- Optional: IOTA reference database (name, region, island count) for display

### 3.2 SOTA

Summits on the Air uses `SOTA_REF` (summit activated) and `MY_SOTA_REF` (chaser). ADIF fields: `SOTA_REF`, `MY_SOTA_REF`.

- Promote to first-class fields
- Count unique summits activated / chased
- Show per-summit first contact date

### 3.3 POTA

Parks on the Air uses `POTA_REF` (park activated) and `MY_POTA_REF`. Currently in `AdifExtra`.

> See also: [POTA Improvements Plan](../plans/pota-improvements-plan.md)

---

## Phase 4: Activity Charts

### 4.1 QSO Rate Chart

- X axis: time (day/week/month/year selectable)
- Y axis: QSO count
- Series: total, by band, by mode
- Inspired by N1MM+'s rate display

### 4.2 Heatmap

- Calendar heatmap of daily QSO activity
- Similar to GitHub contribution graph

### 4.3 Top-N Rankings

- Most-worked countries (count)
- Most-worked callsigns
- Longest distance QSOs (requires lat/lon on both ends)
- Band activity pie/bar chart

---

## Technical Design

### Backend: New Statistics Endpoints

#### `GET /api/statistics/dxcc`

Returns DXCC per-band worked and confirmed counts.

**Query parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `continent` | string | Filter by continent code (AF, EU, etc.) |
| `cqZone` | int | Filter by CQ zone (1–40) |
| `band` | string | Filter to a single band |
| `mode` | string | Filter to a mode group |

**Response:**

```json
{
  "summary": {
    "totalWorked": 209,
    "totalConfirmed": 187,
    "byBand": {
      "20m": { "worked": 189, "confirmed": 165 },
      "40m": { "worked": 152, "confirmed": 130 }
    }
  },
  "entities": [
    {
      "dxccNumber": 1,
      "name": "Canada",
      "continent": "NA",
      "cqZone": 3,
      "ituZone": 4,
      "bands": {
        "20m": { "worked": true, "confirmed": true, "firstQso": "2024-01-15", "qsoCount": 14 },
        "40m": { "worked": true, "confirmed": false, "firstQso": "2024-03-20", "qsoCount": 3 },
        "10m": { "worked": false, "confirmed": false }
      }
    }
  ]
}
```

#### `GET /api/statistics/grids`

Returns worked grid squares per band for VUCC tracking.

**Query parameters:** `band` (required for VUCC: 6m, 2m, 70cm, etc.)

**Response:**

```json
{
  "band": "2m",
  "totalWorked": 89,
  "totalConfirmed": 67,
  "grids": [
    {
      "grid": "IO91",
      "worked": true,
      "confirmed": true,
      "firstQso": "2024-03-14",
      "lastQso": "2025-01-07",
      "qsoCount": 5
    }
  ]
}
```

#### `GET /api/statistics/summary`

Extended version of the existing `/api/qsos/statistics`, adding:

```json
{
  "totalQsos": 3940,
  "uniqueCallsigns": 3030,
  "uniqueCountries": 209,
  "confirmedCountries": 187,
  "uniqueGrids": 2569,
  "qsosToday": 0,
  "qsosByBand": { "20m": 1093, "10m": 1269 },
  "qsosByMode": { "SSB": 3330, "FT8": 330 },
  "qsosByContinent": { "EU": 1450, "NA": 980 },
  "qsosByCqZone": { "14": 890, "5": 340 }
}
```

### Backend: `SpotStatusService` Extensions

**Add confirmed-country tracking:**

```csharp
// New set: countries confirmed on each band (LoTW rcvd = "Y" or QSL rcvd)
private HashSet<string> _confirmedCountryBands = new(StringComparer.OrdinalIgnoreCase);
```

This allows real-time spot coloring to distinguish `newBand` / `worked` / `confirmed` and expose that in the cluster panel.

**Add DXCC entity number as secondary key:**

Index by both `country:band` (existing) and `dxccNumber:band` (new) to support entity-number-based lookups from the statistics API.

### Frontend: New `StatsPlugin` Component

**Location:** `src/Log4YM.Web/src/plugins/StatsPlugin.tsx`

**Plugin tabs:**

```
[DXCC] [Grids/VUCC] [IOTA] [SOTA] [POTA] [Activity]
```

Initially only DXCC tab is implemented (Phase 1).

**State management:**

- Uses TanStack Query (`useQuery`) for server-side data fetching
- Filters stored in local component state (no persistence needed initially)
- Invalidates on: ADIF import, QRZ sync, new QSO logged (via SignalR event)

**Virtual list:** Use `@tanstack/react-virtual` for rendering DXCC entity rows (300 current DXCC entities, manageable without virtualization but recommended for future-proofing with hundreds of filtered rows).

### Data Model Changes

#### Phase 1: No schema changes required

The existing `Qso` model has all necessary fields:
- `Qso.Country` + `Qso.Dxcc` for entity identification
- `Qso.Band` for per-band breakdown
- `Qso.Mode` for per-mode breakdown
- `Qso.Qsl.Lotw.Rcvd` = `"Y"` for LoTW confirmation
- `Qso.Qsl.Rcvd` for paper QSL confirmation
- `StationInfo.CqZone` / `StationInfo.ItuZone` / `StationInfo.Continent` for filtering

#### Phase 3: Promote ADIF fields to first-class

Add to `Qso` model:
```csharp
[BsonElement("iota")]
public string? Iota { get; set; }          // e.g., "EU-005"

[BsonElement("sota_ref")]
public string? SotaRef { get; set; }       // e.g., "G/LD-001"

[BsonElement("pota_ref")]
public string? PotaRef { get; set; }       // e.g., "K-0001"

[BsonElement("my_sota_ref")]
public string? MySotaRef { get; set; }

[BsonElement("my_pota_ref")]
public string? MyPotaRef { get; set; }
```

Add MongoDB indexes on these fields.

---

## Implementation Phases

### Phase 1 — DXCC Statistics Table (MVP)

**Scope:**
- New `GET /api/statistics/dxcc` endpoint with MongoDB aggregation
- New `StatsPlugin.tsx` with DXCC tab only
- Band × Entity matrix UI
- Worked / Confirmed distinction using `Qso.Qsl.Lotw.Rcvd` and `Qso.Qsl.Rcvd`
- Filters: continent, CQ zone, band, status, search
- Summary bar with totals

**Out of scope for Phase 1:**
- `SpotStatusService` confirmed-layer extension (separate story)
- Export functionality
- Non-DXCC award tabs

**Estimated complexity:** Medium — primarily a new aggregation query and new React component.

### Phase 2 — VUCC Grid Tracking

**Scope:**
- New `GET /api/statistics/grids` endpoint
- Grid list tab in `StatsPlugin`
- GeoJSON/CSV export
- VHF/UHF band focus

### Phase 3 — IOTA / SOTA / POTA

**Scope:**
- Promote ADIF fields to first-class model fields
- Add database indexes
- Per-program tracking tabs
- Migration: populate first-class fields from `AdifExtra` on existing QSOs

### Phase 4 — Activity Charts

**Scope:**
- QSO rate/trend charts (recharts or similar)
- Calendar heatmap
- Top-N rankings

---

## Acceptance Criteria

### Phase 1

- [ ] `GET /api/statistics/dxcc` returns a list of all worked DXCC entities with per-band worked/confirmed flags
- [ ] Response time < 2 seconds for a logbook of 10,000 QSOs (MongoDB aggregation with index on `dxcc`, `band`, `qsl.lotw.rcvd`)
- [ ] `StatsPlugin` displays DXCC band matrix with W/C/- indicators
- [ ] Band columns are configurable (toggle individual bands)
- [ ] Filters work: continent, CQ zone, band, status, name search
- [ ] Summary bar shows total worked, total confirmed, per-band badge counts
- [ ] Clicking a DXCC entity row opens filtered Log History view for that entity
- [ ] Data refreshes automatically when a new QSO is logged (SignalR invalidation)
- [ ] Data refreshes after ADIF import

---

## Open Questions

1. **DXCC entity database**: Should Log4YM embed a static DXCC entity list (country name, entity number, continent, CQ zone, current/deleted status) to ensure the stats table shows _all_ entities (not just worked ones)? This would enable "show unworked entities" view. Source: ARRL DXCC list or cty.dat.

2. **Confirmed definition**: Should "confirmed" mean exclusively LoTW (`Qsl.Lotw.Rcvd = "Y"`) or also paper QSL (`Qsl.Rcvd = "Y"`)? Recommended: make it configurable per user or show both columns.

3. **Historical confirmed status**: For QSOs imported from external loggers, LoTW confirmation status may not be populated. Should there be a LoTW import step to populate `Qsl.Lotw.Rcvd`?

4. **SpotStatusService update timing**: Should confirmed status be reflected in spot coloring (e.g., spot shows "confirmed" instead of "worked")? This requires extending the cluster panel UI.

5. **DXCC challenge vs. standard**: DXCC Challenge counts band-entity combinations (max 10 bands × 340 entities = 3,400 points). Should this be tracked separately?

---

## References

- [Issue #191 — Extended/separate statistics tab](https://github.com/brianbruff/Log4YM/issues/191)
- [Log History PRD](../prds/log-history-prd.md) — existing filtering implementation
- [POTA Improvements Plan](../plans/pota-improvements-plan.md)
- [QLog source](https://github.com/ok2cqr/qlog) — reference implementation for DXCC matrix UI
- ADIF 3.1.4 specification — field definitions for IOTA, SOTA_REF, POTA_REF, GRIDSQUARE, DXCC
- ARRL DXCC Rules — confirmation requirements, band/mode categories
- RSGB IOTA Directory — island reference codes
