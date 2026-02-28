# Product Requirements Document: Statistics and Awards Tab

## Document Information
- **Version:** 1.0
- **Date:** 2026-02-28
- **Author:** Claude Code
- **Status:** Draft for Review

## Executive Summary

This PRD defines the requirements for a comprehensive Statistics and Awards tracking system in Log4YM. Based on research of industry leaders (Log4OM, QLog, N1MM+, DXKeeper), this feature will provide users with detailed views of their amateur radio achievements, progress toward major awards programs, and filtering/export capabilities.

The implementation follows a phased approach, starting with DXCC as the highest priority, then expanding to other major awards programs.

---

## 1. Problem Statement

### 1.1 User Need

Amateur radio operators need a centralized location to:
- Track progress toward major awards programs (DXCC, WAS, WAZ, VUCC, IOTA, POTA, SOTA)
- View worked/confirmed status by band and mode
- Identify missing entities, states, zones, or grids
- Filter and sort statistics data for analysis
- Export data for award applications and external tools

### 1.2 Current State

Log4YM currently provides:
- Basic statistics endpoint (`/api/qsos/statistics`) with aggregate counts
- "New DXCC" and "New Band" detection in SpotStatusService
- Individual QSO filtering in LogHistoryPlugin
- POTA spots display in dedicated plugin

However, there is **no comprehensive view** for tracking award progress or analyzing worked/confirmed status across bands and modes.

### 1.3 Gap Analysis

**Missing Capabilities:**
- No dedicated statistics/awards tab
- No DXCC matrix view (entities × bands)
- No filtering by confirmation status (worked vs confirmed)
- No visual progress indicators for awards
- No export functionality for award reports
- Limited access to POTA, SOTA, IOTA data in AdifExtra field

---

## 2. Goals and Success Metrics

### 2.1 Goals

1. **Primary Goal:** Enable users to track DXCC progress with worked/confirmed status by band and mode
2. **Secondary Goal:** Provide framework for other awards (VUCC, IOTA, POTA, SOTA)
3. **Tertiary Goal:** Enable filtering, sorting, and export for statistical analysis

### 2.2 Success Metrics

- User adoption: >40% of active users open Statistics tab at least once per month
- Engagement: Statistics tab ranks in top 5 most-used panels
- Data quality: <5% user reports of incorrect DXCC counts
- Performance: Statistics calculations complete in <2 seconds for 10,000 QSOs

### 2.3 Non-Goals (Out of Scope for Phase 1)

- Automatic LoTW/eQSL synchronization (future feature)
- Geographic visualization on maps (future feature)
- Custom user-defined awards (future feature)
- Real-time award tracking during QSO entry (future feature)
- Mobile-optimized responsive views (future phase)

---

## 3. User Stories and Use Cases

### 3.1 Primary User Stories

**US-1: DXCC Progress Tracking**
> "As an amateur radio operator, I want to see which DXCC entities I've worked and confirmed on each band, so I can identify gaps and plan my operating strategy."

**US-2: Missing Entities Identification**
> "As a DXer, I want to filter my DXCC statistics to show only unworked or unconfirmed entities, so I can focus my efforts on needed countries."

**US-3: Band-Specific Progress**
> "As a single-band enthusiast, I want to view my DXCC progress on 20m separately from other bands, so I can track my specialized achievement."

**US-4: Award Application Export**
> "As an operator preparing an award application, I want to export my statistics to CSV format, so I can submit it to the ARRL or other organizations."

**US-5: VUCC Grid Tracking**
> "As a VHF operator, I want to see how many unique grids I've worked on 6m, 2m, and 70cm, so I can track my progress toward VUCC awards."

**US-6: POTA/SOTA Summary**
> "As a POTA/SOTA participant, I want to see how many unique parks and summits I've activated or hunted, so I can track my certificate progress."

### 3.2 Use Case Scenarios

**Scenario 1: Weekend DXer Reviews Progress**
1. User opens Log4YM after a weekend of operating
2. Clicks on "Statistics" tab in panel picker
3. Views DXCC matrix showing worked/confirmed entities by band
4. Filters to show only "Worked but not confirmed" to prioritize QSL requests
5. Exports list of needed confirmations to CSV

**Scenario 2: Contest Operator Analyzes Band Performance**
1. User completes a major contest
2. Opens Statistics tab
3. Filters by date range (contest weekend)
4. Views QSOs by band and mode
5. Identifies which bands were most productive
6. Exports statistics for club newsletter

**Scenario 3: VHF Operator Tracks VUCC Progress**
1. User opens Statistics tab
2. Selects "VUCC Grids" sub-tab
3. Views grid count for 2m band
4. Identifies missing grids in local region
5. Plans portable operation to work new grids

