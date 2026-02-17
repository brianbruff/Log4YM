# POTA Support Improvement Plan

**Date**: 2026-02-17
**Issue**: Plan for Improving POTA Support—Review and Integrate Features from potacat.com
**Status**: Draft

## Executive Summary

This document provides a comparative analysis of POTA (Parks on the Air) logging features between Log4YM and modern POTA logging tools (including Ham2K/potacat, HAMRS, and other popular solutions), identifies feature gaps, and proposes a prioritized implementation plan to make Log4YM a competitive POTA logging solution.

## Background

Log4YM currently has basic POTA support:
- **Spotting**: Real-time POTA spot display from api.pota.app
- **Map Integration**: POTA spots can be shown on the map with park coordinates
- **ADIF Import/Export**: General ADIF support for importing/exporting logs
- **Click-to-Tune**: Can click on a POTA spot to tune to that frequency/mode

However, compared to dedicated POTA loggers, Log4YM lacks specific activator/hunter workflows, POTA-specific ADIF fields, statistics, and offline capabilities.

## Comparative Analysis

### Feature Matrix: Log4YM vs. Modern POTA Loggers

| Feature Category | Log4YM (Current) | Ham2K/HAMRS/Modern Tools | Gap Priority |
|-----------------|------------------|-------------------------|--------------|
| **Spotting & Discovery** |
| Real-time spot display | ✅ Yes | ✅ Yes | N/A |
| Spot age indicators | ✅ Yes (time ago) | ✅ Yes | N/A |
| Map overlay for spots | ✅ Yes | ✅ Yes | N/A |
| Filter spots by band/mode | ❌ No | ✅ Yes | **HIGH** |
| Filter spots by park type | ❌ No | ✅ Yes (entity, region) | MEDIUM |
| Personal spot alerts/notifications | ❌ No | ✅ Yes | LOW |
| **Activator Workflow** |
| Quick QSO logging mode | ⚠️ General | ✅ Optimized for POTA | **HIGH** |
| Park reference field in QSO | ❌ No | ✅ Yes (MY_SIG/MY_SIG_INFO) | **CRITICAL** |
| Multi-park activation support | ❌ No | ✅ Yes | **HIGH** |
| Park-to-park (P2P) tracking | ❌ No | ✅ Yes (SIG/SIG_INFO) | **HIGH** |
| Activator statistics | ❌ No | ✅ Yes (QSO count, parks) | MEDIUM |
| Quick park reference entry | ❌ No | ✅ Yes (typeahead/recent) | **HIGH** |
| Offline logging capability | ❌ No | ✅ Yes | **CRITICAL** |
| Session management | ❌ No | ✅ Yes (activation sessions) | MEDIUM |
| **Hunter Workflow** |
| Hunter mode | ❌ No | ✅ Yes (simplified logging) | MEDIUM |
| Track worked parks | ❌ No | ✅ Yes | MEDIUM |
| Parks progress tracking | ❌ No | ✅ Yes (awards, states) | LOW |
| Duplicate park warning | ❌ No | ⚠️ Some tools | LOW |
| **ADIF Support** |
| General ADIF import/export | ✅ Yes | ✅ Yes | N/A |
| POTA-specific fields (MY_SIG) | ❌ No | ✅ Yes | **CRITICAL** |
| POTA-specific fields (SIG) | ❌ No | ✅ Yes | **CRITICAL** |
| POTA ADIF validation | ❌ No | ✅ Yes | **HIGH** |
| POTA file naming convention | ❌ No | ✅ Yes (CALL@PARK-YYYYMMDD.adi) | MEDIUM |
| **Statistics & Analytics** |
| QSO statistics | ✅ Basic | ✅ Extended | N/A |
| POTA-specific stats | ❌ No | ✅ Yes (parks, P2P, etc.) | MEDIUM |
| Activation summary | ❌ No | ✅ Yes | MEDIUM |
| **Integration & Sync** |
| Direct POTA upload | ❌ No | ✅ Yes (some tools) | LOW |
| Real-time spot posting | ❌ No | ⚠️ Manual/external | LOW |
| **Usability** |
| Mobile/field-friendly UI | ⚠️ Desktop-focused | ✅ Optimized | LOW |
| Keyboard shortcuts | ⚠️ Limited | ✅ Extensive | MEDIUM |
| Auto-fill from previous QSOs | ⚠️ Basic | ✅ Smart | LOW |

### Key Findings

#### Critical Gaps
1. **No POTA-specific ADIF fields**: Log4YM does not capture or export MY_SIG/MY_SIG_INFO (activator park) or SIG/SIG_INFO (hunter worked park)
2. **No offline logging**: Activators often operate without internet connectivity
3. **No activator mode**: No streamlined workflow for logging many QSOs during park activation

