# Log4YM

**Log for Young Men** - A modern amateur radio logging application with desktop and web interfaces.

> **Alpha Software** - Log4YM is under active development. Features may change and bugs are expected. Use at your own risk and always keep backups of your log data.

![Log4YM 3D Globe](docs/images/log4ym-globe2.png)

## What is Log4YM?

Log4YM is an amateur radio logger available as a desktop app (Windows, macOS, Linux) or web server. Your logs and settings sync automatically across all your devices via MongoDB.

### Key Features

- **3D Globe & 2D Map** - Visualize contacts and control your rotator with a click
- **Real-Time DX Cluster** - Live spots with band/mode filtering
- **CAT Control** - FlexRadio SmartSDR and TCI radio integration
- **Rotator Control** - Hamlib rotctld support with presets
- **Hardware Integration** - 4O3A Antenna Genius, Elecraft PGXL amplifier
- **Cloud Sync** - All settings, layouts, and logs sync across machines via MongoDB Atlas

---

## Installation

### Option 1: Desktop App (Recommended)

Download the latest release for your platform from the [Releases](https://github.com/brianbruff/Log4YM/releases) page.

| Platform | Download | Notes |
|----------|----------|-------|
| **Windows** | `Log4YM-Setup-x.x.x.exe` | Run the installer |
| **macOS (Apple Silicon)** | `Log4YM-x.x.x-arm64.dmg` | See macOS instructions below |
| **macOS (Intel)** | `Log4YM-x.x.x-x64.dmg` | See macOS instructions below |
| **Linux** | `Log4YM-x.x.x.AppImage` | Make executable and run |

#### macOS Installation (Unsigned App)

The macOS builds are not signed with an Apple Developer certificate. You'll need to remove the quarantine attribute before running:

```bash
# After mounting the DMG and copying Log4YM.app to Applications:
xattr -cr /Applications/Log4YM.app
```

Then open the app normally. If you still get a security warning, go to **System Preferences > Security & Privacy** and click **Open Anyway**.

#### First Run - Setup Wizard

On first launch, Log4YM will display a setup wizard to configure your MongoDB connection. You can use:
- **MongoDB Atlas** (free cloud tier) - Sync across all your devices
- **Local MongoDB** - For offline-only use

---

### Option 2: Docker

```bash
git clone https://github.com/brianbruff/Log4YM.git
cd Log4YM
docker-compose up -d
```

Open http://localhost:5000 in your browser.

---

### Option 3: Run from Source

#### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Node.js 18+](https://nodejs.org/)

#### Development Mode (Hot Reload)

Run the backend and frontend separately for development with hot reload:

```bash
# Terminal 1 - Start the .NET backend
cd src/Log4YM.Server
dotnet run

# Terminal 2 - Start the React frontend dev server
cd src/Log4YM.Web
npm install
npm run dev

# Terminal 3 - (Optional) Start Electron shell in dev mode
cd src/Log4YM.Desktop
npm install
npm run dev
```

The backend runs on http://localhost:5000, frontend dev server on http://localhost:5173.

#### Build Desktop App from Source

```bash
cd src/Log4YM.Desktop
npm install
cd ../Log4YM.Web && npm install && cd ../Log4YM.Desktop

# Build for your platform:
npm run package:mac      # macOS (builds both arm64 and x64)
npm run package:win      # Windows
npm run package:linux    # Linux

# Or build all platforms:
npm run package:all
```

Built packages are output to `src/Log4YM.Desktop/dist/`.

#### Individual Build Steps

```bash
# Build frontend only
npm run build:frontend

# Build backend for specific platform
npm run build:backend:mac-arm64   # macOS Apple Silicon
npm run build:backend:mac-x64     # macOS Intel
npm run build:backend:win         # Windows
npm run build:backend:linux       # Linux

# Run packaged Electron app locally (after building backend)
npm start
```

---

## Setting Up MongoDB Atlas (Cloud Sync)

MongoDB Atlas is a free cloud database that lets you sync your logs and settings across all your machines. When you log a QSO on your shack PC, it instantly appears on your laptop or phone.

### Step 1: Create a MongoDB Atlas Account

1. Go to [mongodb.com/cloud/atlas](https://www.mongodb.com/cloud/atlas)
2. Click **Try Free** and create an account
3. Choose the **FREE M0 Sandbox** tier (no credit card required)

### Step 2: Create a Cluster

1. Click **Build a Database**
2. Select **M0 FREE** tier
3. Choose a cloud provider and region close to you
4. Click **Create Deployment**

### Step 3: Set Up Database Access

1. Create a database user:
   - Username: `log4ym` (or your choice)
   - Password: Generate a secure password and **save it**
   - Click **Create User**

2. Add your IP address:
   - Click **Add My Current IP Address** or
   - Enter `0.0.0.0/0` to allow access from anywhere (less secure but convenient for remote operation)
   - Click **Add Entry**

### Step 4: Get Your Connection String

1. Click **Connect** on your cluster
2. Select **Drivers**
3. Copy the connection string, it looks like:
   ```
   mongodb+srv://log4ym:<password>@cluster0.xxxxx.mongodb.net/?retryWrites=true&w=majority
   ```
4. Replace `<password>` with your actual password

### Step 5: Configure Log4YM

**For Docker:** Edit `docker-compose.yml`:

```yaml
environment:
  - MongoDB__ConnectionString=mongodb+srv://log4ym:YOUR_PASSWORD@cluster0.xxxxx.mongodb.net/?retryWrites=true&w=majority
  - MongoDB__DatabaseName=Log4YM
```

**For running from source:** Edit `src/Log4YM.Server/appsettings.json`:

```json
{
  "MongoDB": {
    "ConnectionString": "mongodb+srv://log4ym:YOUR_PASSWORD@cluster0.xxxxx.mongodb.net/?retryWrites=true&w=majority",
    "DatabaseName": "Log4YM"
  }
}
```

Restart Log4YM and your data will now sync to the cloud.

### What Syncs Across Machines?

| Data | Description |
|------|-------------|
| **QSO Logs** | All your contacts |
| **Settings** | Station info, callsign, grid square, QRZ credentials |
| **Layouts** | Your custom panel arrangements |
| **Rotator Presets** | Saved beam headings |

---

## Configuration

### Station Settings

Open the Settings panel in Log4YM to configure:

- **Callsign** - Your amateur radio callsign
- **Grid Square** - Your Maidenhead locator (e.g., IO52RN)
- **QRZ Login** - For callsign lookups (requires QRZ XML subscription)

### Hardware Connections

| Device | Default Port | Configuration |
|--------|--------------|---------------|
| Hamlib rotctld | TCP 4533 | Settings > Rotator |
| FlexRadio | SmartSDR API | Auto-discovered |
| TCI Radio | TCP 40001 | Settings > Radio |
| Antenna Genius | TCP 6800 | Plugin settings |
| PGXL Amplifier | TCP 50000 | Plugin settings |

---

## Remote Access & HTTPS

When accessing Log4YM from another machine on your network (e.g., `http://192.168.1.100:5173`), the 3D Globe may fail to load. This is because browsers require a **secure context** for WebGL features, and HTTP over IP addresses is considered insecure.

### Solution: Enable HTTPS for Remote Access

**For development (Vite dev server):**

```bash
# Default - HTTP for localhost development
npm run dev

# HTTPS mode - for remote access with 3D Globe
VITE_HTTPS=true npm run dev
```

Then access via `https://192.168.1.100:5173` (accept the self-signed certificate warning).

**For Docker/Production:**

The server listens on both HTTP (5000) and HTTPS (5001). Access via HTTPS for remote clients that need the 3D Globe.

### Why is this needed?

- `localhost` is always treated as a secure context, even over HTTP
- IP addresses over HTTP are **not** secure contexts
- WebGL/Three.js (used by the 3D Globe) requires secure context features
- HTTPS provides the secure context needed for WebGL to work properly

---

## Troubleshooting

**3D Globe shows "WebGL Not Available" on remote machines?**
- Use HTTPS instead of HTTP (see Remote Access section above)
- Or access via `localhost` using SSH tunneling: `ssh -L 5173:localhost:5173 user@server-ip`

**Can't connect to MongoDB Atlas?**
- Check your IP is whitelisted in Atlas Network Access
- Verify the connection string password is correct
- Ensure your firewall allows outbound connections on port 27017

**Rotator not responding?**
- Verify rotctld is running: `rotctld -m 202 -r /dev/ttyUSB0`
- Check the IP and port in Settings > Rotator
- Test with: `echo "p" | nc localhost 4533`

**No DX Cluster spots?**
- The server connects to a cluster automatically
- Check your network allows outbound TCP connections

---

## License

MIT

---

*73 de EI6LF*
