const { app, BrowserWindow, Menu, shell, dialog, ipcMain, screen } = require('electron');
const { spawn } = require('child_process');
const path = require('path');
const net = require('net');
const fs = require('fs');
const log = require('electron-log');
const { checkForUpdates } = require('./updater');

// Zoom level persistence using a simple JSON file
const userDataPath = app.getPath('userData');
const zoomConfigPath = path.join(userDataPath, 'zoom-config.json');
const windowStateConfigPath = path.join(userDataPath, 'window-state.json');

function getStoredZoomLevel() {
  try {
    if (fs.existsSync(zoomConfigPath)) {
      const data = fs.readFileSync(zoomConfigPath, 'utf8');
      const config = JSON.parse(data);
      return config.zoomLevel || 0;
    }
  } catch (err) {
    log.warn('Failed to read zoom config:', err.message);
  }
  return 0; // Default zoom level (100%)
}

function saveZoomLevel(level) {
  try {
    const config = { zoomLevel: level };
    fs.writeFileSync(zoomConfigPath, JSON.stringify(config, null, 2), 'utf8');
  } catch (err) {
    log.error('Failed to save zoom config:', err.message);
  }
}

// Set app name for macOS menu bar (must be before ready)
if (process.platform === 'darwin') {
  app.setName('Log4YM');
}

// Enable GPU acceleration and WebGL for 3D globe support
// Some Macs with integrated GPUs may have these disabled by default
app.commandLine.appendSwitch('ignore-gpu-blacklist');
app.commandLine.appendSwitch('enable-gpu-rasterization');
app.commandLine.appendSwitch('enable-zero-copy');
// Ensure WebGL is available - some systems need this explicitly enabled
app.commandLine.appendSwitch('enable-webgl');
app.commandLine.appendSwitch('enable-webgl2-compute-context');

// Configure logging
log.transports.file.level = 'info';
log.transports.console.level = 'debug';

let mainWindow;
let splashWindow;
let backendProcess;
let backendPort;
let isDevMode = process.argv.includes('--dev');
let useViteDevServer = process.argv.includes('--vite') || process.env.VITE_DEV === 'true';
const VITE_DEV_PORT = 5173;

// Multi-window state
const secondaryWindows = new Map(); // windowId -> BrowserWindow
let isAppQuitting = false;

/**
 * Read secondary window bounds from disk
 */
function getWindowStates() {
  try {
    if (fs.existsSync(windowStateConfigPath)) {
      const data = fs.readFileSync(windowStateConfigPath, 'utf8');
      const state = JSON.parse(data);
      return state;
    }
  } catch (err) {
    log.warn('Failed to read window state:', err.message);
  }
  return { secondary: {} };
}

/**
 * Persist secondary window bounds to disk
 */
function saveWindowState(windowId, bounds) {
  try {
    const state = getWindowStates();
    state.secondary = state.secondary || {};
    state.secondary[windowId] = bounds;
    fs.writeFileSync(windowStateConfigPath, JSON.stringify(state, null, 2), 'utf8');
  } catch (err) {
    log.error('Failed to save window state:', err.message);
  }
}

/**
 * Remove a secondary window from persistent state
 */
function removeWindowState(windowId) {
  try {
    const state = getWindowStates();
    if (state.secondary) {
      delete state.secondary[windowId];
      fs.writeFileSync(windowStateConfigPath, JSON.stringify(state, null, 2), 'utf8');
    }
  } catch (err) {
    log.error('Failed to remove window state:', err.message);
  }
}

/**
 * Check whether a window with given bounds overlaps any display by ≥50%
 */
function isWindowVisible(bounds) {
  const displays = screen.getAllDisplays();
  return displays.some(display => {
    const wa = display.workArea;
    const overlapX = Math.max(0, Math.min(bounds.x + bounds.width, wa.x + wa.width) - Math.max(bounds.x, wa.x));
    const overlapY = Math.max(0, Math.min(bounds.y + bounds.height, wa.y + wa.height) - Math.max(bounds.y, wa.y));
    const overlap = overlapX * overlapY;
    const windowArea = bounds.width * bounds.height;
    return windowArea > 0 && (overlap / windowArea) >= 0.5;
  });
}

/**
 * Clamp window bounds to the primary display work area
 */
