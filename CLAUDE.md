# CLAUDE.md

This file provides guidance to Claude Code when working with this repository.

## Development Commands

### Running with Hot Reload (Recommended for Development)

Run these three commands in separate terminals:

```bash
# Terminal 1: .NET Backend API
cd src/Log4YM.Server
dotnet run

# Terminal 2: Vite Dev Server (hot reload for frontend)
cd src/Log4YM.Web
npm run dev

# Terminal 3: Electron Desktop App
cd src/Log4YM.Desktop
npm run dev:vite
```

This setup provides instant hot reload - any changes to frontend files are reflected immediately without rebuilding.

### Running without Hot Reload

If you need to test the production build flow:

```bash
# Build frontend and copy to server wwwroot
cd src/Log4YM.Web
npm run build
cp -r dist/* ../Log4YM.Server/wwwroot/

# Run backend (serves static files)
cd src/Log4YM.Server
dotnet run

# Run Electron pointing to backend
cd src/Log4YM.Desktop
npm run dev
```

### Building for Distribution

```bash
cd src/Log4YM.Desktop

# Build for current platform
npm run package:mac    # macOS (builds both x64 and arm64)
npm run package:win    # Windows
npm run package:linux  # Linux

# Build for all platforms
npm run package:all
```

## Architecture Overview

- **Log4YM.Web**: React frontend using Vite, FlexLayout for panels, SignalR for real-time updates
- **Log4YM.Server**: .NET 10 backend API with SignalR hubs, DX cluster integration
- **Log4YM.Desktop**: Electron wrapper that loads the web app
- **Log4YM.Contracts**: Shared DTOs and contracts

## Key URLs in Development

- Vite Dev Server: http://localhost:5173 (frontend with HMR)
- .NET Backend: http://localhost:5050 (API and SignalR hubs)
- Vite proxies `/api` and `/hubs` requests to the backend automatically

## Git Commit Instructions

- Never push unless instructed
s