---

## 4. Functional Requirements

### 4.1 Phase 1: DXCC Statistics (Priority 1)

#### 4.1.1 DXCC Matrix View

**FR-DXCC-001: Entity-by-Band Matrix Display**
- Display DXCC entities in rows
- Display bands in columns (160m, 80m, 60m, 40m, 30m, 20m, 17m, 15m, 12m, 10m, 6m, 2m, 70cm)
- Each cell shows worked/confirmed status for that entity-band combination
- Color coding:
  - Green: Confirmed (via LoTW, paper QSL, or eQSL)
  - Yellow/Orange: Worked but not confirmed
  - Gray/Empty: Not worked
  - Hover shows QSO count and confirmation details

**FR-DXCC-002: Summary Statistics**
- Total unique entities worked (overall)
- Total unique entities confirmed (overall)
- Entities worked by band
- Entities confirmed by band
- DXCC Challenge progress (sum of all band-entities)
- Progress bars for major milestones (100, 200, 300 entities)

**FR-DXCC-003: Sorting and Filtering**
- Sort entities alphabetically by country name
- Sort entities by continent
- Sort entities by confirmed count (most to least)
- Filter by continent (AF, AS, EU, NA, OC, SA)
- Filter by confirmation status:
  - "All" (default)
  - "Worked" (at least one QSO)
  - "Confirmed" (at least one confirmation)
  - "Worked but not confirmed" (QSOs but no confirmations)
  - "Not worked" (zero QSOs)
- Filter by band (show only specific band)
- Filter by mode (CW, Phone, Digital, Mixed)

**FR-DXCC-004: Entity Detail View**
- Click on entity name to open detail modal
- Show all QSOs with that entity
- Group by band and mode
- Show confirmation status per QSO
- Show first QSO date and most recent QSO date
- Show QSL manager information (if available)

#### 4.1.2 Backend API Endpoints

**FR-API-001: DXCC Statistics Endpoint**
- `GET /api/statistics/dxcc`
- Query parameters:
  - `band` (optional): Filter by specific band
  - `mode` (optional): Filter by mode
  - `fromDate` (optional): Date range filter
  - `toDate` (optional): Date range filter
- Response: `DxccStatistics` DTO
  ```json
  {
    "totalEntities": 285,
    "confirmedEntities": 178,
    "entitiesWorked": [
      {
        "dxccCode": 1,
        "entityName": "Canada",
        "continent": "NA",
        "bandStatus": {
          "20m": { "worked": true, "confirmed": true, "qsoCount": 15 },
          "40m": { "worked": true, "confirmed": false, "qsoCount": 3 },
          "80m": { "worked": false, "confirmed": false, "qsoCount": 0 }
        },
        "firstWorked": "2023-01-15T12:00:00Z",
        "lastWorked": "2026-02-15T18:30:00Z"
      }
    ],
    "bandSummary": {
      "20m": { "worked": 220, "confirmed": 150 },
      "40m": { "worked": 180, "confirmed": 95 }
    }
  }
  ```

**FR-API-002: Entity Detail Endpoint**
- `GET /api/statistics/dxcc/{dxccCode}`
- Response: List of QSOs with that entity, grouped by band/mode
- Includes confirmation status per QSO

### 4.2 Phase 2: Other Major Awards (Priority 2)

#### 4.2.1 VUCC (VHF/UHF Century Club)

**FR-VUCC-001: Grid Square Statistics**
- Display unique grids worked by band
- Separate counts for 6m, 2m, 70cm, and other VHF/UHF bands
- Progress toward award thresholds (100 grids for 6m/2m, 50 for 70cm)
- Grid detail view showing QSOs per grid

**FR-VUCC-002: Grid Visualization**
- Optional: 2D map showing worked grids
- Color coding for grid density (QSO count per grid)
- Export grid list for award application

#### 4.2.2 IOTA (Islands on the Air)

**FR-IOTA-001: Island Group Statistics**
- Display unique IOTA groups worked/confirmed
- Group by continent (EU, NA, SA, AF, AS, OC)
- Progress toward milestone levels
- Detail view showing QSOs per island group

#### 4.2.3 POTA (Parks on the Air)

**FR-POTA-001: Park Statistics**
- Display unique parks activated (as operator)
- Display unique parks hunted (as chaser)
- Total QSOs per park
- Progress toward certificate levels (10, 20, 30+ parks)
- Group by state/region

**FR-POTA-002: POTA Data Extraction**
- Extract POTA references from `AdifExtra["pota_ref"]`
- Handle comma-separated multiple park activations
- Normalize park reference format

