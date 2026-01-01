const { app, dialog, shell } = require('electron');
const log = require('electron-log');

const GITHUB_REPO = 'brianbruff/Log4YM';
const RELEASES_URL = `https://api.github.com/repos/${GITHUB_REPO}/releases/latest`;
const RELEASES_PAGE = `https://github.com/${GITHUB_REPO}/releases/latest`;

/**
 * Compare two semver version strings
 * Returns: 1 if v1 > v2, -1 if v1 < v2, 0 if equal
 */
function compareVersions(v1, v2) {
  // Strip leading 'v' if present
  const clean1 = v1.replace(/^v/, '');
  const clean2 = v2.replace(/^v/, '');

  const parts1 = clean1.split('.').map(Number);
  const parts2 = clean2.split('.').map(Number);

  for (let i = 0; i < Math.max(parts1.length, parts2.length); i++) {
    const num1 = parts1[i] || 0;
    const num2 = parts2[i] || 0;

    if (num1 > num2) return 1;
    if (num1 < num2) return -1;
  }

  return 0;
}

/**
 * Fetch the latest release info from GitHub
 */
async function getLatestRelease() {
  const response = await fetch(RELEASES_URL, {
    headers: {
      'Accept': 'application/vnd.github.v3+json',
      'User-Agent': 'Log4YM-Desktop'
    }
  });

  if (!response.ok) {
    throw new Error(`GitHub API returned ${response.status}`);
  }

  return response.json();
}

/**
 * Check for updates and optionally show dialog
 * @param {boolean} silent - If true, only show dialog when update is available
 * @returns {Promise<{updateAvailable: boolean, latestVersion: string | null}>}
 */
async function checkForUpdates(silent = true) {
  const currentVersion = app.getVersion();
  log.info(`Checking for updates... Current version: ${currentVersion}`);

  try {
    const release = await getLatestRelease();
    const latestVersion = release.tag_name; // e.g., "v1.6.0"
    const latestClean = latestVersion.replace(/^v/, '');

    log.info(`Latest version on GitHub: ${latestVersion}`);

    const comparison = compareVersions(latestClean, currentVersion);

    if (comparison > 0) {
      // New version available
      log.info(`Update available: ${currentVersion} → ${latestClean}`);

      const result = await dialog.showMessageBox({
        type: 'info',
        title: 'Update Available',
        message: `A new version of Log4YM is available!`,
        detail: `Current version: ${currentVersion}\nLatest version: ${latestClean}\n\n${release.name || ''}\n\nWould you like to download it?`,
        buttons: ['Download', 'Later'],
        defaultId: 0,
        cancelId: 1
      });

      if (result.response === 0) {
        // Open releases page in browser
        await shell.openExternal(RELEASES_PAGE);
      }

      return { updateAvailable: true, latestVersion: latestClean };
    } else {
      // No update available
      log.info('No update available - running latest version');

      if (!silent) {
        await dialog.showMessageBox({
          type: 'info',
          title: 'No Updates',
          message: 'You are running the latest version',
          detail: `Current version: ${currentVersion}`,
          buttons: ['OK']
        });
      }

      return { updateAvailable: false, latestVersion: latestClean };
    }
  } catch (err) {
    log.error(`Update check failed: ${err.message}`);

    if (!silent) {
      await dialog.showMessageBox({
        type: 'error',
        title: 'Update Check Failed',
        message: 'Could not check for updates',
        detail: `Please check your internet connection and try again.\n\nError: ${err.message}`,
        buttons: ['OK']
      });
    }

    return { updateAvailable: false, latestVersion: null };
  }
}

module.exports = {
  checkForUpdates,
  compareVersions
};
