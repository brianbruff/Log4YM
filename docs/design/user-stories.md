# User Stories & Acceptance Criteria

## Database Abstraction & Onboarding Redesign

Reference: [database-abstraction-design.md](./database-abstraction-design.md)

---

## Epic 1: Zero-Friction First Run

### US-1.1: Default to local database on first launch

**As a** new user installing Log4YM for the first time,
**I want** the app to work immediately without any configuration,
**so that** I can start logging QSOs right away.

**Acceptance Criteria:**

- [ ] When no `config.json` exists, the app starts with a LiteDB local database at the platform-default path
- [ ] All core features work immediately: log entry, log history, settings, layout persistence
- [ ] SignalR connects successfully on first launch
- [ ] The app does not show a "not connected" error on first launch
- [ ] `GET /api/setup/status` returns `isConfigured: true` with `provider: "local"` even on first run
- [ ] `GET /api/health` reports `databaseConnected: true` on first run

### US-1.2: First-run setup wizard with database choice

**As a** new user on first launch,
**I want** to see a setup wizard that lets me choose between local and cloud database,
**so that** I understand my options and can pick the right one for me.

**Acceptance Criteria:**

- [ ] On first launch (no prior user choice recorded), the SetupWizard overlay renders at `z-[200]`, blocking the main UI
- [ ] The wizard presents two clear options: "Local Database" and "Cloud Database"
- [ ] The "Local Database" card describes: works offline, no setup needed, data stored on this computer
- [ ] The "Cloud Database" card describes: MongoDB Atlas, multi-device sync, cloud backup, free tier available
- [ ] A note below both cards states: "You can switch between local and cloud at any time in Settings"
- [ ] The app is functional behind the wizard overlay (LiteDB initialized per US-1.1)

### US-1.3: Instant local database setup from wizard

**As a** new user who chose "Local Database" in the setup wizard,
**I want** setup to complete instantly with no further input,
**so that** I can start using the app in seconds.

**Acceptance Criteria:**

- [ ] Clicking "Get Started" on the local database card sends `POST /api/setup/configure` with `{ provider: "local" }`
- [ ] The backend confirms the LiteDB file is ready (it was already initialized at startup)
- [ ] A brief success animation plays and the wizard dismisses
- [ ] The main app UI is fully interactive within ~2 seconds of clicking "Get Started"
- [ ] The user's choice is persisted so the wizard does not reappear on subsequent launches

### US-1.4: Cloud database setup from wizard

**As a** new user who chose "Cloud Database" in the setup wizard,
**I want** to enter my MongoDB connection details and verify the connection,
**so that** I can use a cloud-hosted database.

**Acceptance Criteria:**

- [ ] Clicking "Configure" on the cloud database card shows the MongoDB connection form (connection string + database name)
- [ ] A "Test Connection" button validates the connection without saving
- [ ] On successful test, the number of existing databases on the server is displayed
- [ ] A "Save & Continue" button (enabled only after successful test) persists the configuration
- [ ] On save, the backend calls `ReinitializeAsync` to switch from the default LiteDB to MongoDB
- [ ] A success animation plays and the wizard dismisses
- [ ] If the test fails, a clear error message is shown (authentication error, connection timeout, unreachable host)
- [ ] The user can go back to the two-choice screen without losing entered data

---

## Epic 2: Database Provider Abstraction (Backend)

### US-2.1: Database context interface

**As a** developer,
**I want** a provider-agnostic `IDbContext` interface,
**so that** the app can support multiple database backends without changing consumer code.

**Acceptance Criteria:**

- [ ] `IDbContext` interface exists with: `IsConnected`, `DatabaseName`, `Provider` (enum), `TryInitialize()`, `ReinitializeAsync(DatabaseConfig)`
- [ ] `DatabaseProvider` enum has values: `Local` (LiteDB) and `MongoDb`
- [ ] `DatabaseConfig` model supports both providers: `Provider`, `ConnectionString`, `MongoDbDatabaseName`, `LocalDbPath`
- [ ] `MongoDbContext` implements `IDbContext`
- [ ] `LiteDbContext` implements `IDbContext`
- [ ] All services depend on `IDbContext` (not `MongoDbContext` directly)

### US-2.2: Complete repository interface coverage

**As a** developer,
**I want** repository interfaces for all database collections,
**so that** no service accesses database collections directly.

**Acceptance Criteria:**

