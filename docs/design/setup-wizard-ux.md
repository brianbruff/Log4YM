# Setup Wizard UX Design

## Overview

This document defines the UX flow for the redesigned setup wizard, updated Settings > Database section, and provider-aware status indicators. The goal is to make Log4YM work immediately on first launch (via local LiteDB) while offering an optional cloud upgrade path.

---

## Design Principles

1. **Zero-friction start** - One click to a working app with local storage
2. **Progressive disclosure** - Show complexity only when the user opts into cloud
3. **Consistent visual language** - Reuse existing glass-panel, glass-button, glass-input patterns and Lucide icons
4. **Non-destructive** - Always warn before actions that could lose data (provider switching)
5. **Reversible** - Users can switch between local and cloud at any time from Settings

---

## 1. First-Run Setup Wizard

### 1.1 Trigger Condition

The wizard renders as a **blocking full-screen overlay** (`z-[200]`, matching the existing SetupWizard z-index) when:

```
!isLoading && !status?.isConfigured
```

The app is already functional behind the wizard (LiteDB initialized by default), so SignalR connects, settings load, and layout persists even before the user makes a choice.

### 1.2 Wizard Layout

Single-screen, two-card choice layout. No multi-step flow for the happy path.

```
+------------------------------------------------------------------+
|  fixed inset-0 bg-dark-900 z-[200]                               |
|  flex items-center justify-center                                |
|                                                                  |
|  +----------------------------------------------------------+   |
|  | glass-panel max-w-2xl animate-fade-in                     |   |
|  |                                                           |   |
|  |  [Header]                                                 |   |
|  |  +------------------------------------------------------+ |   |
|  |  | w-14 h-14 bg-orange-500/20 rounded-xl                | |   |
|  |  | <Radio> icon      LOG4YM                              | |   |
|  |  |                   Welcome! Let's get you set up.      | |   |
|  |  +------------------------------------------------------+ |   |
|  |                                                           |   |
|  |  [Two Choice Cards - grid grid-cols-2 gap-4]             |   |
|  |  +-------------------------+ +-------------------------+  |   |
|  |  | LOCAL DATABASE          | | CLOUD DATABASE          |  |   |
|  |  |                         | |                         |  |   |
|  |  | <HardDrive> icon        | | <Cloud> icon            |  |   |
|  |  |                         | |                         |  |   |
|  |  | * Works offline         | | * MongoDB Atlas         |  |   |
|  |  | * No setup needed       | | * Multi-device sync     |  |   |
|  |  | * Data stays on this    | | * Cloud backup          |  |   |
|  |  |   computer              | | * Free tier available   |  |   |
|  |  |                         | |                         |  |   |
|  |  | [Get Started] (primary) | | [Configure] (default)   |  |   |
|  |  +-------------------------+ +-------------------------+  |   |
|  |                                                           |   |
|  |  "You can switch between local and cloud                  |   |
|  |   at any time in Settings."                               |   |
|  +----------------------------------------------------------+   |
|                                                                  |
+------------------------------------------------------------------+
```

### 1.3 Card Design Details

**Local Database Card** (left, recommended):
- Border: `border-accent-success/30` (green tint to signal "easy/go")
- Icon: `<HardDrive>` from Lucide, `w-10 h-10 text-accent-success`
- Heading: "Local Database" - `text-lg font-semibold text-dark-200`
- Bullet points: `text-sm text-dark-300`, each with a `<Check>` icon prefix in `text-accent-success`
- Recommended badge: Small `"Recommended"` tag top-right corner using `bg-accent-success/20 text-accent-success text-xs px-2 py-0.5 rounded-full`
- CTA button: `glass-button-success` - "Get Started"

**Cloud Database Card** (right):
- Border: `border-accent-info/30` (blue tint for cloud)
- Icon: `<Cloud>` from Lucide, `w-10 h-10 text-accent-info`
- Heading: "Cloud Database" - `text-lg font-semibold text-dark-200`
- Bullet points: `text-sm text-dark-300`, each with a `<Check>` icon prefix in `text-accent-info`
- CTA button: `glass-button` (neutral) - "Configure"

**Card hover states**: `hover:border-accent-success/50` (local) / `hover:border-accent-info/50` (cloud), `hover:bg-dark-700/30`, `transition-all duration-200`

**Card selection**: When a card is clicked, it gains `ring-2 ring-accent-success/40` or `ring-2 ring-accent-info/40` with `scale-[1.01]` transform.

### 1.4 Flow: Local Database (Happy Path)