#### 4.2.4 SOTA (Summits on the Air)

**FR-SOTA-001: Summit Statistics**
- Display unique summits activated
- Display unique summits chased
- Total QSOs per summit
- Point accumulation (if available in ADIF)
- Group by association/region

### 4.3 Phase 3: Advanced Features (Priority 3)

#### 4.3.1 Export Functionality

**FR-EXPORT-001: CSV Export**
- Export statistics to CSV format
- Columns: Entity/State/Zone, Band, Worked Count, Confirmed Count, First QSO, Last QSO
- Option to include all QSO details

**FR-EXPORT-002: ADIF Export (Filtered)**
- Export filtered QSOs to ADIF format
- Useful for award applications requiring specific bands/modes
- Preserve all original ADIF fields

**FR-EXPORT-003: PDF Report**
- Generate printable PDF summary report
- Include summary statistics, charts, and tables
- Formatted for professional appearance

#### 4.3.2 Confirmation Status Tracking

**FR-CONFIRM-001: QSL Card Tracking**
- Track paper QSL card confirmations
- Manual entry for received cards
- Status: Requested, Received, Confirmed

**FR-CONFIRM-002: LoTW/eQSL Sync**
- Background job to sync confirmation status from LoTW
- Update QSO confirmation flags in database
- Display sync timestamp and status

#### 4.3.3 Advanced Filtering

**FR-FILTER-001: Custom Filter Builder**
- Boolean operators (AND, OR, NOT)
- Multi-select for bands and modes
- Date range presets (Last 7 days, Last month, This year, etc.)
- Save custom filters for reuse

**FR-FILTER-002: "Needed" Alerts**
- Highlight entities/states/zones that are close to milestones
- Example: "Work 3 more entities on 40m to reach 100"
- Visual badges for critical QSOs

---

## 5. Non-Functional Requirements

### 5.1 Performance

**NFR-PERF-001: Fast Calculations**
- Statistics calculations must complete in <2 seconds for logs up to 10,000 QSOs
- Use database aggregation (MongoDB/LiteDB) for efficiency
- Cache frequently accessed statistics

**NFR-PERF-002: Responsive UI**
- Statistics tab renders in <1 second
- Filtering and sorting apply in <500ms
- Smooth scrolling for large entity lists (virtualized rendering)

### 5.2 Usability

**NFR-UX-001: Intuitive Navigation**
- Statistics tab accessible from panel picker
- Clear sub-navigation for different awards (DXCC, VUCC, IOTA, etc.)
- Breadcrumb navigation for detail views

**NFR-UX-002: Visual Design**
- Consistent with Log4YM color scheme and typography
- Color-blind friendly color palette for status indicators
- Responsive layout adapts to panel size in FlexLayout

**NFR-UX-003: Accessibility**
- Keyboard navigation support
- Screen reader compatible
- WCAG 2.1 AA compliance

### 5.3 Data Integrity

**NFR-DATA-001: Accurate Counts**
- DXCC entity mapping must match ARRL official list
- Handle entity changes and deletions (e.g., deleted entities still count for Honor Roll)
- Validate grid squares against Maidenhead format

**NFR-DATA-002: Confirmation Rules**
- Only LoTW confirmations count for DXCC (per ARRL rules)
- Paper QSL and eQSL tracked separately
- Mode-specific confirmations (CW vs Phone vs Digital)

### 5.4 Compatibility

**NFR-COMPAT-001: Database Support**
- Works with MongoDB (cloud sync)
- Works with LiteDB (offline mode)
- Consistent behavior across both backends

**NFR-COMPAT-002: Browser Support**
- Chromium-based browsers (Electron)
- Modern browser features (ES2020+, CSS Grid)

---

## 6. Technical Architecture

### 6.1 Backend Components

#### 6.1.1 New Controllers

**StatisticsController** (`/src/Log4YM.Server/Controllers/StatisticsController.cs`)
- `GET /api/statistics/dxcc` - DXCC statistics
- `GET /api/statistics/vucc` - VUCC grid statistics
- `GET /api/statistics/iota` - IOTA island statistics
- `GET /api/statistics/pota` - POTA park statistics
- `GET /api/statistics/sota` - SOTA summit statistics
- `GET /api/statistics/summary` - Overall summary dashboard

#### 6.1.2 New Services

**AwardsService** (`/src/Log4YM.Server/Services/AwardsService.cs`)
- Encapsulates business logic for award calculations
- Methods:
  - `GetDxccStatisticsAsync(filters)` → DxccStatistics
  - `GetVuccStatisticsAsync(filters)` → VuccStatistics
  - `GetIotaStatisticsAsync(filters)` → IotaStatistics
  - `GetPotaStatisticsAsync(filters)` → PotaStatistics
  - `GetSotaStatisticsAsync(filters)` → SotaStatistics

