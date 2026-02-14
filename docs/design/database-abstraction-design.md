# Database Abstraction & Onboarding Redesign

## Problem Statement

Users install Log4YM but are immediately stuck because they don't know how to connect to MongoDB Atlas. There is no blocking setup wizard on first run - users see the full app with a small red "MongoDB Not Connected" status bar warning and must discover Settings > Database on their own. Many users never get past this point.

## Goals

1. **Zero-friction first run** - App works immediately out of the box with a local database
2. **Optional cloud upgrade** - Users can connect to MongoDB Atlas when ready
3. **Offline-first** - App works without internet connectivity
4. **Clear onboarding** - Guide users through database choice on first launch
5. **Minimal migration effort** - Leverage existing repository interfaces

---

## Architecture Design

### Database Provider Strategy

```
                    ┌─────────────────────┐
                    │   IQsoRepository    │
                    │ ISettingsRepository │
                    │ ILayoutRepository   │  ◄── NEW interfaces for currently
                    │ IPluginRepository   │      direct-access collections
                    │ ISmartUnlinkRepo    │
                    │ ICallsignImageRepo  │
                    └──────────┬──────────┘
                               │
                    ┌──────────┴──────────┐
                    │  IDbContext          │  ◄── NEW: replaces MongoDbContext
                    │  - IsConnected       │
                    │  - DatabaseName      │
                    │  - Provider (enum)   │
                    │  - Initialize()      │
                    │  - Reinitialize()    │
                    └──────────┬──────────┘
                               │
                 ┌─────────────┴─────────────┐
                 │                           │
        ┌────────┴────────┐        ┌────────┴────────┐
        │  LiteDbContext  │        │ MongoDbContext   │
        │  (local file)   │        │ (Atlas/remote)   │
        └────────┬────────┘        └────────┬────────┘
                 │                           │
        ┌────────┴────────┐        ┌────────┴────────┐
        │ LiteDb repos    │        │ Mongo repos     │
        │ (LiteQsoRepo,  │        │ (existing code, │
        │  LiteSettings…) │        │  cleaned up)    │
        └─────────────────┘        └─────────────────┘
```

### Why LiteDB for Local Storage

| Criteria | LiteDB | SQLite | RavenDB |
|----------|--------|--------|---------|
| Document model (matches existing) | Native BSON documents | Relational (mismatch) | Native documents |
| Migration effort | LOW | HIGH (full rewrite) | MEDIUM |
| Zero config | Single .db file | Single file | Spawns server process |
| Schema flexibility (ADIF extras) | `Dictionary<string,BsonValue>` | JSON column workarounds | Native |
| Cross-platform | .NET Standard 2.0 | Yes | Yes |
| License | MIT | Public domain | Commercial |
| Size | ~350KB DLL | ~2MB | ~100MB+ |

**LiteDB wins** because it's the closest analog to MongoDB's document model. The repository implementations can follow nearly identical patterns, and the `BsonExtraElements` gap (used in `Qso.AdifExtra` and `PluginSettings.Settings`) can be handled with `Dictionary<string, BsonValue>` properties.

---

## Implementation Plan

### Phase 1: Repository Abstraction (Backend)

#### 1.1 Create missing repository interfaces

Currently only `IQsoRepository` and `ISettingsRepository` exist. Four collections are accessed directly through `MongoDbContext`. Create interfaces for all:

```csharp
// NEW interfaces needed:
public interface ILayoutRepository
{
    Task<Layout?> GetAsync(string id = "default");
    Task<Layout> UpsertAsync(Layout layout);
}

public interface IPluginSettingsRepository
{
    Task<PluginSettings?> GetAsync(string pluginName);
    Task<PluginSettings> UpsertAsync(PluginSettings settings);
}

public interface ISmartUnlinkRepository
{
    Task<IEnumerable<SmartUnlinkRadioEntity>> GetAllAsync();
    Task<SmartUnlinkRadioEntity> UpsertAsync(SmartUnlinkRadioEntity entity);
    Task<bool> DeleteAsync(string id);
}

public interface ICallsignImageRepository
{
    Task<CallsignMapImage?> GetByCallsignAsync(string callsign);
    Task<CallsignMapImage> UpsertAsync(CallsignMapImage image);
    Task<IEnumerable<CallsignMapImage>> GetRecentAsync(int limit);
}
```

#### 1.2 Create `IDbContext` interface