```
User clicks "Get Started"
  |
  v
Button shows spinner: <Loader2 animate-spin> "Setting up..."
  |
  v
POST /api/setup/configure { provider: "local" }
  |
  +-- Success (expected ~200ms) --+
  |                                |
  v                                v
Success state:                   Error state:
  <CheckCircle> green             <AlertCircle> red
  "You're all set!"               "Setup failed: {message}"
  "Using local database"          [Try Again] button
  auto-dismiss after 1.5s
  |
  v
onComplete() -> main app loads
```

**Total interaction time: ~2 seconds, zero configuration required.**

The success state uses the same animation pattern as the existing wizard's `step === 'success'` state: centered `<CheckCircle>` icon with a green accent, brief message, and auto-dismiss.

### 1.5 Flow: Cloud Database

```
User clicks "Configure"
  |
  v
Wizard transitions to MongoDB configuration form
(reuses existing SetupWizard form layout, slightly adapted)
  |
  +-- [Back] button top-left to return to choice screen
  |
  v
  +------------------------------------------------------+
  | CLOUD DATABASE SETUP                                  |
  |                                                       |
  | <Server> MongoDB Connection String                    |
  | [mongodb+srv://...____________] [eye toggle]          |
  | "Stored locally, never sent elsewhere"                |
  |                                                       |
  | Database Name                                         |
  | [Log4YM________________________]                      |
  | "Created automatically if it doesn't exist"           |
  |                                                       |
  | [Test Result area - same as existing]                 |
  |                                                       |
  | <ExternalLink> Get a free MongoDB Atlas cluster       |
  |                                                       |
  |                    [Back]  [Test Connection]           |
  |                    (after test passes:)                |
  |                    [Back]  [Save & Continue]           |
  +------------------------------------------------------+
```

This reuses the existing `SetupWizard.tsx` form logic and `useSetupStore` actions, but the API call body changes to include `provider: "mongodb"`:

```
POST /api/setup/configure {
  provider: "mongodb",
  connectionString: "...",
  databaseName: "..."
}
```

The step progression remains: `input` -> `tested` -> `success`, identical to current behavior.

### 1.6 Transitions & Animations

- **Choice -> Form**: Slide-left animation (`transform: translateX(-100%)` on choice, `translateX(0)` on form), 300ms ease-out. Both panels exist in a `overflow-hidden` container with `flex` positioning.
- **Choice -> Success (local)**: Fade-out choice cards, fade-in success state. Use existing `animate-fade-in` class.
- **Success -> App**: Fade out the entire overlay (`opacity 0` over 300ms), then `onComplete()`.

### 1.7 Responsive Considerations

- **Desktop (>= 640px)**: Two-column card layout as shown
- **Narrow/Mobile (< 640px)**: Stack cards vertically (`grid-cols-1`), reduce padding. The wizard container changes to `max-w-md` with `mx-4`.

---

## 2. Settings > Database Section Redesign

### 2.1 Updated Navigation Label

The sidebar entry changes from:
```
Database - "MongoDB connection settings"
```
to:
```
Database - "Storage provider and connection"
```

### 2.2 Provider-Aware Layout

The Database settings section now adapts based on the active provider.

```
+------------------------------------------------------+
| DATABASE CONNECTION                                   |
| "Manage your database storage provider."              |
|                                                       |
| [Provider Status Banner]                              |
| +--------------------------------------------------+ |
| | <icon> <status text>                              | |
| | Provider: Local (LiteDB) | Cloud (MongoDB)       | |
| | Status: Connected                                 | |
| +--------------------------------------------------+ |
|                                                       |
| Current Provider                                      |
| +-------------------+ +-------------------+           |
| | <HardDrive>       | | <Cloud>           |           |
| | Local              | | Cloud             |           |
| | (active/selected)  | | (inactive)        |           |
| +-------------------+ +-------------------+           |
|                                                       |
| [Provider-specific content below]                     |
+------------------------------------------------------+
```

### 2.3 Provider Selector