function clampToPrimaryDisplay(bounds) {
  const primary = screen.getPrimaryDisplay();
  const { workArea } = primary;
  const width = Math.max(bounds.width || 900, 800);
  const height = Math.max(bounds.height || 700, 600);
  const x = Math.max(workArea.x, Math.min((bounds.x != null ? bounds.x : workArea.x + 80), workArea.x + workArea.width - width));
  const y = Math.max(workArea.y, Math.min((bounds.y != null ? bounds.y : workArea.y + 80), workArea.y + workArea.height - height));
  return { x, y, width, height };
}

/**
 * Create a secondary (detached) application window for a given layout slot
 */
function createSecondaryWindow(windowId, savedBounds) {
  if (secondaryWindows.has(windowId)) {
    secondaryWindows.get(windowId).focus();
    return secondaryWindows.get(windowId);
  }

  let bounds;
  if (savedBounds && isWindowVisible(savedBounds)) {
    bounds = savedBounds;
  } else {
    bounds = clampToPrimaryDisplay(savedBounds || {});
  }

  const win = new BrowserWindow({
    x: bounds.x,
    y: bounds.y,
    width: bounds.width,
    height: bounds.height,
    minWidth: 800,
    minHeight: 600,
    title: 'Log4YM',
    icon: path.join(__dirname, 'assets', 'icon.png'),
    webPreferences: {
      nodeIntegration: false,
      contextIsolation: true,
      webSecurity: true,
      preload: path.join(__dirname, 'preload.js'),
    },
    backgroundColor: '#0a0a0f',
  });

  const loadUrl = useViteDevServer
    ? `http://localhost:${VITE_DEV_PORT}?windowId=${windowId}`
    : `http://localhost:${backendPort}?windowId=${windowId}`;

  log.info(`Loading secondary window: ${loadUrl}`);
  win.loadURL(loadUrl);

  win.webContents.setWindowOpenHandler(({ url }) => {
    shell.openExternal(url);
    return { action: 'deny' };
  });

  // Restore zoom level on secondary windows too
  win.once('ready-to-show', () => {
    const savedZoomLevel = getStoredZoomLevel();
    if (savedZoomLevel !== 0) {
      win.webContents.setZoomLevel(savedZoomLevel);
    }
  });

  win.on('close', () => {
    if (isAppQuitting) {
      // Persist bounds for next startup
      saveWindowState(windowId, win.getBounds());
    } else {
      // User explicitly closed this window — don't auto-restore next startup
      removeWindowState(windowId);
    }
  });

  win.on('closed', () => {
    secondaryWindows.delete(windowId);
  });

  secondaryWindows.set(windowId, win);
  return win;
}

/**
 * Restore secondary windows saved from previous session
 */
async function restoreSecondaryWindows() {
  const state = getWindowStates();
  const saved = state.secondary || {};
  const windowIds = Object.keys(saved);
  if (windowIds.length === 0) return;

  log.info(`Restoring ${windowIds.length} secondary window(s)...`);

  for (const windowId of windowIds) {
    try {
      const response = await fetch(`http://localhost:${backendPort}/api/settings/window-layout/${windowId}`);
      if (response.ok) {
        createSecondaryWindow(windowId, saved[windowId]);
        log.info(`Restored secondary window: ${windowId}`);
      } else {
        // Layout was deleted from MongoDB — drop the saved bounds too
        removeWindowState(windowId);
        log.warn(`Secondary window ${windowId} not found in DB, removing from state`);
      }
    } catch (err) {
      log.warn(`Failed to restore secondary window ${windowId}: ${err.message}`);
    }
  }
}

/**
 * Find an available port starting from the given port
 */
async function findAvailablePort(startPort = 5050) {
  return new Promise((resolve, reject) => {
    const server = net.createServer();
    server.listen(startPort, '127.0.0.1', () => {
      const port = server.address().port;
      server.close(() => {
        log.info(`Found available port: ${port}`);
        resolve(port);
      });
    });
    server.on('error', (err) => {
      if (err.code === 'EADDRINUSE') {
        log.debug(`Port ${startPort} in use, trying ${startPort + 1}`);
        resolve(findAvailablePort(startPort + 1));
      } else {
        reject(err);
      }
    });
  });
}

/**
 * Get the path to the backend executable
 */
function getBackendPath() {
  const platform = process.platform;
  const arch = process.arch;

  let runtimeId;
  if (platform === 'win32') {
    runtimeId = arch === 'ia32' ? 'win-x86' : 'win-x64';
  } else if (platform === 'darwin') {
    runtimeId = arch === 'arm64' ? 'osx-arm64' : 'osx-x64';
  } else {
    runtimeId = 'linux-x64';
  }

  const execName = platform === 'win32' ? 'Log4YM.Server.exe' : 'Log4YM.Server';

  if (app.isPackaged) {
    // In packaged app, backend is in resources/backend
    return path.join(process.resourcesPath, 'backend', execName);
  } else {
    // In development, use the published output
    return path.join(__dirname, '..', 'Log4YM.Server', 'bin', 'Release', 'net10.0', runtimeId, 'publish', execName);
  }
}