```csharp
public enum DatabaseProvider
{
    Local,      // LiteDB
    MongoDb     // MongoDB Atlas or self-hosted
}

public interface IDbContext
{
    bool IsConnected { get; }
    string? DatabaseName { get; }
    DatabaseProvider Provider { get; }
    bool TryInitialize();
    Task<bool> ReinitializeAsync(DatabaseConfig config);
}

public class DatabaseConfig
{
    public DatabaseProvider Provider { get; set; }
    // For MongoDB:
    public string? ConnectionString { get; set; }
    public string? MongoDbDatabaseName { get; set; }
    // For LiteDB:
    public string? LocalDbPath { get; set; }  // auto-populated if not set
}
```

#### 1.3 Decouple models from MongoDB BSON attributes

The `Log4YM.Contracts` project currently depends on `MongoDB.Driver` for BSON attributes. This needs decoupling.

**Approach**: Use `System.Text.Json` attributes as the primary serialization contract. Add a MongoDB-specific serialization configuration (class maps) in the MongoDB provider rather than attributes on the models.

```csharp
// BEFORE (in Contracts):
[BsonId]
[BsonRepresentation(BsonType.ObjectId)]
public string Id { get; set; }

[BsonElement("callsign")]
public string Callsign { get; set; }

[BsonExtraElements]
public BsonDocument? AdifExtra { get; set; }

// AFTER (in Contracts - provider-agnostic):
[JsonPropertyName("id")]
public string Id { get; set; } = string.Empty;

[JsonPropertyName("callsign")]
public string Callsign { get; set; } = string.Empty;

[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
public Dictionary<string, object>? AdifExtra { get; set; }
```

MongoDB-specific mapping moves to a `MongoClassMapConfig` in the server project:

```csharp
// In Log4YM.Server/Core/Database/Mongo/MongoClassMapConfig.cs
public static class MongoClassMapConfig
{
    public static void Register()
    {
        BsonClassMap.RegisterClassMap<Qso>(cm =>
        {
            cm.AutoMap();
            cm.MapIdMember(c => c.Id)
              .SetIdGenerator(StringObjectIdGenerator.Instance)
              .SetSerializer(new StringSerializer(BsonType.ObjectId));
            cm.MapExtraElementsMember(c => c.AdifExtra);
            cm.SetElementName(c => c.Callsign, "callsign");
            // ... etc
        });
    }
}
```

**This is the biggest single change** but decouples the entire Contracts project from MongoDB.

#### 1.4 Implement LiteDB repositories

```
src/Log4YM.Server/Core/Database/
├── IDbContext.cs                    # NEW interface
├── DatabaseConfig.cs                # NEW config model
├── Mongo/
│   ├── MongoDbContext.cs            # MOVED (existing, refactored)
│   ├── MongoClassMapConfig.cs       # NEW (BSON mappings)
│   ├── MongoQsoRepository.cs        # RENAMED from QsoRepository
│   ├── MongoSettingsRepository.cs   # RENAMED from SettingsRepository
│   ├── MongoLayoutRepository.cs     # NEW
│   ├── MongoPluginSettingsRepo.cs   # NEW
│   ├── MongoSmartUnlinkRepo.cs      # NEW
│   └── MongoCallsignImageRepo.cs    # NEW
├── LiteDb/
│   ├── LiteDbContext.cs             # NEW
│   ├── LiteQsoRepository.cs         # NEW
│   ├── LiteSettingsRepository.cs    # NEW
│   ├── LiteLayoutRepository.cs      # NEW
│   ├── LitePluginSettingsRepo.cs    # NEW
│   ├── LiteSmartUnlinkRepo.cs       # NEW
│   └── LiteCallsignImageRepo.cs     # NEW
└── DbServiceRegistration.cs         # NEW - DI registration based on config
```

#### 1.5 DI Registration with Provider Selection

```csharp
public static class DbServiceRegistration
{
    public static IServiceCollection AddDatabase(
        this IServiceCollection services,
        DatabaseConfig config)
    {
        switch (config.Provider)
        {
            case DatabaseProvider.Local:
                services.AddSingleton<IDbContext, LiteDbContext>();
                services.AddScoped<IQsoRepository, LiteQsoRepository>();
                services.AddScoped<ISettingsRepository, LiteSettingsRepository>();
                services.AddScoped<ILayoutRepository, LiteLayoutRepository>();
                // ... etc
                break;

            case DatabaseProvider.MongoDb:
                MongoClassMapConfig.Register();
                services.AddSingleton<IDbContext, MongoDbContext>();
                services.AddScoped<IQsoRepository, MongoQsoRepository>();
                services.AddScoped<ISettingsRepository, MongoSettingsRepository>();
                services.AddScoped<ILayoutRepository, MongoLayoutRepository>();
                // ... etc
                break;
        }
        return services;
    }
}
```

#### 1.6 Handle LiteDB-specific challenges

