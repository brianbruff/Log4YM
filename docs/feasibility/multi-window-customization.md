# Multi-Window Customization Feasibility Study

**Date:** 2026-04-16
**Author:** Claude (AI Agent)
**Issue:** [More flexibility with windows](https://github.com/brianbruff/Log4YM/issues/TBD)

## Executive Summary

This document analyzes the feasibility of adding multi-window support and enhanced panel customization to Log4YM. The request is to:
1. Allow panels to be resized more flexibly (smaller/bigger)
2. Enable auto-hiding of less frequently used panels (like rig info after connection)
3. Support multiple native OS windows (mentioned as "flex-layout supports multiple windows")
4. Handle monitor count changes gracefully

**Verdict:** ✅ **FEASIBLE** with moderate complexity

## Current Architecture

### FlexLayout Implementation
- **Library:** `flexlayout-react` v0.7.15
- **Current Features:**
  - Single-window tabbed layout with splitters
  - Drag-and-drop panel reorganization
  - Tab closable/reopenable via panel picker
  - Layout persistence to MongoDB
  - Minimum sizes enforced (`tabSetMinWidth: 100, tabSetMinHeight: 40`)

### Key Files
- `src/Log4YM.Web/src/store/layoutStore.ts` - Layout state management
- `src/Log4YM.Web/src/App.tsx` - FlexLayout configuration and rendering
- `src/Log4YM.Desktop/main.js` - Electron main process (single BrowserWindow)

### Current Limitations
1. **Global Minimum Sizes:** All tabsets share same minimums (100px width, 40px height)
2. **No Per-Panel Sizing:** Cannot set different minimum sizes per panel type
3. **Single Window Only:** Only one Electron BrowserWindow exists
4. **No Auto-Hide:** Panels must be manually closed
5. **No Monitor Awareness:** Layout doesn't track or respond to monitor changes

## User Requirements Analysis

### Requirement 1: Flexible Panel Sizing
**Request:** "ability to make, say the freq/mode/band to a smaller display"

**Current State:**
- FlexLayout enforces global `tabSetMinWidth: 100, tabSetMinHeight: 40`
- Users can resize via splitters but hit these hard limits
- Rig panel and other info panels feel "empty" with wasted space

**Proposed Solution:**
Enable per-tab minimum sizes using FlexLayout's `config` attribute:
```typescript
{
  type: 'tab',
  name: 'Rig',
  component: 'rig',
  config: {
    minWidth: 200,  // Override global minimum
    minHeight: 80
  }
}
```

**Implementation:**
1. Add `minWidth/minHeight` config to tab definitions in `defaultLayout`
2. Small panels (Header Bar, status info): 150px width min
3. Medium panels (Rig, Cluster): 250px width min
4. Large panels (Log History, Maps): 400px width min
5. Update existing saved layouts via migration function

**Effort:** 🟢 **LOW** (4-8 hours)

### Requirement 2: Auto-Hide After Connection
**Request:** "no real need in looking at the rig and IP address... would be nice for that to auto hide after connected"

**Current State:**
- Rig panel shows connection status, frequency, mode
- No auto-hide capability
- User must manually close the tab

**Proposed Solutions:**

#### Option A: Collapsible Panel (Recommended)
Add a "minimize to border" feature:
- Panel collapses to thin sidebar with icon
- Click icon to expand temporarily
- FlexLayout supports border docking natively

```typescript
// Convert tab to border after connection
model.doAction(
  Actions.moveNode(rigTabId, borderSetId, DockLocation.BOTTOM, 0)
);
```

#### Option B: Auto-Close with Notification
- Close rig panel automatically after successful connection
- Show toast notification: "Rig connected • Click to reopen"
- Add quick-access button in status bar

**Recommendation:** **Option A** - more discoverable and reversible

**Effort:** 🟡 **MEDIUM** (8-16 hours)

### Requirement 3: Multiple Native Windows
**Request:** "flex-layout supports multiple windows"

**Analysis:**
FlexLayout does support "popout" windows, but these are **browser windows** (via `window.open()`), not native OS windows. For Electron, we need to create separate `BrowserWindow` instances.

#### Architecture Approaches

##### Approach A: FlexLayout Popouts → Electron Windows (Recommended)
**How it works:**
1. Enable FlexLayout's `tabEnableFloat: true` in global config
2. User drags tab to "pop out" (FlexLayout's native gesture)
3. Intercept popout event in renderer process
4. Send IPC message to main process
5. Main process creates new `BrowserWindow`
6. New window loads minimal FlexLayout with only that panel
7. IPC keeps panel state synchronized

**Pros:**
- Leverages FlexLayout's existing UX patterns
- Users already understand drag-to-float
- Native OS window management (taskbar, alt-tab, etc.)
- Each window can have different zoom levels
- Works across multiple monitors naturally

**Cons:**
- Complex state synchronization (SignalR connections, settings, etc.)
- Each window needs its own React root
- Memory overhead (multiple renderer processes)
- Layout persistence becomes complex (which panels in which windows?)

**Example Implementation Flow:**
```typescript
// 1. Enable in layoutStore.ts
global: {
  tabEnableFloat: true,
  tabEnableRename: false,
}

// 2. In App.tsx, intercept popout
const onAction = (action: Action) => {
  if (action.type === Actions.FLOAT_TAB) {
    const tabId = action.data.node;
    const component = model.getNodeById(tabId)?.getComponent();

    // Request Electron window via IPC
    window.electronAPI.createPanelWindow({
      panelId: tabId,
      component: component,
      bounds: { width: 800, height: 600 }
    });

    // Prevent default browser window.open()
    return false;
  }
  return true;
};

// 3. In main.js, handle IPC
ipcMain.handle('create-panel-window', (event, { panelId, component, bounds }) => {
  const panelWindow = new BrowserWindow({
    width: bounds.width,
    height: bounds.height,
    webPreferences: {
      preload: path.join(__dirname, 'panel-preload.js')
    }
  });

  // Load with query params to show only this panel
  panelWindow.loadURL(
    `http://localhost:${backendPort}?panel=${component}&mode=standalone`
  );

  // Track window for state sync
  panelWindows.set(panelId, panelWindow);
});
```

**Effort:** 🔴 **HIGH** (40-60 hours)

##### Approach B: Manual Multi-Window System
Build custom window management outside FlexLayout:
- "Window" menu to open panels in new native windows
- Each window is independent BrowserWindow
- No drag-and-drop popouts

**Pros:**
- Simpler than integrating with FlexLayout popouts
- Full control over window lifecycle

**Cons:**
- Less intuitive UX (no drag-to-float)
- Reinventing window management
- Still needs complex state sync

**Effort:** 🔴 **HIGH** (32-48 hours)

#### Recommendation
**Phase 1:** Implement Requirement 1 & 2 (improved sizing and auto-hide)
**Phase 2:** Evaluate user demand before committing to multi-window (large effort)

If multi-window is critical, use **Approach A** (FlexLayout popouts).

### Requirement 4: Monitor Count Change Handling
**Request:** "how do we manage situations when we try startup up with only one monitor when multiple were previously connected?"

**Problem:**
User saves layout with panels on monitor 2, then disconnects monitor 2. On next startup:
- Panel windows may open off-screen (on phantom monitor)
- Panel positions may be invalid
- Layout should gracefully fallback

**Proposed Heuristics:**

#### Rule 1: Window Bounds Validation
Before showing any window:
```javascript
function validateWindowBounds(bounds, displays) {
  const primary = displays.find(d => d.bounds.x === 0 && d.bounds.y === 0);

  // Check if window is visible on any display
  const isVisible = displays.some(display => {
    return bounds.x >= display.bounds.x &&
           bounds.y >= display.bounds.y &&
           bounds.x < display.bounds.x + display.bounds.width &&
           bounds.y < display.bounds.y + display.bounds.height;
  });

  if (!isVisible) {
    // Move to primary display, centered
    return {
      x: primary.bounds.x + (primary.bounds.width - bounds.width) / 2,
      y: primary.bounds.y + (primary.bounds.height - bounds.height) / 2,
      width: bounds.width,
      height: bounds.height
    };
  }

  return bounds;
}
```

#### Rule 2: Monitor Count Fingerprint
Store monitor configuration with layout:
```typescript
interface LayoutMetadata {
  layout: IJsonModel;
  monitorConfig: {
    count: number;
    primary: { width: number; height: number };
    fingerprint: string; // hash of display IDs
  };
  savedAt: string;
}
```

On startup, compare fingerprints:
- **Same fingerprint:** Restore exact positions
- **Different count but same primary:** Restore to primary display
- **Completely different:** Reset to default single-window layout

#### Rule 3: Main Window Protection
Main window always opens on primary display, never off-screen:
```javascript
function createWindow() {
  const displays = screen.getAllDisplays();
  const primary = screen.getPrimaryDisplay();

  const savedBounds = loadSavedWindowBounds();
  const validBounds = validateWindowBounds(savedBounds, displays);

  mainWindow = new BrowserWindow({
    ...validBounds,
    // ... other options
  });
}
```

#### Rule 4: Popout Window Strategy
For multi-window mode (if implemented):

**On Startup:**
1. Count available monitors
2. If popout windows were on now-missing monitors:
   - Option A (Conservative): Don't restore popout windows, merge back to main window
   - Option B (Aggressive): Restore popouts on primary monitor, cascaded

**During Runtime:**
Listen for display changes:
```javascript
screen.on('display-removed', (event, oldDisplay) => {
  // Find windows on removed display
  BrowserWindow.getAllWindows().forEach(win => {
    const bounds = win.getBounds();
    const displays = screen.getAllDisplays();
    const valid = validateWindowBounds(bounds, displays);

    if (valid.x !== bounds.x || valid.y !== bounds.y) {
      // Window was on removed display, move to primary
      win.setBounds(valid);
    }
  });
});

