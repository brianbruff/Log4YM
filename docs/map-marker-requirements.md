# Map Marker Requirements & Acceptance Criteria

## Overview

The map displays callsign markers for stations being worked and previously logged. Stations with QRZ profile pictures show circular image markers; stations without pictures should show a radio emoji placeholder instead of disappearing.

## Definitions

| Term | Meaning |
|------|---------|
| Focused callsign | The callsign currently being worked (tuned in / selected) |
| Logged callsign | A callsign for which a QSO has been saved |
| Image marker | Circular marker showing a QRZ profile picture with callsign label |
| Placeholder marker | Circular marker showing a radio emoji with callsign label |
| 2x scale | Active/focused marker: 56px, 3px amber (#ffb432) border |
| 1x scale | Logged/saved marker: 44px, 2px cyan (#00ddff) border |

## Root Cause Analysis

Three code paths prevent no-image callsigns from persisting:

1. **Backend skip** (`LogHub.cs:182`): The condition `!string.IsNullOrEmpty(info.ImageUrl)` gates MongoDB persistence, so callsigns without a QRZ picture are never saved.
2. **Frontend store skip** (`MapPlugin.tsx:633`): The `useEffect` that adds callsigns to the local store early-returns when `!focusedCallsignInfo?.imageUrl`, so no-image callsigns never enter the client-side list.
3. **Frontend render skip** (`MapPlugin.tsx:776`): The focused marker conditional checks `focusedCallsignInfo?.imageUrl && ...`, falling through to the generic dot marker rather than a styled placeholder.

## Acceptance Criteria

### AC-1: Focused callsign WITH image shows image marker at 2x

**Precondition:** A callsign is focused that has a QRZ profile picture and valid lat/lon.

| Step | Expected |
|------|----------|
| Callsign lookup returns with imageUrl, lat, lon | Map shows image marker at 2x scale (56px, amber border) at the station's coordinates |
| Popup is opened on marker | Shows callsign (amber, monospace), name, grid, bearing/distance |

**Status:** Already works. Must not regress.

### AC-2: Focused callsign WITHOUT image shows placeholder marker at 2x

**Precondition:** A callsign is focused that has NO QRZ picture but has valid lat/lon.

| Step | Expected |
|------|----------|
| Callsign lookup returns with no imageUrl, but has lat/lon | Map shows placeholder marker at 2x scale (56px, amber border) with radio emoji centered in the circle |
| Callsign label is shown below the placeholder | Label uses amber color, monospace, same style as image markers |
| Popup is opened on marker | Shows callsign, name (if available), grid, bearing/distance - same as AC-1 |

**Current behavior:** Falls through to generic colored dot marker (targetIcon). **Must be fixed.**

### AC-3: Logged callsign WITH image persists at 1x

**Precondition:** A QSO is logged for a callsign that has a QRZ profile picture.

| Step | Expected |
|------|----------|
| QSO is logged | Marker transitions from 2x (amber) to 1x (44px, cyan border) |
| Marker remains on map | Image marker stays visible after focus moves away |
| Session is reloaded | Marker reappears on map (loaded from MongoDB via `/api/callsign-images`) |

**Status:** Already works. Must not regress.

### AC-4: Logged callsign WITHOUT image persists at 1x

**Precondition:** A QSO is logged for a callsign that has NO QRZ picture but has valid lat/lon.

| Step | Expected |
|------|----------|
| QSO is logged | Placeholder marker transitions from 2x (amber) to 1x (44px, cyan border) with radio emoji |
| Marker remains on map | Placeholder marker stays visible after focus moves away |
| Session is reloaded | Placeholder marker reappears on map (loaded from MongoDB) |

**Current behavior:** Marker disappears because (a) it was never added to the local store, and (b) it was never saved to MongoDB. **Must be fixed.**

### AC-5: Session reload shows all persisted markers up to limit

**Precondition:** MongoDB has both image and no-image callsign entries. `showCallsignImages` is enabled.

| Step | Expected |
|------|----------|
| App starts / session loads | GET `/api/callsign-images?limit=N` returns both image and no-image entries |
| Markers are rendered | Image entries show QRZ pictures; no-image entries show radio emoji placeholder |
| Total markers respect `maxCallsignImages` setting | At most N markers are shown, ordered by most recently saved |

### AC-6: `showCallsignImages` toggle hides/shows all markers

| Step | Expected |
|------|----------|
| Setting is toggled OFF | All callsign markers (both image and placeholder) are hidden |
| Setting is toggled ON | All callsign markers reappear |

**Status:** Already works for image markers. Must also work for placeholder markers.

### AC-7: Callsign with no lat/lon is not shown on map

**Precondition:** A callsign is looked up but has no latitude/longitude data.

| Step | Expected |
|------|----------|
| Callsign lookup returns without lat/lon | No marker is placed on the map regardless of image presence |
| No record is saved to MongoDB | Backend skips persistence |

**Status:** Already works. Must not regress.

## Required Changes (Summary for Developers)

### Backend

1. **`LogHub.cs`** — Change the persistence guard from `!string.IsNullOrEmpty(info.ImageUrl) && lat && lon` to just `lat && lon`. Allow `ImageUrl` to be null/empty when saving.
2. **`CallsignMapImage.cs`** — Make `ImageUrl` nullable (`string?`) since callsigns without QRZ pictures will now be persisted.
3. **`CallsignImagesController.cs`** — Return `imageUrl` as null/empty for no-image entries (no filter change needed, just ensure the shape is preserved).
4. **`SaveCallsignMapImageAsync`** — Handle null `ImageUrl` in the update builder (use `info.ImageUrl ?? ""` or allow null).

### Frontend

5. **`client.ts`** — Make `imageUrl` optional (`imageUrl?: string` or `imageUrl: string | null`).
6. **`MapPlugin.tsx` (store effect, ~line 633)** — Remove the `imageUrl` guard from the `useEffect` so no-image callsigns are added to the local store. Keep the `latitude`/`longitude` guard.
7. **`MapPlugin.tsx` (focused marker, ~line 776)** — When `showCallsignImages` is enabled and there's no `imageUrl`, render a placeholder marker at 2x instead of the generic dot. Use `createCallsignImageIcon` or a new `createPlaceholderIcon` function.
8. **`MapPlugin.tsx` (saved markers, ~line 830)** — When rendering saved markers, check if `imageUrl` is present. If not, render with a placeholder icon instead.
9. **`createCallsignImageIcon` (~line 97)** — Either modify to accept an optional imageUrl (showing emoji when absent), or create a sibling `createPlaceholderIcon` function that renders the radio emoji in the same circular style.

## Test Scenarios

| # | Scenario | Category | Validates |
|---|----------|----------|-----------|
| T1 | Focused callsign with image renders 2x image marker | Unit (frontend) | AC-1 |
| T2 | Focused callsign without image renders 2x placeholder marker | Unit (frontend) | AC-2 |
| T3 | Logging a QSO transitions marker from 2x to 1x (with image) | Unit (frontend) | AC-3 |
| T4 | Logging a QSO transitions marker from 2x to 1x (without image) | Unit (frontend) | AC-4 |
| T5 | Backend saves callsign with image to MongoDB | Unit (backend) | AC-3 |
| T6 | Backend saves callsign without image to MongoDB | Unit (backend) | AC-4 |
| T7 | Backend skips save when no lat/lon | Unit (backend) | AC-7 |
| T8 | GET `/api/callsign-images` returns both image and no-image entries | Integration (backend) | AC-5 |
| T9 | Session reload renders mixed image/placeholder markers | Integration (frontend) | AC-5 |
| T10 | Toggle `showCallsignImages` hides/shows all marker types | Unit (frontend) | AC-6 |
| T11 | `createCallsignImageIcon` with image URL renders `<img>` tag | Unit (frontend) | AC-1, AC-3 |
| T12 | Placeholder icon renders radio emoji in circle | Unit (frontend) | AC-2, AC-4 |
