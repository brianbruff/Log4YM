const { app, BrowserWindow, Menu, shell, dialog } = require('electron');
const { spawn } = require('child_process');
const path = require('path');
const net = require('net');
const log = require('electron-log');
const { checkForUpdates } = require('./updater');

// Set app name for macOS menu bar (must be before ready)
if (process.platform === 'darwin') {
  app.setName('Log4YM');
}

// Configure logging
log.transports.file.level = 'info';
log.transports.console.level = 'debug';

let mainWindow;
let backendProcess;
let backendPort;
let isDevMode = process.argv.includes('--dev');

/**
 * Find an available port starting from the given port
 */
async function findAvailablePort(startPort = 5000) {
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
    runtimeId = 'win-x64';
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
    return path.join(__dirname, '..', 'Log4YM.Server', 'bin', 'Release', 'net8.0', runtimeId, 'publish', execName);
  }
}

/**
 * Start the .NET backend process
 */
async function startBackend() {
  if (isDevMode) {
    // In dev mode, assume backend is already running on port 5000
    backendPort = 5000;
    log.info('Dev mode: Using existing backend on port 5000');
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

  // Make executable on Unix
  if (process.platform !== 'win32') {
    try {
      fs.chmodSync(backendPath, '755');
    } catch (err) {
      log.warn(`Could not set executable permission: ${err.message}`);
    }
  }

  const backendDir = path.dirname(backendPath);
  backendProcess = spawn(backendPath, [], {
    cwd: backendDir,
    env: {
      ...process.env,
      ASPNETCORE_URLS: `http://localhost:${backendPort}`,
      ASPNETCORE_ENVIRONMENT: 'Development'
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
    dialog.showErrorBox('Backend Error', `Failed to start backend: ${err.message}`);
  });

  backendProcess.on('close', (code) => {
    log.info(`Backend process exited with code ${code}`);
    if (code !== 0 && code !== null) {
      dialog.showErrorBox('Backend Crashed', `The backend process exited unexpectedly with code ${code}`);
    }
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
      webSecurity: true
    },
    backgroundColor: '#0a0a0f',
    show: false // Don't show until ready
  });

  // Load the backend URL
  mainWindow.loadURL(`http://localhost:${backendPort}`);

  // Show window when ready
  mainWindow.once('ready-to-show', () => {
    mainWindow.show();
  });

  // Open external links in default browser
  mainWindow.webContents.setWindowOpenHandler(({ url }) => {
    shell.openExternal(url);
    return { action: 'deny' };
  });

  // Handle window close
  mainWindow.on('closed', () => {
    mainWindow = null;
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
        { label: 'About Log4YM', role: 'about' },
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

  try {
    await startBackend();
    createMenu();
    createWindow();

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
app.on('before-quit', cleanup);
app.on('will-quit', cleanup);

// Handle uncaught exceptions
process.on('uncaughtException', (err) => {
  log.error(`Uncaught exception: ${err.message}`);
  log.error(err.stack);
});