- [ ] `ILayoutRepository` interface exists with: `GetAsync(string id)`, `UpsertAsync(Layout)`
- [ ] `IPluginSettingsRepository` interface exists with: `GetAsync(string pluginName)`, `UpsertAsync(PluginSettings)`
- [ ] `ISmartUnlinkRepository` interface exists with: `GetAllAsync()`, `UpsertAsync(SmartUnlinkRadioEntity)`, `DeleteAsync(string id)`
- [ ] `ICallsignImageRepository` interface exists with: `GetByCallsignAsync(string)`, `UpsertAsync(CallsignMapImage)`, `GetRecentAsync(int limit)`
- [ ] Existing `IQsoRepository` and `ISettingsRepository` remain unchanged
- [ ] No service or controller accesses `IMongoCollection<T>` directly; all go through repository interfaces
- [ ] All hub classes (SignalR) use repository interfaces, not `MongoDbContext` collections

### US-2.3: Decouple Contracts from MongoDB

**As a** developer,
**I want** the `Log4YM.Contracts` project to have no dependency on `MongoDB.Driver`,
**so that** shared models are provider-agnostic and can be used with any database backend.

**Acceptance Criteria:**

- [ ] `Log4YM.Contracts.csproj` has no reference to `MongoDB.Bson` or `MongoDB.Driver`
- [ ] All BSON attributes (`[BsonId]`, `[BsonElement]`, `[BsonExtraElements]`, `[BsonRepresentation]`) are removed from models
- [ ] Models use `System.Text.Json` attributes (`[JsonPropertyName]`) as the primary serialization contract
- [ ] `Qso.AdifExtra` changes from `BsonDocument?` to `Dictionary<string, object>?`
- [ ] A `MongoClassMapConfig` in the server project registers BSON mappings via `BsonClassMap.RegisterClassMap<T>()` for all models
- [ ] JSON property names in the new attributes match the existing `[BsonElement("name")]` values to avoid API breaking changes
- [ ] All existing API responses serialize identically (no breaking changes for frontend consumers)
- [ ] SignalR message payloads serialize correctly with the new attribute scheme
- [ ] ADIF import/export continues to work correctly

### US-2.4: MongoDB repository implementations

**As a** developer,
**I want** MongoDB-specific repository implementations for the new interfaces,
**so that** existing MongoDB functionality is preserved under the new abstraction.

**Acceptance Criteria:**

- [ ] `MongoLayoutRepository` implements `ILayoutRepository` using `IMongoCollection<Layout>`
- [ ] `MongoPluginSettingsRepository` implements `IPluginSettingsRepository`
- [ ] `MongoSmartUnlinkRepository` implements `ISmartUnlinkRepository`
- [ ] `MongoCallsignImageRepository` implements `ICallsignImageRepository`
- [ ] Existing `QsoRepository` is renamed to `MongoQsoRepository` and moved to the `Mongo/` folder
- [ ] Existing `SettingsRepository` is renamed to `MongoSettingsRepository` and moved to the `Mongo/` folder
- [ ] All MongoDB repositories live under `Core/Database/Mongo/`
- [ ] All existing MongoDB behavior (indexes, sorting, aggregation) is preserved

### US-2.5: LiteDB repository implementations

**As a** developer,
**I want** LiteDB repository implementations for all interfaces,
**so that** the local database provider has full feature parity.

**Acceptance Criteria:**

- [ ] `LiteDbContext` manages a single `.db` file at the configured or default path
- [ ] `LiteQsoRepository` implements `IQsoRepository` with equivalent behavior to MongoDB version
- [ ] `LiteSettingsRepository` implements `ISettingsRepository`
- [ ] `LiteLayoutRepository` implements `ILayoutRepository`
- [ ] `LitePluginSettingsRepository` implements `IPluginSettingsRepository`
- [ ] `LiteSmartUnlinkRepository` implements `ISmartUnlinkRepository`
- [ ] `LiteCallsignImageRepository` implements `ICallsignImageRepository`
- [ ] All LiteDB repositories live under `Core/Database/LiteDb/`
- [ ] `LiteQsoRepository.GetStatisticsAsync()` produces the same `QsoStatistics` output as the MongoDB version (implemented with LINQ-to-objects instead of aggregation pipeline)
- [ ] Multi-field sort (QsoDate + TimeOn) is handled via a `SortTimestamp` property or equivalent single-field approach
- [ ] `Dictionary<string, object>` fields (AdifExtra, PluginSettings.Settings) serialize correctly in LiteDB

### US-2.6: Provider-based DI registration

**As a** developer,
**I want** database services registered in DI based on the configured provider,
**so that** switching providers only requires a configuration change, not code changes.

**Acceptance Criteria:**