**ConfirmationService** (`/src/Log4YM.Server/Services/ConfirmationService.cs`)
- Manages QSL confirmation status
- Methods:
  - `UpdateQslStatus(qsoId, status)` → Task
  - `SyncLotwConfirmations()` → Task
  - `GetConfirmationStatus(qsoId)` → QslStatus

#### 6.1.3 Data Models (Contracts)

**DxccStatistics** (`/src/Log4YM.Contracts/Api/StatisticsDto.cs`)
```csharp
public record DxccStatistics(
    int TotalEntitiesWorked,
    int TotalEntitiesConfirmed,
    List<DxccEntityStatus> Entities,
    Dictionary<string, BandSummary> BandSummaries
);

public record DxccEntityStatus(
    int DxccCode,
    string EntityName,
    string Continent,
    Dictionary<string, BandStatus> BandStatus,
    DateTime? FirstWorked,
    DateTime? LastWorked
);

public record BandStatus(
    bool Worked,
    bool Confirmed,
    int QsoCount,
    DateTime? FirstQso,
    DateTime? LastQso
);

public record BandSummary(
    int EntitiesWorked,
    int EntitiesConfirmed
);
```

**VuccStatistics, IotaStatistics, PotaStatistics, SotaStatistics** (similar structure)

#### 6.1.4 Repository Extensions

**QsoRepository Extensions**
- Add aggregation queries for statistics
- Optimize for group-by operations (entity, band, mode)
- Index on `Dxcc`, `Band`, `Mode`, `Country` fields for performance

### 6.2 Frontend Components

#### 6.2.1 New Plugin: StatisticsPlugin

**File:** `/src/Log4YM.Web/src/plugins/StatisticsPlugin.tsx`

**Structure:**
```tsx
export const StatisticsPlugin: React.FC = () => {
  const [activeTab, setActiveTab] = useState<'dxcc' | 'vucc' | 'iota' | 'pota' | 'sota'>('dxcc');

  return (
    <div className="statistics-plugin">
      <TabNavigation activeTab={activeTab} onChange={setActiveTab} />

      {activeTab === 'dxcc' && <DxccStatistics />}
      {activeTab === 'vucc' && <VuccStatistics />}
      {activeTab === 'iota' && <IotaStatistics />}
      {activeTab === 'pota' && <PotaStatistics />}
      {activeTab === 'sota' && <SotaStatistics />}
    </div>
  );
};
```

**Sub-Components:**
- `DxccStatistics.tsx` - DXCC matrix and filtering
- `VuccStatistics.tsx` - Grid square tracking
- `IotaStatistics.tsx` - Island group tracking
- `PotaStatistics.tsx` - Park tracking
- `SotaStatistics.tsx` - Summit tracking
- `EntityDetailModal.tsx` - Detail view for specific entity
- `StatisticsFilters.tsx` - Reusable filter panel
- `StatisticsExport.tsx` - Export dialog

#### 6.2.2 API Client Extensions

**File:** `/src/Log4YM.Web/src/api/client.ts`

Add methods:
```typescript
export const api = {
  // ... existing methods ...

  // Statistics
  getDxccStatistics: (filters?: StatisticsFilters) =>
    client.get<DxccStatistics>('/api/statistics/dxcc', { params: filters }),

  getVuccStatistics: (filters?: StatisticsFilters) =>
    client.get<VuccStatistics>('/api/statistics/vucc', { params: filters }),

  // ... other award endpoints
};
```

#### 6.2.3 State Management

**Zustand Store:** `/src/Log4YM.Web/src/store/statisticsStore.ts`
- Cache active filters
- Cache fetched statistics
- Track selected entity/detail view
- Persist user preferences (default tab, favorite filters)

### 6.3 Integration Points

#### 6.3.1 FlexLayout Integration

- Register StatisticsPlugin in `PLUGINS` record in `App.tsx`
- Add to panel picker with appropriate metadata:
  ```typescript
  {
    id: 'statistics',
    name: 'Statistics',
    description: 'DXCC, VUCC, IOTA, POTA awards tracking',
    component: 'statistics',
    icon: '📊',
    category: 'Information',
    singleton: true
  }
  ```

#### 6.3.2 SignalR Hub Integration

- Listen for QSO changes via LogHub
- Invalidate statistics cache when QSOs are added/updated/deleted
- Real-time update of statistics without manual refresh