#### High Priority Gaps
1. **No multi-park support**: Cannot log activations from multiple parks simultaneously
2. **No park-to-park (P2P) tracking**: Cannot identify and mark P2P contacts
3. **No spot filtering**: Cannot filter spots by band, mode, or other criteria
4. **No quick park reference entry**: Typing full park references (e.g., US-4564) is slow

#### Medium Priority Gaps
1. Session management for activations
2. POTA-specific statistics (parks worked, P2P contacts, etc.)
3. Park progress/award tracking
4. POTA ADIF file naming conventions

## Recommended Feature Enhancements

### Phase 1: Core POTA Activator Support (Critical)
**Goal**: Enable Log4YM to be a viable activator logger with proper ADIF export

1. **Add POTA Fields to QSO Model**
   - Add `MY_SIG`, `MY_SIG_INFO` (activator's park reference)
   - Add `SIG`, `SIG_INFO` (contacted station's park reference for P2P)
   - Store in Qso model and BSON document

2. **Update ADIF Import/Export**
   - Parse POTA fields from imported ADIF files
   - Export POTA fields in ADIF (MY_SIG, MY_SIG_INFO, SIG, SIG_INFO)
   - Implement POTA ADIF file naming: `CALLSIGN@PARK-YYYYMMDD.adi`

3. **Add POTA Mode to QSO Entry**
   - Add "POTA Activator Mode" toggle in settings or QSO entry
   - When enabled:
     - Show park reference input field (MY_SIG_INFO)
     - Auto-fill MY_SIG as "POTA"
     - Detect P2P contacts from spot data or manual entry
   - Park reference autocomplete/typeahead from POTA API

4. **Offline Logging Support**
   - Allow QSO entry when backend is offline
   - Queue QSOs in browser localStorage
   - Sync to backend when connection restored
   - Visual indicator of offline mode and pending sync count

### Phase 2: Enhanced Activator Features (High Priority)
**Goal**: Streamline the activator experience for efficiency

5. **Multi-Park Activation Support**
   - Allow user to activate from multiple parks in one session
   - Quick park switcher in QSO entry
   - Associate each QSO with the active park

6. **Park-to-Park (P2P) Detection**
   - Auto-detect P2P contacts from POTA spot data
   - Visual indicator when logging P2P contact
   - P2P badge in QSO history
   - Auto-fill SIG/SIG_INFO when detecting P2P

7. **POTA Spot Filtering**
   - Add band filter dropdown to POTA plugin
   - Add mode filter dropdown to POTA plugin
   - Add text search for park reference or callsign
   - Remember filter preferences

8. **Quick Reference Entry**
   - Recent parks dropdown
   - Nearby parks based on station location
   - Park reference validation against POTA API

### Phase 3: Statistics & Hunter Support (Medium Priority)
**Goal**: Provide insights and support hunter workflows

9. **POTA Statistics Panel**
   - Total activations (unique parks activated)
   - Total parks worked as hunter
   - P2P contact count
   - Parks by entity/state breakdown
   - QSO count by park reference

10. **Hunter Mode**
    - Simplified QSO entry for hunters
    - Track worked parks (SIG_INFO)
    - Duplicate park warning
    - Parks worked progress by entity

11. **Activation Session Management**
    - Create/end activation session
    - Associate QSOs with session
    - Session summary (QSO count, unique calls, bands, modes)
    - Session export to ADIF with proper naming

### Phase 4: Advanced Features (Low Priority)
**Goal**: Match or exceed competitor features

12. **POTA Integration**
    - Direct log upload to POTA (requires API key/auth)
    - Self-spotting from Log4YM
    - Fetch park details from POTA API (location, entity, etc.)

13. **Awards Tracking**
    - Track progress toward POTA awards
    - Activator awards (parks activated, P2P, entities)
    - Hunter awards (parks worked, entities)
    - Visual progress bars and badges

14. **Mobile/Field Optimizations**
    - Larger touch targets for field use
    - Simplified keyboard-only logging workflow
    - Dark mode optimizations for field visibility
    - PWA enhancements for offline use

## Implementation Plan

### Phase 1: Core POTA Activator Support (2-3 weeks)
**Deliverables**: POTA ADIF fields, export, basic activator mode, offline logging

#### Week 1
- [ ] Update `Qso` model with POTA fields (MY_SIG, MY_SIG_INFO, SIG, SIG_INFO)
- [ ] Update `AdifService` to parse and export POTA fields
- [ ] Add database migration for new fields
- [ ] Write unit tests for ADIF POTA field handling

#### Week 2
- [ ] Add POTA activator mode UI to QSO entry form
- [ ] Implement park reference input with validation
- [ ] Add P2P detection logic from spot data
- [ ] Update QSO entry form to show/hide POTA fields based on mode

#### Week 3
- [ ] Implement offline logging with localStorage queue
- [ ] Add sync indicator and pending QSO count
- [ ] Add POTA ADIF file naming on export
- [ ] Integration testing and bug fixes

### Phase 2: Enhanced Activator Features (2-3 weeks)
**Deliverables**: Multi-park, P2P features, spot filtering, quick entry

#### Week 4
- [ ] Multi-park activation support (park switcher)
- [ ] Enhanced P2P detection and auto-fill
- [ ] P2P visual indicators in history
- [ ] POTA spot filtering (band, mode, search)

#### Week 5
- [ ] Park reference autocomplete from POTA API
- [ ] Recent parks list
- [ ] Nearby parks based on station coordinates
- [ ] Park reference validation

### Phase 3: Statistics & Hunter Support (2 weeks)
**Deliverables**: POTA statistics, hunter mode, session management

#### Week 6-7
- [ ] POTA statistics panel/plugin
- [ ] Hunter mode implementation
- [ ] Activation session management
- [ ] Session-based ADIF export

### Phase 4: Advanced Features (Future / As Needed)
**Deliverables**: Direct POTA integration, awards tracking, mobile optimizations

- To be scheduled based on user demand and feedback

## Technical Considerations

### Database Schema Changes
- Add POTA-specific fields to Qso model (backward compatible)
- Use BsonDocument AdifExtra for extensibility
- Index on MY_SIG_INFO and SIG_INFO for statistics queries

### API Design
- Add `/api/pota/parks` endpoint for park lookup/autocomplete
- Add `/api/pota/statistics` endpoint for POTA-specific stats
- Extend `/api/qsos` to support POTA field filtering

### Offline Storage Strategy
- Use IndexedDB (via Dexie.js or similar) for robust offline storage
- Store pending QSOs with conflict resolution on sync
- Implement retry logic for failed syncs

### UI/UX Considerations
- Keep POTA features optional (don't clutter non-POTA users)
- Use progressive disclosure (show POTA fields only when relevant)
- Maintain consistent design with existing Log4YM UI

## Success Metrics

1. **Adoption**: Number of POTA QSOs logged per month
2. **ADIF Compliance**: Successfully upload exported ADIF to POTA website
3. **User Feedback**: Positive reviews from POTA activators
4. **Feature Parity**: Match 80%+ of Ham2K/HAMRS POTA features
5. **Performance**: Offline logging and sync works reliably in field conditions

## Risks & Mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| POTA API changes | HIGH | Monitor POTA API, implement graceful degradation |
| Offline sync conflicts | MEDIUM | Implement timestamp-based conflict resolution |
| Complexity for casual users | MEDIUM | Use progressive disclosure, keep POTA features optional |
| Mobile performance | LOW | Test on low-end devices, optimize bundle size |

## References

- [POTA ADIF Technical Reference](https://docs.pota.app/docs/activator_reference/ADIF_for_POTA_reference.html)
- [POTA Activator Guide](https://docs.pota.app/docs/activator_reference/activator_guide-english.html)
- [Ham2K Portable Logger](https://github.com/ham2k) - Reference implementation
- [HAMRS](https://hamrs.app/) - Popular POTA logger
- Log4YM Current Implementation:
  - `/src/Log4YM.Web/src/plugins/POTAPlugin.tsx` - Spot display
  - `/src/Log4YM.Server/Controllers/PotaController.cs` - POTA API proxy
  - `/src/Log4YM.Server/Services/AdifService.cs` - ADIF import/export
  - `/src/Log4YM.Contracts/Models/Qso.cs` - QSO data model

## Appendix: POTA ADIF Field Specifications

### Required Fields for POTA Activator Logs
- `STATION_CALLSIGN`: Activator's callsign
- `OPERATOR`: Operator callsign (can be same as station)
- `QSO_DATE`: Date in YYYYMMDD format
- `TIME_ON`: Time in HHMM or HHMMSS format
- `BAND`: Operating band
- `MODE` or `SUBMODE`: Operating mode
- `CALL`: Contacted station's callsign
- `MY_SIG`: Must be "POTA"
- `MY_SIG_INFO`: Park reference (e.g., "US-4564")

### Optional Fields for Park-to-Park
- `SIG`: "POTA" (if contacted station is also activating)
- `SIG_INFO`: Contacted station's park reference

### File Naming Convention
- Format: `CALLSIGN@PARK-YYYYMMDD.adi`
- Example: `W1ABC@US-4564-20260217.adi`
- One park per file (multi-park activations should create separate files)

## Next Steps

1. **Community Feedback**: Share this plan with Log4YM users and POTA community for input
2. **Prototype**: Build a minimal Phase 1 prototype to validate approach
3. **User Testing**: Recruit POTA activators for early testing
4. **Iterate**: Refine based on feedback before Phase 2

---

**Document Version**: 1.0
**Last Updated**: 2026-02-17
**Owner**: Log4YM Development Team