/**
 * Start the .NET backend process
 */
async function startBackend() {
  if (isDevMode) {
    // In dev mode, assume backend is already running on port 5050
    backendPort = 5050;
    log.info('Dev mode: Using existing backend on port 5050');
    return;
  }

  backendPort = await findAvailablePort();
  const backendPath = getBackendPath();

  log.info(`Starting backend: ${backendPath}`);
  log.info(`Backend port: ${backendPort}`);

  // Check if backend exists
  const fs = require('fs');
  if (!fs.existsSync(backendPath)) {
    const errorMsg = `Backend not found at: ${backendPath}\n\nPlease build the backend first using:\nnpm run build:backend:${process.platform === 'win32' ? 'win' : process.platform === 'darwin' ? 'mac-' + process.arch : 'linux'}`;
    log.error(errorMsg);
    dialog.showErrorBox('Backend Not Found', errorMsg);
    app.quit();
    return;
  }

  const backendDir = path.dirname(backendPath);

  // Make executable on Unix and remove macOS quarantine attribute
  if (process.platform !== 'win32') {
    try {
      fs.chmodSync(backendPath, '755');
    } catch (err) {
      log.warn(`Could not set executable permission: ${err.message}`);
    }
  }

  // Remove macOS quarantine attribute from the backend directory
  // Unsigned apps downloaded from the internet get quarantined by Gatekeeper,
  // which prevents child process binaries from executing
  if (process.platform === 'darwin') {
    const { execSync } = require('child_process');
    try {
      execSync(`xattr -rd com.apple.quarantine "${backendDir}"`, { stdio: 'ignore' });
      log.info('Removed quarantine attribute from backend directory');
    } catch (err) {
      log.warn(`Could not remove quarantine attribute: ${err.message}`);
    }
  }
  backendProcess = spawn(backendPath, [], {
    cwd: backendDir,
    env: {
      ...process.env,
      ASPNETCORE_URLS: `http://localhost:${backendPort}`,
      ASPNETCORE_ENVIRONMENT: app.isPackaged ? 'Production' : 'Development'
    },
    stdio: ['ignore', 'pipe', 'pipe']
  });

  backendProcess.stdout.on('data', (data) => {
    log.info(`[Backend] ${data.toString().trim()}`);
  });

  backendProcess.stderr.on('data', (data) => {
    log.error(`[Backend] ${data.toString().trim()}`);
  });

  backendProcess.on('error', (err) => {
    log.error(`Backend process error: ${err.message}`);
  });

  backendProcess.on('close', (code) => {
    log.info(`Backend process exited with code ${code}`);
  });

  // Wait for backend to be ready
  await waitForBackend();
}

/**
 * Wait for the backend to respond to health checks
 */
async function waitForBackend(retries = 30, delayMs = 1000) {
  log.info('Waiting for backend to be ready...');

  for (let i = 0; i < retries; i++) {
    // Check if the backend process has already exited
    if (backendProcess && backendProcess.exitCode !== null) {
      throw new Error(`Backend process exited with code ${backendProcess.exitCode} before becoming ready. Check the logs for details.`);
    }

    try {
      const response = await fetch(`http://localhost:${backendPort}/api/health`);
      if (response.ok) {
        log.info('Backend is ready!');
        return;
      }
    } catch (err) {
      // Backend not ready yet
    }
    log.debug(`Backend not ready, attempt ${i + 1}/${retries}`);
    await new Promise(resolve => setTimeout(resolve, delayMs));
  }

  throw new Error('Backend failed to start within timeout');
}

/**
 * Create the splash screen window
 */
function createSplashWindow() {
  const imgPath = path.join(__dirname, 'assets', 'splash.webp');

  splashWindow = new BrowserWindow({
    width: 600,
    height: 400,
    frame: false,
    resizable: false,
    transparent: false,
    alwaysOnTop: true,
    center: true,
    backgroundColor: '#0a0e14',
    webPreferences: {
      nodeIntegration: false,
      contextIsolation: true
    }
  });

  const splashPath = path.join(__dirname, 'splash.html');
  const version = app.getVersion();
  splashWindow.loadFile(splashPath, {
    query: { v: version, img: imgPath }
  });

  splashWindow.on('closed', () => {
    splashWindow = null;
  });
}