#### 6.3.3 Existing SpotStatusService

- Reuse logic for "New DXCC" and "New Band" detection
- Consider refactoring into AwardsService for centralization

---

## 7. Data Sources and Validation

### 7.1 DXCC Entity List

**Source:** ARRL DXCC Current List (340 entities as of 2026)
- Maintained as JSON or CSV resource file
- Updated periodically from ARRL website
- Includes: DXCC code, entity name, continent, prefix patterns

**Validation:**
- Cross-reference against `CtyService` (cty.dat)
- Handle entity additions/deletions
- Historical entities (deleted) preserved for Honor Roll tracking

### 7.2 Grid Square Validation

**Format:** Maidenhead Locator System (e.g., FN20, JO22)
- 2-character (field): AA-RR
- 4-character (square): AA00-RR99
- 6-character (subsquare): AA00aa-RR99xx
- 8-character (extended): AA00aa00-RR99xx99

**Validation Rules:**
- First pair: Letters A-R
- Second pair: Digits 0-9
- Third pair: Letters a-x (lowercase)
- Case-insensitive storage, uppercase display

### 7.3 IOTA References

**Format:** CC-NNN (Continent Code + Number)
- EU-001 to EU-191 (Europe)
- NA-001 to NA-245 (North America)
- SA-001 to SA-101 (South America)
- AF-001 to AF-100 (Africa)
- AS-001 to AS-169 (Asia)
- OC-001 to OC-284 (Oceania)