- [ ] `DbServiceRegistration.AddDatabase(services, config)` registers the correct context and all repositories based on `DatabaseConfig.Provider`
- [ ] When `Provider = Local`: `LiteDbContext` and all `Lite*Repository` classes are registered
- [ ] When `Provider = MongoDb`: `MongoDbContext`, `MongoClassMapConfig.Register()`, and all `Mongo*Repository` classes are registered
- [ ] `IDbContext` is registered as singleton; repositories are registered as scoped
- [ ] `Program.cs` / startup uses `DbServiceRegistration` instead of directly registering `MongoDbContext`

---

## Epic 3: Setup Wizard & Settings UI (Frontend)

### US-3.1: Redesigned setup wizard with two-choice layout

**As a** user,
**I want** a clear, visually appealing setup wizard that presents Local and Cloud as equal options,
**so that** I can make an informed decision about my database.

**Acceptance Criteria:**

- [ ] The wizard renders as a full-screen overlay at z-index 200
- [ ] Two side-by-side cards are shown: "Local Database" and "Cloud Database"
- [ ] Each card lists its key benefits (offline/no setup vs. multi-device/cloud backup)
- [ ] The Local card has a "Get Started" button; the Cloud card has a "Configure" button
- [ ] The wizard is responsive and usable at narrow window widths (cards stack vertically if needed)
- [ ] The Log4YM logo/branding is visible in the wizard

### US-3.2: Database provider setting in Settings panel

**As a** user who has already completed setup,
**I want** to view and change my database provider from Settings > Database,
**so that** I can switch between local and cloud at any time.

**Acceptance Criteria:**

- [ ] The Database section in Settings shows the current provider (Local or Cloud) in a dropdown or toggle
- [ ] When "Local" is selected: the database file path and size are displayed, along with the QSO count
- [ ] When "Cloud" is selected: the existing MongoDB connection string and database name fields are shown with Test Connection / Save & Reconnect buttons
- [ ] A "Switch to Cloud" or "Switch to Local" action is available to change providers
- [ ] A warning is displayed: "Switching providers does not migrate data. Export your QSOs to ADIF first if needed."
- [ ] Switching providers calls the backend reinitialize endpoint and refreshes the app state

### US-3.3: Setup store updates for provider awareness

**As a** frontend developer,
**I want** the `useSetupStore` to track the active database provider,
**so that** UI components can adapt based on whether Local or Cloud is active.

**Acceptance Criteria:**

- [ ] `useSetupStore` state includes `provider: "local" | "mongodb"` from `GET /api/setup/status`
- [ ] `fetchStatus()` populates the provider field
- [ ] Components can read `provider` to conditionally render provider-specific UI
- [ ] The store supports `configure(config)` for both local and MongoDB payloads

---

## Epic 4: Backend API Updates

### US-4.1: Updated setup API for provider selection

**As a** frontend,
**I want** the setup API to accept a provider choice,
**so that** users can configure either local or cloud databases.

**Acceptance Criteria:**

- [ ] `POST /api/setup/configure` accepts `{ provider: "local" | "mongodb", connectionString?, databaseName? }`
- [ ] When `provider = "local"`: backend confirms LiteDB is initialized, no connection string needed
- [ ] When `provider = "mongodb"`: existing behavior is preserved (validates and connects to MongoDB)
- [ ] `POST /api/setup/test-connection` continues to work for MongoDB connections
- [ ] `GET /api/setup/status` response includes `provider` field (e.g., `"local"` or `"mongodb"`)

### US-4.2: Updated health endpoint for provider awareness

**As a** frontend,
**I want** the health endpoint to report the active database provider,
**so that** the status bar and connection overlay can show accurate information.

**Acceptance Criteria:**

- [ ] `GET /api/health` response includes `databaseProvider: "local" | "mongodb"` (new field)
- [ ] The existing `mongoDbConnected` field is renamed to `databaseConnected` for provider-agnostic naming
- [ ] When using local provider, `databaseConnected` is always `true` (LiteDB is file-based, never "disconnected")
- [ ] The `databaseName` field reflects the active database (file name for LiteDB, database name for MongoDB)

### US-4.3: Updated user config model

**As a** developer,
**I want** the `UserConfig` model to store the chosen database provider,
**so that** the app restarts with the correct provider.

**Acceptance Criteria:**