screen.on('display-added', (event, newDisplay) => {
  // Optionally notify user: "New display detected"
  // Could offer to restore previously "lost" windows
});
```

**Effort:** 🟡 **MEDIUM** (16-24 hours for full implementation)

## Implementation Roadmap

### Phase 1: Enhanced Single-Window Experience (Recommended Start)
**Effort:** 12-24 hours
**Risk:** Low
**User Value:** High (addresses 2/4 requirements)

1. **Per-panel minimum sizes** (4-8 hours)
   - Update `defaultLayout` with panel-specific configs
   - Add layout migration for existing users
   - Test with various panel combinations

2. **Auto-hide/minimize panels** (8-16 hours)
   - Add border docking support
   - Implement auto-minimize for rig panel after connection
   - Add toggle button in status bar for minimized panels
   - Persist minimized state

**Deliverables:**
- More compact layout options
- Less wasted space
- Auto-declutter after rig connection

### Phase 2: Monitor-Aware Layout (Optional)
**Effort:** 16-24 hours
**Risk:** Low-Medium
**User Value:** Medium (nice-to-have, prevents edge case bugs)

1. **Window bounds validation** (8-12 hours)
2. **Monitor change detection** (4-6 hours)
3. **Layout metadata tracking** (4-6 hours)

**Deliverables:**
- No more off-screen windows
- Graceful handling of dock/undock scenarios

### Phase 3: Multi-Window Support (Optional, High Effort)
**Effort:** 40-60 hours
**Risk:** High
**User Value:** Medium-High (power users, multi-monitor setups)

1. **FlexLayout popout → Electron window bridge** (16-24 hours)
2. **IPC state synchronization** (12-16 hours)
3. **Multi-window layout persistence** (8-12 hours)
4. **Monitor-aware window restoration** (4-8 hours)

**Deliverables:**
- Panels can pop out to separate OS windows
- Each window independent (drag, resize, taskbar)
- Works across multiple monitors

## Risks & Challenges

### Technical Risks
1. **State Synchronization (Multi-Window):**
   - SignalR connections per window vs shared
   - Settings changes must propagate to all windows
   - Log entry form state coordination
   - **Mitigation:** Use Electron IPC + shared state store (Zustand with sync middleware)

2. **Layout Persistence Complexity:**
   - Currently: Single JSON blob
   - With multi-window: Need to track which panels in which windows
   - **Mitigation:** Version layout schema, support both formats during transition

3. **Memory Overhead:**
   - Each Electron window = separate renderer process
   - React app loaded multiple times
   - **Mitigation:** Accept trade-off, or implement window pooling

### UX Risks
1. **Cognitive Overhead:**
   - Users might not understand multi-window concept
   - Need clear visual feedback during popout
   - **Mitigation:** Tutorial/tooltip on first use, animate popout action

2. **Window Management Chaos:**
   - Too many windows = hard to manage
   - **Mitigation:** Limit popout count (e.g., max 3 secondary windows)

## Recommendations

### For Immediate Implementation (Next Sprint)
✅ **Do:** Phase 1 - Enhanced single-window experience
- Per-panel sizing (4-8 hours)
- Auto-hide/minimize rig panel (8-16 hours)
- Total: ~12-24 hours, high user value, low risk

### For Future Consideration
⏸️ **Defer:** Multi-window support (Phase 3)
- Gather user feedback first: Is this actually needed?
- Survey users: How many have multi-monitor setups?
- Prototype: Build quick demo to validate UX before full implementation
- Only proceed if >30% of users request this feature

✅ **Do Eventually:** Monitor-aware layout (Phase 2)
- Implement after Phase 1 is stable
- Prevents edge case bugs
- Relatively low effort for high reliability improvement

## Alternative: Progressive Enhancement

Instead of full multi-window, consider:

### Panel Focus Mode
- Double-click tab header → panel goes fullscreen
- Escape to return to layout
- Simple, no multi-window complexity
- **Effort:** 4-6 hours

### Secondary Display Helper
- Add "Move to Display 2" context menu item
- Electron moves entire main window to chosen display
- Simpler than per-panel multi-window
- **Effort:** 2-4 hours

## Conclusion

**Recommended Approach:**

1. **Immediate (Sprint 1):** Implement Phase 1 (flexible sizing + auto-hide)
   - Solves user's immediate pain points
   - Low risk, high value
   - Builds foundation for future enhancements

2. **Short-term (Sprint 2-3):** Add monitor-aware layout (Phase 2)
   - Improves reliability
   - Prevents off-screen window bugs
   - Works with or without multi-window

3. **Long-term (Future):** Evaluate multi-window based on user demand
   - Gather data: How many users have multi-monitor setups?
   - Build prototype and test with 5-10 users
   - Only proceed if validated

**Total Effort for Recommended Path (Phase 1 + 2):** 28-48 hours

This approach delivers immediate value while keeping options open for future enhancements.

---

## Appendix: Code Examples

### Example 1: Per-Panel Minimum Sizes
```typescript
// src/Log4YM.Web/src/store/layoutStore.ts
export const defaultLayout: IJsonModel = {
  global: {
    tabEnableFloat: false, // Enable later for multi-window
    tabSetMinWidth: 100,   // Global fallback
    tabSetMinHeight: 40,
  },
  layout: {
    type: 'row',
    children: [
      {
        type: 'tabset',
        children: [
          {
            type: 'tab',
            name: 'Header Bar',
            component: 'header-bar',
            enableClose: false,
            config: {
              minWidth: 150,
              minHeight: 30,
            }
          }
        ]
      },
      {
        type: 'tabset',
        children: [
          {
            type: 'tab',
            name: 'Rig',
            component: 'rig',
            config: {
              minWidth: 250,
              minHeight: 80,
            }
          }
        ]
      }
    ]
  }
};
```

### Example 2: Auto-Minimize After Connection
```typescript
// src/Log4YM.Web/src/plugins/RigPlugin.tsx
useEffect(() => {
  if (isConnected && settings.ui.autoMinimizeRigPanel) {
    // Wait 2 seconds after connection
    const timer = setTimeout(() => {
      // Send message to App to minimize this panel
      window.dispatchEvent(new CustomEvent('minimize-panel', {
        detail: { component: 'rig' }
      }));
    }, 2000);

    return () => clearTimeout(timer);
  }
}, [isConnected, settings.ui.autoMinimizeRigPanel]);
```

### Example 3: Monitor-Aware Window Bounds
```javascript
// src/Log4YM.Desktop/main.js
const { screen } = require('electron');