Two toggle-style buttons (same pattern as Rotator's network/serial selector):

```tsx
<div className="grid grid-cols-2 gap-3">
  <button className={`p-4 rounded-lg border transition-all ${
    provider === 'local'
      ? 'border-accent-primary bg-accent-primary/10'
      : 'border-glass-100 hover:border-glass-200'
  }`}>
    <HardDrive /> Local
    <span className="text-xs text-dark-300">LiteDB file on this computer</span>
  </button>
  <button className={`p-4 rounded-lg border transition-all ${
    provider === 'mongodb'
      ? 'border-accent-primary bg-accent-primary/10'
      : 'border-glass-100 hover:border-glass-200'
  }`}>
    <Cloud /> Cloud
    <span className="text-xs text-dark-300">MongoDB Atlas or self-hosted</span>
  </button>
</div>
```

Switching providers shows a **confirmation dialog** (see section 2.6).

### 2.4 Local Provider View

When `provider === "local"`:

```
+------------------------------------------------------+
| DATABASE FILE                                         |
|                                                       |
| Location: ~/Library/Application Support/              |
|           Log4YM/log4ym.db                            |
|                                                       |
| Size: 2.4 MB                                         |
| QSOs: 1,234                                          |
| Last modified: 2026-02-14 10:30 UTC                  |
|                                                       |
| [Export to ADIF]                                      |
|                                                       |
| Info box:                                             |
| "Your data is stored locally in a LiteDB file.       |
|  No internet connection required. Back up this file   |
|  to preserve your logs."                              |
+------------------------------------------------------+
```

- File path shown in `font-mono text-sm text-dark-300` with a subtle copy button
- Stats displayed in a `bg-dark-700/50 rounded-lg border border-glass-100` card
- Export button: `glass-button` with `<Download>` icon

### 2.5 Cloud Provider View

When `provider === "mongodb"`:

The existing `DatabaseSettingsSection` content is shown (connection string, database name, test connection, save & reconnect). This is the current implementation with minor label updates:

- Status banner color matches `accent-success` (connected) or `accent-danger` (not connected)
- "MongoDB Atlas" link remains
- Test Connection and Save & Reconnect buttons remain

### 2.6 Provider Switch Confirmation

When the user clicks a different provider, show a confirmation dialog before switching:

```
+------------------------------------------------------+
| <AlertTriangle> Switch Database Provider?             |
|                                                       |
| You are about to switch from Local to Cloud.          |
|                                                       |
| Warning: Switching providers does not migrate your    |
| data. Export your QSOs to ADIF first if you want      |
| to transfer them.                                     |
|                                                       |
|                [Cancel]  [Switch Provider]             |
+------------------------------------------------------+
```

- Uses `bg-accent-warning/10 border-accent-warning/30` styling
- Cancel button: `glass-button`
- Switch button: `glass-button-primary` with `<ArrowRightLeft>` icon
- If switching to cloud, the MongoDB configuration form appears after confirmation
- If switching to local, immediate `POST /api/setup/configure { provider: "local" }` then refresh

---

## 3. Status Bar Updates

### 3.1 Current Behavior (MongoDB-only)

The status bar currently shows a pulsing red `"MongoDB Not Connected"` warning when `mongoDbConnected === false`.

### 3.2 New Provider-Aware Status

The status bar warning becomes provider-aware:

**When Local provider, always connected (no warning):**
- No database warning shown (LiteDB is always available)

**When Cloud provider, disconnected:**
```
<AlertTriangle> Cloud Database Disconnected
```
- Same pulsing red styling as current
- Add a clickable action: `[Switch to Local]` that opens Settings > Database with a pre-filled switch prompt

**When Cloud provider, connected:**
- No warning (same as current working state)

### 3.3 Appstore State Change

Rename `mongoDbConnected` to `databaseConnected` and add `databaseProvider` field:

```typescript
// In appStore
databaseConnected: boolean;
databaseProvider: 'local' | 'mongodb';
```

The health endpoint response changes to include `databaseProvider`:
```json
{
  "databaseConnected": true,
  "databaseProvider": "local",
  "databaseName": "log4ym.db"
}
```

---

## 4. Connection Overlay Updates

### 4.1 Current Behavior

The `ConnectionOverlay` blocks UI when SignalR is disconnected/reconnecting. This is about SignalR connectivity, not database connectivity, so it remains largely unchanged.

### 4.2 No Changes Needed

The connection overlay handles SignalR state, which is orthogonal to the database provider. Since the app boots with LiteDB by default, SignalR connects immediately regardless of database choice. No changes required to this component.

---

## 5. Store Changes (setupStore)

### 5.1 Extended SetupStatus

```typescript
export interface SetupStatus {
  isConfigured: boolean;
  isConnected: boolean;
  provider: 'local' | 'mongodb';  // NEW
  configuredAt?: string;
  databaseName?: string;
  localDbPath?: string;            // NEW - for local provider
  localDbSizeBytes?: number;       // NEW - for local provider stats
  qsoCount?: number;               // NEW - for local provider stats
}
```

### 5.2 Extended SetupState

```typescript
interface SetupState {
  // ... existing fields ...
  provider: 'local' | 'mongodb';  // NEW - selected provider

  // Actions
  setProvider: (provider: 'local' | 'mongodb') => void;  // NEW
  configureLocal: () => Promise<boolean>;                  // NEW
  configure: () => Promise<boolean>;                       // existing, now sends provider
}
```

### 5.3 New API Call for Local Setup

```typescript
configureLocal: async () => {
  set({ isLoading: true, error: null });
  try {
    const response = await fetch('/api/setup/configure', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ provider: 'local' }),
    });
    const result = await response.json();
    if (result.success) {
      await get().fetchStatus();
      return true;
    }
    set({ error: result.message, isLoading: false });
    return false;
  } catch (err) {
    set({ error: err instanceof Error ? err.message : 'Setup failed', isLoading: false });
    return false;
  }
}
```

---

## 6. Component Hierarchy

```
App.tsx
  |
  +-- (if !isConfigured) SetupWizard
  |     |
  |     +-- WizardChoiceScreen (default view)
  |     |     +-- LocalCard
  |     |     +-- CloudCard
  |     |
  |     +-- WizardCloudForm (if user chose cloud)
  |     |     +-- (reuses existing MongoDB form logic)
  |     |
  |     +-- WizardSuccess (after either path completes)
  |
  +-- (if isConfigured) Main App
        +-- Layout (FlexLayout)
        +-- StatusBar (provider-aware warnings)
        +-- SettingsPanel
        |     +-- DatabaseSettingsSection (provider-aware)
        |           +-- ProviderSelector
        |           +-- LocalProviderView (if local)
        |           +-- CloudProviderView (if cloud, existing form)
        |           +-- ProviderSwitchConfirmation (modal)
        +-- ConnectionOverlay (unchanged, SignalR only)
```

---

## 7. Accessibility

- All interactive cards are `<button>` elements with `role` and `aria-label`
- Choice cards support keyboard navigation (Tab, Enter/Space to select)
- Provider switch confirmation is a focus-trapped modal
- Color is never the sole indicator - always paired with icons and text
- All form inputs have associated `<label>` elements (existing pattern)
- Success/error states use `<CheckCircle>`/`<AlertCircle>` icons alongside color

---

## 8. Icon Inventory (Lucide)

| Usage | Icon | Import |
|-------|------|--------|
| Local database | `HardDrive` | `lucide-react` |
| Cloud database | `Cloud` | `lucide-react` |
| Success state | `CheckCircle` | already imported |
| Error state | `AlertCircle` | already imported |
| Loading spinner | `Loader2` | already imported |
| Back navigation | `ArrowLeft` | `lucide-react` |
| Provider switch | `ArrowRightLeft` | `lucide-react` |
| Database (generic) | `Database` | already imported |
| Connection string | `Server` | already imported |
| External link | `ExternalLink` | already imported |
| Eye toggle | `Eye` / `EyeOff` | already imported |
| Radio/brand | `Radio` | already imported |
| Warning | `AlertTriangle` | already imported |
| File export | `Download` | already imported |
| Check bullet | `Check` | already imported |
| Copy path | `Copy` | already imported |

All icons already exist in the project's Lucide dependency. New imports needed: `HardDrive`, `Cloud`, `ArrowLeft`, `ArrowRightLeft`.

---

## 9. Edge Cases

| Scenario | Behavior |
|----------|----------|
| User closes wizard without choosing | App continues with LiteDB (already initialized). Wizard re-appears on next launch until `isConfigured` is set. |
| User picks local, then later opens Settings > Database | Shows local provider view with file stats. Can switch to cloud. |
| User picks cloud, connection fails on next launch | App boots, cloud connection fails. Status bar shows warning with "Switch to Local" action. Settings > Database shows red status. |
| User switches cloud -> local | Confirmation dialog warns about non-migration. Local DB created fresh (or reuses existing if present). |
| User switches local -> cloud | Confirmation dialog. MongoDB form appears. Must test connection before saving. |
| Electron app in offline mode | Local provider works perfectly. Cloud provider shows connection failure. |

---

## 10. Implementation Notes for Frontend Developer

1. **Refactor `SetupWizard.tsx`** - Replace single MongoDB form with choice screen + conditional form. The component stays at `z-[200]` and is conditionally rendered in `App.tsx`.

2. **Extend `setupStore.ts`** - Add `provider` field, `configureLocal` action, update `configure` to include provider in request body.

3. **Refactor `DatabaseSettingsSection`** in `SettingsPanel.tsx` - Add provider selector, conditional views, switch confirmation dialog.

4. **Update `StatusBar.tsx`** - Replace `mongoDbConnected` check with `databaseConnected` + `databaseProvider` awareness. Suppress warning for local provider.

5. **Update `appStore.ts`** - Rename `mongoDbConnected` to `databaseConnected`, add `databaseProvider`.

6. **Update `App.tsx`** - Change health check to read `databaseConnected` and `databaseProvider` from response. Wire `SetupWizard` render condition using `setupStore.status?.isConfigured`.

7. **CSS** - No new CSS classes needed. All styling uses existing Tailwind utilities and glass-* component classes.