/**
 * Create the main application window
 */
function createWindow() {
  mainWindow = new BrowserWindow({
    width: 1400,
    height: 900,
    minWidth: 800,
    minHeight: 600,
    title: 'Log4YM',
    icon: path.join(__dirname, 'assets', 'icon.png'),
    webPreferences: {
      nodeIntegration: false,
      contextIsolation: true,
      webSecurity: true,
      preload: path.join(__dirname, 'preload.js')
    },
    backgroundColor: '#0a0a0f',
    show: false // Don't show until ready
  });

  // Clear cache on startup to ensure fresh assets are loaded
  // This prevents stale chunk references after updates
  // Note: This clears HTTP cache but preserves zoom levels and other preferences
  mainWindow.webContents.session.clearCache();

  // Load the appropriate URL based on dev mode
  const loadUrl = useViteDevServer
    ? `http://localhost:${VITE_DEV_PORT}`
    : `http://localhost:${backendPort}`;
  log.info(`Loading URL: ${loadUrl}`);
  mainWindow.loadURL(loadUrl);

  // Open external links in default browser
  mainWindow.webContents.setWindowOpenHandler(({ url }) => {
    shell.openExternal(url);
    return { action: 'deny' };
  });

  // Handle window close
  mainWindow.on('closed', () => {
    mainWindow = null;
  });

  // Save zoom level before window closes
  mainWindow.on('close', () => {
    if (mainWindow) {
      const currentZoom = mainWindow.webContents.getZoomLevel();
      saveZoomLevel(currentZoom);
      log.debug(`Saved zoom level on close: ${currentZoom}`);
    }
  });
}

/**
 * Create the application menu
 */
function createMenu() {
  const isMac = process.platform === 'darwin';

  const template = [
    // App menu (macOS only)
    ...(isMac ? [{
      label: 'Log4YM',
      submenu: [
        {
          label: 'About Log4YM',
          click: () => {
            if (mainWindow) {
              mainWindow.webContents.send('open-about');
            }
          }
        },
        { type: 'separator' },
        {
          label: 'Settings...',
          accelerator: 'CmdOrCtrl+,',
          click: () => {
            if (mainWindow) {
              mainWindow.webContents.send('open-settings');
            }
          }
        },
        { type: 'separator' },
        { role: 'services' },
        { type: 'separator' },
        { label: 'Hide Log4YM', role: 'hide' },
        { role: 'hideOthers' },
        { role: 'unhide' },
        { type: 'separator' },
        { label: 'Quit Log4YM', role: 'quit' }
      ]
    }] : []),
    // File menu
    {
      label: 'File',
      submenu: [
        ...(!isMac ? [{
          label: 'Settings',
          accelerator: 'CmdOrCtrl+,',
          click: () => {
            if (mainWindow) {
              mainWindow.webContents.send('open-settings');
            }
          }
        },
        { type: 'separator' }] : []),
        isMac ? { role: 'close' } : { role: 'quit' }
      ]
    },
    // Edit menu
    {
      label: 'Edit',
      submenu: [
        { role: 'undo' },
        { role: 'redo' },
        { type: 'separator' },
        { role: 'cut' },
        { role: 'copy' },
        { role: 'paste' },
        { role: 'selectAll' }
      ]
    },
    // View menu
    {
      label: 'View',
      submenu: [
        { role: 'reload' },
        { role: 'forceReload' },
        { role: 'toggleDevTools' },
        { type: 'separator' },
        { role: 'resetZoom' },
        { role: 'zoomIn' },
        { role: 'zoomOut' },
        { type: 'separator' },
        { role: 'togglefullscreen' }
      ]
    },
    // Window menu
    {
      label: 'Window',
      submenu: [
        { role: 'minimize' },
        { role: 'zoom' },
        ...(isMac ? [
          { type: 'separator' },
          { role: 'front' }
        ] : [
          { role: 'close' }
        ])
      ]
    },
    // Help menu
    {
      label: 'Help',
      submenu: [
        {
          label: 'Check for Updates...',
          click: async () => {
            await checkForUpdates(false);
          }
        },
        { type: 'separator' },
        {
          label: 'Log4YM on GitHub',
          click: async () => {
            await shell.openExternal('https://github.com/brianbruff/Log4YM');
          }
        },
        { type: 'separator' },
        {
          label: 'Open Logs Folder',
          click: async () => {
            await shell.openPath(app.getPath('logs'));
          }
        }
      ]
    }
  ];

  const menu = Menu.buildFromTemplate(template);
  Menu.setApplicationMenu(menu);
}

