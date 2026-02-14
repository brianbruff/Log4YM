# Map Markers — Callsign Image & Placeholder Feature

## Overview

The 2D map displays circular markers at the geographic location of amateur radio stations. When a callsign is looked up (via QRZ), the station's coordinates are used to place a marker on the map. Stations with a QRZ profile picture show that image inside the marker; stations without a picture show a radio emoji (📻) placeholder.

Markers come in two visual scales:

| Scale | Size | Border | Color | Usage |
|-------|------|--------|-------|-------|
| **2x** | 56px | 3px | Amber `#ffb432` | Currently focused callsign |
| **1x** | 44px | 2px | Cyan `#00ddff` | Previously saved callsigns |

## Behavior Matrix

| Scenario | Has Image? | Has Lat/Lon? | Marker Appearance |
|----------|:----------:|:------------:|-------------------|
| Focused callsign with QRZ photo | Yes | Yes | 2x image marker (amber border) |
| Focused callsign, no QRZ photo | No | Yes | 2x placeholder marker with 📻 (amber border) |
| Focused callsign, no coordinates | N/A | No | No marker placed on map |
| Saved callsign with QRZ photo | Yes | Yes | 1x image marker (cyan border) |
| Saved callsign, no QRZ photo | No | Yes | 1x placeholder marker with 📻 (cyan border) |
| `showCallsignImages` disabled | N/A | N/A | Falls back to generic dot marker (focused) or hidden (saved) |

## Settings

These settings are accessible from the map's Sun/Overlay panel under "Callsign Images":

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `showCallsignImages` | boolean | `true` | Master toggle for callsign image/placeholder markers |
| `maxCallsignImages` | number | `50` | Max saved markers to display (range: 5–200) |

When `showCallsignImages` is off, the focused callsign uses the standard orange dot marker (`targetIcon`) instead of the callsign image icon.

## Data Flow

```
┌─────────────┐     FocusCallsign      ┌──────────────┐
│   Frontend   │ ─────────────────────> │   LogHub.cs  │
│  (SignalR)   │                        │  (SignalR Hub)│
└─────────────┘                        └──────┬───────┘
                                              │
                                    QRZ Lookup │
                                              ▼
                                       ┌──────────────┐
                                       │  QRZ Service  │
                                       └──────┬───────┘
                                              │
                         Returns QrzCallsignInfo (callsign, imageUrl?,
                                    lat, lon, name, country, grid)
                                              │
                      ┌───────────────────────┼──────────────────────┐
                      ▼                       ▼                      ▼
              OnCallsignLookedUp     SaveCallsignMapImageAsync    MongoDB
              (broadcast to all      (upsert by callsign)       ┌─────────┐
               clients via SignalR)   if lat/lon present         │callsign │
                      │                                          │imageUrl?│
                      ▼                                          │lat, lon │
              ┌───────────────┐                                  │name     │
              │  appStore     │                                  │country  │
              │  addCallsign- │                                  │grid     │
              │  MapImage()   │                                  │savedAt  │
              └───────┬───────┘                                  └─────────┘
                      │                                               │
                      ▼                                               │
              ┌───────────────┐    On page load / settings change     │
              │  MapPlugin    │ <─────────────────────────────────────┘
              │  renders      │    GET /api/callsign-images?limit=N
              │  markers      │
              └───────────────┘
```

### Step-by-step

1. User focuses a callsign (types it, clicks a DX spot, etc.)
2. Frontend sends `FocusCallsign` via SignalR to `LogHub`
3. `LogHub` calls `QrzService.LookupCallsignAsync()` to get station info
4. If lat/lon are present, `SaveCallsignMapImageAsync` upserts the record into MongoDB's `callsignMapImages` collection — regardless of whether `imageUrl` is present
5. `OnCallsignLookedUp` event is broadcast to all connected clients
6. Frontend `appStore.addCallsignMapImage()` adds/replaces the entry in client-side state (keyed by callsign, case-insensitive)
7. `MapPlugin` renders the focused callsign as a **2x marker** and all other saved callsigns as **1x markers**
8. On page load, `GET /api/callsign-images?limit=N` rehydrates the saved markers from MongoDB

## Placeholder Behavior

The `createCallsignImageIcon` function handles both image and no-image cases:

- **With `imageUrl`**: Renders an `<img>` tag with the QRZ photo. If the image fails to load (broken URL, 404), an `onerror` handler replaces it with the 📻 emoji.
- **Without `imageUrl`**: Renders the 📻 emoji directly inside the circular marker. No `<img>` tag is created.

Both variants share the same visual styling (circle border, glow shadow, callsign label underneath) so image markers and placeholder markers look consistent on the map.

```
imageUrl present?
    ├── Yes ──> <img src="..." onerror="show 📻" />
    └── No ───> <div>📻</div>
```

## Key Source Files

| File | Role |
|------|------|
| `src/Log4YM.Web/src/plugins/MapPlugin.tsx` | Map rendering, marker icons, settings UI |
| `src/Log4YM.Server/Hubs/LogHub.cs` | SignalR hub, QRZ lookup orchestration, MongoDB persistence |
| `src/Log4YM.Contracts/Models/CallsignMapImage.cs` | MongoDB document model (`ImageUrl` is nullable `string?`) |
| `src/Log4YM.Web/src/api/client.ts` | REST client + `CallsignMapImage` TypeScript interface (`imageUrl?` optional) |
| `src/Log4YM.Web/src/store/appStore.ts` | Client-side state for `callsignMapImages[]` |
| `src/Log4YM.Web/src/store/settingsStore.ts` | `showCallsignImages` and `maxCallsignImages` settings |

## Fix Summary (Before/After)

### Before

The map marker persistence had three gatekeepers that all required a non-empty `imageUrl`:

1. **Backend** (`LogHub.cs`): `if (!string.IsNullOrEmpty(info.ImageUrl) && lat && lon)` — callsigns without a QRZ photo were never saved to MongoDB.
2. **Frontend store** (`MapPlugin.tsx` useEffect): `if (!focusedCallsignInfo?.imageUrl) return` — no-image callsigns were never added to client-side state.
3. **Frontend render** (`MapPlugin.tsx` marker): `focusedCallsignInfo?.imageUrl && showCallsignImages ? <ImageMarker> : <DotMarker>` — no-image callsigns always fell through to the generic dot.

**Result**: Only stations with QRZ profile pictures appeared as styled markers. Stations without photos got a plain dot while focused and disappeared entirely from the map afterward.

### After

1. **Backend**: Persistence guard changed to `if (info.Latitude.HasValue && info.Longitude.HasValue)` — any callsign with coordinates is saved. `ImageUrl` is stored as nullable.
2. **Frontend store**: Guard changed to check only `latitude` and `longitude` — no-image callsigns enter client state.
3. **Frontend render**: `createCallsignImageIcon` accepts `imageUrl: string | undefined | null` and renders the 📻 placeholder when no image URL is available. Both the focused (2x) and saved (1x) marker paths use this unified function.

**Result**: All looked-up callsigns with valid coordinates get a styled circular marker on the map — with their QRZ photo if available, or the 📻 placeholder if not.
