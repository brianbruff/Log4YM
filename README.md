# Log4YM

**Log for Young Men** - A modern amateur radio logging application with desktop and web interfaces.

> **Alpha Software** - Log4YM is under active development. Features may change and bugs are expected. Use at your own risk and always keep backups of your log data.

---

## 📚 [Read the Full Documentation on the Wiki](https://github.com/brianbruff/Log4YM/wiki)

**For detailed guides, advanced features, and the latest documentation, visit the [Log4YM Wiki](https://github.com/brianbruff/Log4YM/wiki).**

---

<img width="2045" height="1211" alt="image" src="https://github.com/user-attachments/assets/c2ff987e-e7bd-456f-bd77-7ae0376b6fdb" />


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
xattr -cr /Applications/Log4YM.app
```

Then open the app normally. If you still get a security warning, go to **System Preferences > Security & Privacy** and click **Open Anyway**.

**Important:** For detailed information about troubleshooting macOS security settings, see the [Wiki](https://github.com/brianbruff/Log4YM/wiki).

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

Open http://localhost:5050 in your browser.

---

### Option 3: Run from Source

#### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
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

The backend runs on http://localhost:5050, frontend dev server on http://localhost:5173.

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

MongoDB Atlas is a free cloud database that lets you sync your logs and settings across all your machines.

### Quick Setup

1. Go to [mongodb.com/cloud/atlas](https://www.mongodb.com/cloud/atlas) and create a free account
2. Create a free **M0 Sandbox** cluster
3. Set up a database user (username/password)
4. Add your IP address to the access list (or use `0.0.0.0/0` for access from anywhere)
5. Get your connection string (looks like `mongodb+srv://username:password@cluster0.xxxxx.mongodb.net/`)

### Configure Log4YM

**For Desktop App:** Use the setup wizard on first launch to enter your MongoDB connection string.

**For Docker:** Edit `docker-compose.yml`:

```yaml
environment:
  - MongoDB__ConnectionString=mongodb+srv://username:password@cluster0.xxxxx.mongodb.net/?retryWrites=true&w=majority
  - MongoDB__DatabaseName=Log4YM
```

**For running from source:** Edit `src/Log4YM.Server/appsettings.json`:

```json
{
  "MongoDB": {
    "ConnectionString": "mongodb+srv://username:password@cluster0.xxxxx.mongodb.net/?retryWrites=true&w=majority",
    "DatabaseName": "Log4YM"
  }
}
```

**For detailed MongoDB Atlas setup instructions, visit the [Wiki](https://github.com/brianbruff/Log4YM/wiki).**

---

## Configuration

Open the Settings panel in Log4YM to configure your station and hardware:

- **Station Settings:** Callsign, grid square, QRZ login
- **Radio Control:** FlexRadio SmartSDR, TCI radio integration
- **Rotator Control:** Hamlib rotctld support
- **Hardware Plugins:** 4O3A Antenna Genius, Elecraft PGXL amplifier

**For detailed configuration guides, including rotator setup, remote access, and hardware integration, visit the [Wiki](https://github.com/brianbruff/Log4YM/wiki).**

---

## Troubleshooting

**Common Issues:**

- **3D Globe not working on remote machines?** Use HTTPS instead of HTTP (see Wiki for details)
- **Can't connect to MongoDB Atlas?** Verify your IP is whitelisted and the connection string is correct
- **Rotator not responding?** Check that rotctld is running and configured correctly
- **No DX Cluster spots?** Verify your network allows outbound TCP connections

**For comprehensive troubleshooting guides, visit the [Wiki](https://github.com/brianbruff/Log4YM/wiki).**

---

## License

MIT

---

*73 de EI6LF*