/**
 * Cleanup when quitting
 */
function cleanup() {
  if (backendProcess && !backendProcess.killed) {
    log.info('Stopping backend process...');
    backendProcess.kill('SIGTERM');

    // Force kill after timeout
    setTimeout(() => {
      if (backendProcess && !backendProcess.killed) {
        log.warn('Force killing backend process');
        backendProcess.kill('SIGKILL');
      }
    }, 5000);
  }
}

// App event handlers
app.whenReady().then(async () => {
  log.info(`Log4YM Desktop starting (version ${app.getVersion()})`);
  log.info(`Platform: ${process.platform} ${process.arch}`);
  log.info(`Electron: ${process.versions.electron}`);
  log.info(`Packaged: ${app.isPackaged}`);
  log.info(`Dev mode: ${isDevMode}, Vite dev server: ${useViteDevServer}`);

  // Handle restart requests from renderer (e.g. database provider switch)
  ipcMain.handle('restart-app', () => {
    log.info('Restart requested via IPC');
    app.relaunch();
    app.exit(0);
  });

  // Handle zoom level IPC
  ipcMain.handle('get-zoom-level', () => {
    if (mainWindow) {
      return mainWindow.webContents.getZoomLevel();
    }
    return 0;
  });

  ipcMain.handle('set-zoom-level', (event, level) => {
    if (mainWindow) {
      mainWindow.webContents.setZoomLevel(level);
      saveZoomLevel(level);
      log.debug(`Zoom level set to ${level}`);
    }
  });

  // Multi-window IPC: open a secondary window for a given layout slot
  ipcMain.handle('open-secondary-window', (event, windowId) => {
    if (!windowId || typeof windowId !== 'string') {
      log.warn('open-secondary-window: invalid windowId');
      return;
    }
    const state = getWindowStates();
    const savedBounds = (state.secondary && state.secondary[windowId]) || null;
    createSecondaryWindow(windowId, savedBounds);
    log.info(`Opened secondary window: ${windowId}`);
  });

  // Multi-window IPC: close a secondary window
  ipcMain.handle('close-secondary-window', (event, windowId) => {
    const win = secondaryWindows.get(windowId);
    if (win && !win.isDestroyed()) {
      win.close();
    }
  });

  try {
    createSplashWindow();
    await startBackend();
    createMenu();
    createWindow();
    await restoreSecondaryWindows();

    // Close splash and show main window when ready
    mainWindow.once('ready-to-show', () => {
      if (splashWindow) {
        splashWindow.close();
      }
      mainWindow.show();

      // Restore saved zoom level
      const savedZoomLevel = getStoredZoomLevel();
      if (savedZoomLevel !== 0) {
        mainWindow.webContents.setZoomLevel(savedZoomLevel);
        log.info(`Restored zoom level: ${savedZoomLevel}`);
      }
    });

    // Save zoom level when it changes
    mainWindow.webContents.on('zoom-changed', (event, zoomDirection) => {
      const currentZoom = mainWindow.webContents.getZoomLevel();
      saveZoomLevel(currentZoom);
      log.debug(`Zoom changed (${zoomDirection}): ${currentZoom}`);
    });

    // Check for updates after startup (delayed to not slow down launch)
    setTimeout(() => {
      checkForUpdates(true).catch(err => {
        log.warn(`Startup update check failed: ${err.message}`);
      });
    }, 3000);

    // macOS: Re-create window when dock icon clicked
    app.on('activate', () => {
      if (BrowserWindow.getAllWindows().length === 0) {
        createWindow();
      }
    });
  } catch (err) {
    log.error(`Startup error: ${err.message}`);
    if (splashWindow) {
      splashWindow.close();
      splashWindow = null;
    }
    dialog.showErrorBox('Startup Error', err.message);
    app.quit();
  }
});

// Quit when all windows are closed (except on macOS)
app.on('window-all-closed', () => {
  if (process.platform !== 'darwin') {
    app.quit();
  }
});

// Cleanup before quit
app.on('before-quit', () => {
  isAppQuitting = true;
  // Persist bounds of all currently-open secondary windows so they can be restored
  for (const [windowId, win] of secondaryWindows.entries()) {
    if (!win.isDestroyed()) {
      saveWindowState(windowId, win.getBounds());
    }
  }
  cleanup();
});
app.on('will-quit', cleanup);

// Handle uncaught exceptions
process.on('uncaughtException', (err) => {
  log.error(`Uncaught exception: ${err.message}`);
  log.error(err.stack);
});