**`BsonExtraElements` → `Dictionary<string, object>`**:
LiteDB natively serializes `Dictionary<string, object>` as a BSON document. Change `Qso.AdifExtra` from `BsonDocument?` to `Dictionary<string, object>?`. Both MongoDB (via class map) and LiteDB handle this naturally.

**Multi-field sorting (`ThenByDescending`)**:
LiteDB only supports single-field `OrderBy`. For the `QsoRepository.GetRecentAsync()` pattern of `SortByDescending(QsoDate).ThenByDescending(TimeOn)`:
- Option A: Sort by `QsoDate` only (TimeOn is within the same day, usually sufficient)
- Option B: Add a computed `SortKey` field combining date+time (e.g., `"2024-01-15T14:30"`) and sort by that single field
- **Recommendation**: Option B - add a `SortTimestamp` property that combines `QsoDate` + `TimeOn` into a single `DateTime`. This benefits both providers.

**Aggregation pipeline (`GetStatisticsAsync`)**:
LiteDB has no aggregation pipeline. Replace with LINQ-to-objects:
```csharp
// LiteDB version
public async Task<QsoStatistics> GetStatisticsAsync()
{
    var all = _collection.FindAll().ToList();
    return new QsoStatistics(
        TotalQsos: all.Count,
        UniqueCallsigns: all.Select(q => q.Callsign).Distinct().Count(),
        // ... etc
        QsosByBand: all.GroupBy(q => q.Band ?? "Unknown")
                      .ToDictionary(g => g.Key, g => g.Count())
    );
}
```
For large datasets (10k+ QSOs), add caching or incremental stats tracking.

---

### Phase 2: First-Run Onboarding (Frontend)

#### 2.1 Activate and Redesign SetupWizard

The `SetupWizard.tsx` component already exists but is never rendered. Redesign it as a **two-choice wizard**:

```
┌──────────────────────────────────────────────────┐
│                                                  │
│     🔶 LOG4YM                                    │
│     Welcome! Let's get you set up.               │
│                                                  │
│  ┌─────────────────────┐  ┌────────────────────┐ │
│  │                     │  │                    │ │
│  │   💾 Local          │  │   ☁️  Cloud         │ │
│  │   Database          │  │   Database         │ │
│  │                     │  │                    │ │
│  │  Works offline      │  │  MongoDB Atlas     │ │
│  │  No setup needed    │  │  Multi-device sync │ │
│  │  Data on this       │  │  Cloud backup      │ │
│  │  computer           │  │  Free tier         │ │
│  │                     │  │                    │ │
│  │  [Get Started]      │  │  [Configure]       │ │
│  └─────────────────────┘  └────────────────────┘ │
│                                                  │
│  You can switch between local and cloud          │
│  at any time in Settings.                        │
│                                                  │
└──────────────────────────────────────────────────┘
```

**Flow for "Local Database"**:
1. User clicks "Get Started"
2. `POST /api/setup/configure` with `{ provider: "local" }`
3. Backend creates LiteDB file at platform-specific path
4. Success animation → main app loads immediately
5. **Total time: ~2 seconds, zero configuration**

**Flow for "Cloud Database"**:
1. User clicks "Configure"
2. Existing MongoDB connection form appears (connection string + database name)
3. Test Connection → Save & Continue
4. Same flow as current `SetupWizard`, already implemented

#### 2.2 Wire SetupWizard into App.tsx

```tsx
// In App.tsx
const { isConfigured, isLoading } = useSetupStore();

// Show setup wizard on first run (blocking)
if (!isLoading && !isConfigured) {
  return <SetupWizard onComplete={() => fetchStatus()} />;
}
```

The `SetupWizard` renders at `z-[200]` (already highest z-index in the app), blocking all interaction until setup is complete.

#### 2.3 Update Settings > Database section

Add a provider switcher to the existing Database settings section:

```
┌─ Database ──────────────────────────────────────┐
│                                                 │
│  Current Provider: [Local ▾]  ← dropdown        │
│                                                 │
│  ── When "Local" selected ──                    │
│  Database File: ~/Library/.../log4ym.db         │
│  Size: 2.4 MB | QSOs: 1,234                    │
│  [Export to ADIF]  [Switch to Cloud →]          │
│                                                 │
│  ── When "Cloud" selected ──                    │
│  Connection String: [mongodb+srv://...]         │
│  Database Name: [Log4YM]                        │
│  Status: ● Connected                            │
│  [Test Connection]  [Save & Reconnect]          │
│  [Switch to Local →]                            │
│                                                 │
│  ⚠️ Switching providers does not migrate data.   │
│  Export your QSOs to ADIF first if needed.       │
│                                                 │
└─────────────────────────────────────────────────┘
```

---

### Phase 3: Backend API Changes

#### 3.1 Update Setup Controller