function getValidatedWindowBounds(savedBounds) {
  const displays = screen.getAllDisplays();
  const primary = screen.getPrimaryDisplay();

  if (!savedBounds) {
    // No saved bounds, use default centered on primary
    return {
      x: primary.bounds.x + 100,
      y: primary.bounds.y + 100,
      width: 1400,
      height: 900
    };
  }

  // Check if any part of window is visible
  const isVisible = displays.some(display => {
    const db = display.bounds;
    const wb = savedBounds;

    return (wb.x + wb.width > db.x &&
            wb.x < db.x + db.width &&
            wb.y + wb.height > db.y &&
            wb.y < db.y + db.height);
  });

  if (!isVisible) {
    log.warn('Saved window position is off-screen, resetting to primary display');
    return {
      x: primary.bounds.x + 100,
      y: primary.bounds.y + 100,
      width: savedBounds.width,
      height: savedBounds.height
    };
  }

  return savedBounds;
}

function createWindow() {
  const savedBounds = loadWindowBounds();
  const validBounds = getValidatedWindowBounds(savedBounds);

  mainWindow = new BrowserWindow({
    ...validBounds,
    minWidth: 800,
    minHeight: 600,
    // ... other options
  });
}
```

## References

- [FlexLayout-React Documentation](https://github.com/caplin/FlexLayout)
- [Electron Multi-Window Patterns](https://www.electronjs.org/docs/latest/api/browser-window)
- [Electron Display Management](https://www.electronjs.org/docs/latest/api/screen)
- [React Portals for Multi-Window](https://pietrasiak.com/creating-multi-window-electron-apps-using-react-portals)