**Source:** IOTA Directory (https://www.iota-world.org)
- Updated annually
- Maintained as reference file

### 7.4 POTA/SOTA References

**POTA Format:** K-1234 (Country + Number)
- US parks: K-0001 to K-9999
- Canadian parks: VE-0001 to VE-9999
- International: Varies by country

**SOTA Format:** Association/Region Code + Summit Number
- Example: W6/NC-001 (California, North Coast region, summit 1)

**Sources:**
- POTA: https://pota.app API
- SOTA: https://www.sotadata.org.uk API
- Both provide JSON endpoints for reference data

---

## 8. Implementation Phases

### Phase 1: Foundation (Weeks 1-2)

**Backend:**
- [ ] Create StatisticsController with DXCC endpoint
- [ ] Create AwardsService with DXCC calculations
- [ ] Define DxccStatistics DTO in Contracts
- [ ] Add repository methods for DXCC aggregation
- [ ] Unit tests for AwardsService

**Frontend:**
- [ ] Create StatisticsPlugin shell component
- [ ] Create DxccStatistics component with basic table
- [ ] Integrate with API client
- [ ] Add to FlexLayout plugin registry
- [ ] Basic styling and layout

**Deliverable:** Working DXCC statistics tab with basic entity list

### Phase 2: DXCC Matrix and Filtering (Weeks 3-4)

**Backend:**
- [ ] Enhance DXCC endpoint with filter parameters
- [ ] Add entity detail endpoint
- [ ] Optimize database queries with indexes

**Frontend:**
- [ ] Implement DXCC matrix view (entities × bands)
- [ ] Add color coding for worked/confirmed status
- [ ] Implement filters (continent, band, mode, status)
- [ ] Add sorting options
- [ ] Create EntityDetailModal component
- [ ] Add summary statistics display

**Deliverable:** Full-featured DXCC statistics with filtering and detail views

### Phase 3: Other Awards (Weeks 5-6)

**Backend:**
- [ ] Add VUCC endpoint and service methods
- [ ] Add IOTA endpoint and service methods
- [ ] Add POTA endpoint and service methods
- [ ] Add SOTA endpoint and service methods
- [ ] Extract POTA/SOTA references from AdifExtra

**Frontend:**
- [ ] Create VuccStatistics component
- [ ] Create IotaStatistics component
- [ ] Create PotaStatistics component
- [ ] Create SotaStatistics component
- [ ] Add tab navigation between awards
- [ ] Consistent styling across all tabs

**Deliverable:** Complete award tracking for DXCC, VUCC, IOTA, POTA, SOTA

### Phase 4: Export and Advanced Features (Weeks 7-8)

**Backend:**
- [ ] Add export endpoints (CSV, ADIF, PDF)
- [ ] Implement ConfirmationService for QSL tracking
- [ ] Add background job for LoTW sync (optional)

**Frontend:**
- [ ] Create StatisticsExport component
- [ ] Implement CSV export
- [ ] Implement ADIF export (filtered)
- [ ] Add "Needed" alerts and highlighting
- [ ] Save user filter preferences
- [ ] Add progress bars and visual indicators

**Deliverable:** Complete statistics system with export and confirmation tracking

---

## 9. Testing Strategy

### 9.1 Unit Tests

**Backend:**
- AwardsService methods with mock QSO data
- DXCC aggregation logic
- Grid square validation
- IOTA/POTA/SOTA reference parsing

**Frontend:**
- Component rendering tests (React Testing Library)
- Filter application logic
- Data transformation utilities
- Export functionality

### 9.2 Integration Tests

**Backend:**
- StatisticsController endpoints with test database
- Database aggregation queries (MongoDB and LiteDB)
- SignalR hub integration

**Frontend:**
- API client integration
- Full user workflows (filter, view details, export)

### 9.3 Manual Testing

- Visual verification of matrix display
- Color coding accuracy
- Filter combinations
- Export file format validation
- Performance with large logs (10,000+ QSOs)

---

## 10. Success Criteria

### 10.1 Definition of Done

Phase 1 is complete when:
- [ ] DXCC statistics endpoint returns correct data
- [ ] StatisticsPlugin displays DXCC matrix
- [ ] Entities show worked/confirmed status
- [ ] Basic filtering works (band, continent)
- [ ] Unit tests pass (>80% coverage)
- [ ] Manual testing confirms accuracy with sample log

Phase 2 is complete when:
- [ ] All filters implemented (status, mode, date)
- [ ] Sorting works (alphabetical, continent, count)
- [ ] Entity detail modal shows QSO list
- [ ] Summary statistics display correctly
- [ ] Color coding is clear and intuitive

Phase 3 is complete when:
- [ ] VUCC, IOTA, POTA, SOTA tabs functional
- [ ] Grid square counting works correctly
- [ ] POTA/SOTA references extracted from AdifExtra
- [ ] Tab navigation between awards seamless

Phase 4 is complete when:
- [ ] CSV export generates valid files
- [ ] ADIF export preserves all fields
- [ ] User preferences persist across sessions
- [ ] "Needed" alerts highlight critical QSOs

### 10.2 Acceptance Criteria

The Statistics and Awards Tab is considered successful when:
1. User can view DXCC progress with worked/confirmed status
2. User can filter by band, mode, continent, and status
3. User can export statistics to CSV for external use
4. Calculations are accurate (verified against known logs)
5. Performance meets <2 second load time for 10,000 QSOs
6. Visual design is consistent with Log4YM theme
7. User feedback is positive (>80% satisfaction in surveys)

---

## 11. Open Questions and Risks

### 11.1 Open Questions

1. **LoTW Integration:** Should we implement automatic LoTW sync in Phase 4, or defer to future release?
   - **Recommendation:** Defer to separate feature (requires LoTW API credentials and complex authentication)

2. **Confirmation Priority:** Which confirmation method takes precedence: LoTW > eQSL > Paper QSL?
   - **Recommendation:** Follow ARRL rules: LoTW for DXCC, all methods for other awards

3. **Historical Data:** Should deleted DXCC entities (e.g., USSR, Czechoslovakia) be shown?
   - **Recommendation:** Yes, with "(Deleted)" label, for Honor Roll tracking

4. **Mode Grouping:** Should we group modes (CW, Phone, Digital) or show all specific modes?
   - **Recommendation:** Default to grouped, with option to expand to specific modes

5. **Grid Square Precision:** Should we count 4-character grids, 6-character, or both?
   - **Recommendation:** 4-character (standard for VUCC), with option to show 6-character detail

### 11.2 Risks and Mitigation

| Risk | Impact | Probability | Mitigation |
|------|--------|-------------|------------|
| **Performance issues with large logs** | High | Medium | Use database aggregation, caching, and virtualized rendering |
| **DXCC entity mapping errors** | High | Low | Validate against multiple sources (cty.dat, ARRL list), unit tests |
| **User confusion with multiple tabs** | Medium | Medium | Clear navigation, help text, tooltips, consistent design |
| **Data extraction from AdifExtra** | Medium | Medium | Robust parsing, handle missing/malformed data gracefully |
| **Color-blind accessibility** | Medium | Low | Use patterns/icons in addition to colors, test with simulators |
| **Database compatibility issues** | High | Low | Consistent abstraction layer, test with both MongoDB and LiteDB |

---

## 12. Future Enhancements (Post-Phase 4)

### 12.1 Advanced Visualizations

- **Geographic Map:** Show worked entities on world map
- **Heat Maps:** QSO density by hour/day
- **Charts:** Progress over time (line charts), band distribution (pie charts)
- **Grid Map:** 2D Maidenhead grid overlay

### 12.2 Real-Time Features

- **Live Award Tracking:** Highlight "New DXCC" during QSO entry
- **Alert Notifications:** Audio/visual alerts for needed entities on DX cluster
- **Progress Notifications:** "You just reached 200 DXCC entities!"

### 12.3 Custom Awards

- **Award Builder:** Define custom awards using any QSO field
- **Club Awards:** Track club-specific achievements
- **Personal Challenges:** "Work all 50 states on 6m"

### 12.4 Social Features

- **Leaderboards:** Compare progress with other Log4YM users
- **Achievement Badges:** Gamification of award milestones
- **Export to Social Media:** Share achievements on Twitter, Facebook

### 12.5 Integration Enhancements

- **LoTW Auto-Sync:** Background job syncs confirmations daily
- **eQSL Integration:** Automatic confirmation updates
- **Clublog Sync:** Cloud-based statistics
- **QRZ Manager Lookup:** Automatic QSL manager information

---

## 13. References and Resources

### 13.1 Industry Research

- **Log4OM V2:** Award tracking system (reference implementation)
- **QLog:** Open-source Qt-based logger with award support
- **N1MM+:** Contest logging with real-time statistics
- **DXKeeper:** Comprehensive award tracking (DXLab Suite)

### 13.2 Award Program Rules

- **ARRL DXCC Rules:** http://www.arrl.org/dxcc-rules
- **VUCC Rules:** http://www.arrl.org/vucc
- **IOTA Programme:** https://www.iota-world.org
- **POTA Rules:** https://parksontheair.com/rules/
- **SOTA Rules:** https://www.sotadata.org.uk/docs

### 13.3 Technical References

- **ADIF Specification:** https://adif.org/
- **Maidenhead Locator System:** https://en.wikipedia.org/wiki/Maidenhead_Locator_System
- **DXCC Entity List:** http://www.arrl.org/dxcc-list
- **CTY.DAT Format:** http://www.country-files.com/

### 13.4 Log4YM Architecture

- **FlexLayout React:** https://github.com/caplin/FlexLayout
- **SignalR:** https://learn.microsoft.com/en-us/aspnet/core/signalr/
- **MongoDB Aggregation:** https://www.mongodb.com/docs/manual/aggregation/
- **React Query:** https://tanstack.com/query/latest

---

## Appendix A: Data Models (Detailed)

### A.1 QSO Model (Existing)

```csharp
public class Qso
{
    public string? Id { get; set; }
    public string Callsign { get; set; }
    public DateTime TimeOn { get; set; }
    public DateTime? TimeOff { get; set; }
    public string Band { get; set; }
    public string Mode { get; set; }
    public int? Dxcc { get; set; }
    public string? Country { get; set; }
    public string? Continent { get; set; }
    public string? Grid { get; set; }
    public StationInfo? Station { get; set; }
    public BsonDocument? AdifExtra { get; set; }

    // Confirmation fields
    public bool LotwQslRcvd { get; set; }
    public DateTime? LotwQslRcvdDate { get; set; }
    public bool EqslQslRcvd { get; set; }
    public DateTime? EqslQslRcvdDate { get; set; }
    public bool QslRcvd { get; set; }  // Paper QSL
    public DateTime? QslRcvdDate { get; set; }
}
```

### A.2 DXCC Statistics Model (New)

```csharp
public record DxccStatistics(
    int TotalEntitiesWorked,
    int TotalEntitiesConfirmed,
    int ChallengeScore,  // Sum of all band-entities
    List<DxccEntityStatus> Entities,
    Dictionary<string, BandSummary> BandSummaries,
    Dictionary<string, int> ModeBreakdown
);

public record DxccEntityStatus(
    int DxccCode,
    string EntityName,
    string Continent,
    string Prefix,
    Dictionary<string, BandStatus> BandStatus,
    DateTime? FirstWorked,
    DateTime? LastWorked,
    int TotalQsos
);

public record BandStatus(
    bool Worked,
    bool Confirmed,
    int QsoCount,
    DateTime? FirstQso,
    DateTime? LastQso,
    ConfirmationType? ConfirmedVia  // LoTW, eQSL, Paper
);

public record BandSummary(
    int EntitiesWorked,
    int EntitiesConfirmed,
    int QsoCount
);

public enum ConfirmationType
{
    LoTW,
    EQsl,
    Paper,
    Clublog
}
```

### A.3 VUCC Statistics Model (New)

```csharp
public record VuccStatistics(
    Dictionary<string, GridBandSummary> BandSummaries,
    List<GridDetail> Grids
);

public record GridBandSummary(
    string Band,
    int UniqueGrids,
    int AwardThreshold,  // 100 for 6m/2m, 50 for 70cm, etc.
    int QsoCount
);

public record GridDetail(
    string Grid,
    string Band,
    int QsoCount,
    DateTime? FirstWorked,
    DateTime? LastWorked
);
```

### A.4 POTA Statistics Model (New)

```csharp
public record PotaStatistics(
    int UniqueParkActivations,
    int UniqueParkHunts,
    int TotalActivationQsos,
    int TotalHuntQsos,
    List<PotaParkDetail> Parks
);

public record PotaParkDetail(
    string ParkReference,  // e.g., "K-0817"
    string ParkName,
    string State,
    PotaActivityType ActivityType,  // Activator or Hunter
    int QsoCount,
    DateTime? FirstQso,
    DateTime? LastQso
);

public enum PotaActivityType
{
    Activator,
    Hunter,
    Both
}
```

---

## Appendix B: Filter Parameters

### B.1 StatisticsFilters DTO

```csharp
public record StatisticsFilters(
    string? Band = null,
    string? Mode = null,
    string? Continent = null,
    ConfirmationStatus? Status = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    string? Operator = null  // For multi-op stations
);

public enum ConfirmationStatus
{
    All,
    Worked,
    Confirmed,
    WorkedNotConfirmed,
    NotWorked
}
```

---

## Appendix C: UI Wireframes (Text Description)

### C.1 DXCC Matrix View

```
┌─────────────────────────────────────────────────────────────────┐
│ STATISTICS                                         [⚙️ Settings] │
├─────────────────────────────────────────────────────────────────┤
│ [DXCC] [VUCC] [IOTA] [POTA] [SOTA]                              │
├─────────────────────────────────────────────────────────────────┤
│ Filters:                                                         │
│  Continent: [All ▼] Band: [All ▼] Mode: [All ▼]                │
│  Status: [All ▼]                              [Reset] [Export]  │
├─────────────────────────────────────────────────────────────────┤
│ Summary:                                                         │
│  Total Entities Worked: 285 | Confirmed: 178 | Challenge: 1,234│
│  Progress: [███████████░░░░░░░░] 178/340 (52%)                 │
├─────────────────────────────────────────────────────────────────┤
│ Entity            │ 160│ 80 │ 40 │ 30 │ 20 │ 17 │ 15 │ 12 │ 10 │
├───────────────────┼────┼────┼────┼────┼────┼────┼────┼────┼────┤
│ Canada (VE)       │ ✓  │ ✓✓ │ ✓✓ │ -  │ ✓✓ │ ✓✓ │ ✓✓ │ -  │ ✓  │
│ United States (K) │ ✓✓ │ ✓✓ │ ✓✓ │ ✓✓ │ ✓✓ │ ✓✓ │ ✓✓ │ ✓✓ │ ✓✓ │
│ Mexico (XE)       │ -  │ ✓  │ ✓✓ │ -  │ ✓✓ │ ✓  │ ✓✓ │ -  │ ✓  │
│ ...               │    │    │    │    │    │    │    │    │    │
└───────────────────────────────────────────────────────────────────┘

Legend:
  ✓✓ = Confirmed (green)
  ✓  = Worked but not confirmed (yellow)
  -  = Not worked (gray)
```

### C.2 Entity Detail Modal

```
┌─────────────────────────────────────────────────────────────────┐
│ CANADA (VE) - DXCC Entity #1                          [✕ Close] │
├─────────────────────────────────────────────────────────────────┤
│ Continent: North America                                        │
│ First Worked: 2023-01-15 12:00 UTC                             │
│ Last Worked: 2026-02-20 18:30 UTC                              │
│ Total QSOs: 47                                                  │
├─────────────────────────────────────────────────────────────────┤
│ QSOs by Band and Mode:                                          │
│                                                                  │
│ 20m (15 QSOs) - Confirmed ✓                                    │
│  • 2023-01-15 12:00 - VE3ABC - SSB - [LoTW ✓]                  │
│  • 2024-03-22 15:30 - VE7XYZ - FT8 - [LoTW ✓]                  │
│  • 2026-02-20 18:30 - VE2DEF - CW - [Not confirmed]            │
│                                                                  │
│ 40m (8 QSOs) - Worked but not confirmed                        │
│  • 2024-11-10 20:00 - VE3GHI - SSB - [Not confirmed]           │
│  • 2025-05-15 22:15 - VE1JKL - CW - [Not confirmed]            │
│                                                                  │
│ [Export QSOs] [View on Map]                                    │
└─────────────────────────────────────────────────────────────────┘
```

---

## Document Revision History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0 | 2026-02-28 | Claude Code | Initial PRD creation |

---

**End of Document**