- [ ] `UserConfig` includes `Provider` (defaults to `DatabaseProvider.Local`), `MongoDbConnectionString`, `MongoDbDatabaseName`, `LocalDbPath` (nullable, uses default if null), `ConfiguredAt`
- [ ] The config file is read at startup to determine which provider to initialize
- [ ] When `Provider = Local` and `LocalDbPath` is null, the default platform path is used
- [ ] Existing config files with MongoDB-only settings continue to work (backward compatible)

### US-4.4: Runtime provider switching

**As a** user,
**I want** to switch database providers without restarting the app,
**so that** I can move between local and cloud seamlessly.

**Acceptance Criteria:**

- [ ] `POST /api/setup/configure` with a different provider triggers `IDbContext.ReinitializeAsync()`
- [ ] DI-registered repositories are re-resolved to the new provider's implementations
- [ ] Active SignalR connections are maintained during the switch
- [ ] The frontend receives a notification (via SignalR or polling) that the provider changed
- [ ] After switching, all subsequent database operations use the new provider

---

## Epic 5: Offline & Resilience

### US-5.1: Local database works offline

**As a** user with the local database provider,
**I want** all database operations to work without internet,
**so that** I can log QSOs at field events or in areas without connectivity.

**Acceptance Criteria:**

- [ ] With `provider = "local"`, the app starts and is fully functional with no network connection
- [ ] QSO logging, search, statistics, settings, and layout persistence all work offline
- [ ] The status bar does not show any database warning when using local provider offline
- [ ] DX cluster and other network-dependent features fail gracefully independently of database status

### US-5.2: Graceful degradation for cloud database

**As a** user with the cloud (MongoDB) database provider,
**I want** the app to handle connection loss gracefully,
**so that** I understand what happened and can take action.

**Acceptance Criteria:**

- [ ] When the MongoDB connection is lost, the existing connection overlay / reconnecting UI appears
- [ ] The health check reports `databaseConnected: false`
- [ ] The status bar shows a warning with provider context (e.g., "MongoDB disconnected")
- [ ] A "Switch to Local" action button is shown so users can continue working offline
- [ ] Database operations fail with user-visible error messages, not unhandled exceptions

---

## Epic 6: Testing & Verification

### US-6.1: Repository unit tests for both providers

**As a** developer,
**I want** unit tests for all repository implementations,
**so that** both LiteDB and MongoDB providers have verified behavior.

**Acceptance Criteria:**

- [ ] Each `Lite*Repository` has unit tests covering CRUD operations
- [ ] Each `Mongo*Repository` has unit tests covering CRUD operations
- [ ] `LiteQsoRepository.GetStatisticsAsync()` is tested to produce correct aggregation results
- [ ] Tests for `ExistsAsync` duplicate checking pass for both providers
- [ ] Tests verify that `Dictionary<string, object>` fields (AdifExtra) round-trip correctly in both providers
- [ ] Tests run as part of the standard `dotnet test` suite (Category=Unit)

### US-6.2: Provider switching integration tests

**As a** developer,
**I want** integration tests that verify provider switching works end-to-end,
**so that** users can safely switch between local and cloud.

**Acceptance Criteria:**

- [ ] Test: app starts with local provider, verify QSO CRUD works
- [ ] Test: switch from local to MongoDB, verify new operations go to MongoDB
- [ ] Test: switch from MongoDB back to local, verify operations go to LiteDB
- [ ] Test: provider switch preserves SignalR connectivity
- [ ] Tests run as part of the standard `dotnet test` suite (Category=Integration)

### US-6.3: First-run onboarding integration tests

**As a** developer,
**I want** integration tests for the first-run flow,
**so that** new users always have a working experience.

**Acceptance Criteria:**

- [ ] Test: with no config file, app starts successfully with LiteDB
- [ ] Test: `GET /api/setup/status` returns `isConfigured: true` with no config file
- [ ] Test: `GET /api/health` returns `databaseConnected: true` with no config file
- [ ] Test: `POST /api/setup/configure` with `provider: "local"` succeeds and persists choice
- [ ] Test: `POST /api/setup/configure` with `provider: "mongodb"` and valid connection string succeeds

### US-6.4: BSON decoupling regression tests

**As a** developer,
**I want** regression tests that verify serialization is not broken after removing BSON attributes,
**so that** the API, SignalR, and ADIF import/export continue working correctly.

**Acceptance Criteria:**

- [ ] Test: API JSON responses for QSO objects have the same property names as before the change
- [ ] Test: SignalR hub messages deserialize correctly on the frontend
- [ ] Test: ADIF import produces the same `Qso` objects as before
- [ ] Test: ADIF export produces the same output as before
- [ ] Test: MongoDB documents written with new class maps are readable by old code (forward compatibility check)