```
POST /api/setup/configure
{
  "provider": "local" | "mongodb",
  "connectionString": "...",     // only for mongodb
  "databaseName": "..."         // only for mongodb
}

POST /api/setup/test-connection
{
  "provider": "mongodb",
  "connectionString": "...",
  "databaseName": "..."
}

GET /api/health
{
  "databaseConnected": true,      // renamed from mongoDbConnected
  "databaseProvider": "local",    // NEW
  "databaseName": "log4ym.db",
  "signalRConnected": true
}

GET /api/setup/status
{
  "isConfigured": true,
  "provider": "local",
  "databaseName": "log4ym.db"
}
```

#### 3.2 Update UserConfig model

```csharp
public class UserConfig
{
    public DatabaseProvider Provider { get; set; } = DatabaseProvider.Local;
    public string? MongoDbConnectionString { get; set; }
    public string? MongoDbDatabaseName { get; set; }
    public string? LocalDbPath { get; set; }  // null = use default path
    public DateTime? ConfiguredAt { get; set; }
}
```

#### 3.3 Startup flow change

```
App Start
  │
  ├── Read config.json
  │    ├── Not found → Default to LOCAL provider
  │    │                Create LiteDB at default path
  │    │                App is immediately functional
  │    │                Frontend shows SetupWizard for user choice
  │    │
  │    ├── Provider = Local → Initialize LiteDbContext
  │    │                       App ready immediately
  │    │
  │    └── Provider = MongoDB → Initialize MongoDbContext
  │                              May fail (offline, bad config)
  │                              App still loads, shows warning
  │
  ├── Register DI services based on provider
  ├── Start SignalR hub
  └── Serve frontend
```

**Key change**: Even before the user makes a choice in the wizard, the app boots with LiteDB. This means:
- SignalR connects immediately
- Settings can be loaded/saved
- Layout persists
- Only the "SetupWizard" overlay blocks the main UI

---

### Phase 4: Offline Handling

#### 4.1 Local DB = Always works offline

When using LiteDB, the app is inherently offline-first. No network needed for any database operations.

#### 4.2 MongoDB = Graceful degradation

When using MongoDB and the connection is lost:
1. SignalR `OnReconnecting` triggers UI overlay (already exists)
2. Health check detects `databaseConnected: false`
3. Status bar shows warning with "Switch to Local" action button
4. All database operations fail gracefully with user-visible errors

#### 4.3 Future: Sync between local and cloud

Not in initial scope, but the architecture supports a future sync feature:
- Export local DB to ADIF → Import into cloud DB
- Or a dedicated sync service that mirrors collections

---

## Migration Order & Risk Assessment

### Recommended implementation order:

| Step | Description | Risk | Effort |
|------|-------------|------|--------|
| 1 | Create missing repository interfaces + MongoDB implementations | Low | 1 day |
| 2 | Refactor services to use interfaces instead of `MongoDbContext` directly | Medium | 1 day |
| 3 | Decouple `Contracts` from MongoDB BSON attributes | **High** | 2 days |
| 4 | Implement `IDbContext` + `LiteDbContext` | Low | 1 day |
| 5 | Implement LiteDB repository classes | Medium | 2 days |
| 6 | Add provider-based DI registration | Low | 0.5 day |
| 7 | Update `UserConfigService` + Setup API | Low | 0.5 day |
| 8 | Redesign SetupWizard with local/cloud choice | Low | 1 day |
| 9 | Wire SetupWizard into App.tsx + update Settings panel | Low | 0.5 day |
| 10 | Update health check + status bar for provider awareness | Low | 0.5 day |
| 11 | Testing (both providers, switching, first-run, offline) | Medium | 2 days |

**Total estimate: ~12 days of focused work**

### Biggest risk: Step 3 (BSON attribute decoupling)

The `Contracts` project is shared and its models are serialized in multiple places:
- MongoDB BSON serialization
- JSON API responses (System.Text.Json)
- SignalR message payloads
- ADIF import/export

Changing attributes could break serialization. **Mitigation**:
- Do this in a dedicated PR with comprehensive tests
- Use MongoDB class maps for BSON mapping (keeps Contracts clean)
- Ensure JSON property names match existing `[BsonElement("name")]` values to avoid API breaking changes

---

## Decision: Default to Local on First Install

The single most impactful change is: **when no `config.json` exists, default to LiteDB instead of "not configured"**.

This means:
- `UserConfigService.IsConfigured()` returns `true` even on first run (local is the default)
- `MongoDbContext` constructor path changes: instead of "waiting for setup wizard", it initializes LiteDB
- The SetupWizard is still shown but the app is **already functional** behind it
- Users who dismiss or ignore the wizard still have a working app

This eliminates the #1 complaint: "I installed the app but it doesn't work."
