# Log4YM

**Log for Young Men** - A modern, web-based amateur radio logging application built for the next generation of HAM operators.

*Why "Young Men"? It's a playful nod to Log4OM (Log for Old Men) - we're bringing amateur radio logging into the modern web era.*

![Log4YM 3D Globe](docs/images/log4ym-globe.png)

## What is Log4YM?

Log4YM is a fully web-based amateur radio logging platform designed to run on any device with a browser - your shack PC, laptop, tablet, or phone. The server handles all the heavy lifting: connecting to your rig via CAT control, receiving DX cluster spots over UDP, and managing your logbook. You just open a browser and operate.

### Key Concepts

- **Web-First Design**: No desktop app to install. The server runs in your shack (or on a Raspberry Pi) and you connect from any browser, anywhere.

- **Plugin Architecture**: The UI is built from dockable, drag-and-drop panels. Arrange your workspace however you like - log entry, cluster spots, 3D globe, QSO history - all where you want them.

- **Real-Time Everything**: SignalR WebSocket connections mean when a spot comes in, your globe rotates. When you tune your rig, the UI updates. Everything stays in sync.

- **3D Globe Visualization**: Interactive Earth view showing your antenna beam path. Click anywhere on the globe to set your rotator bearing. Watch your coverage pattern in real-time.

## Core Features

| Feature | Description |
|---------|-------------|
| **Log Entry** | Quick QSO logging with automatic callsign lookup (QRZ, HamQTH) |
| **DX Cluster** | Real-time spots via UDP multicast with band/mode filtering |
| **3D Globe** | Interactive globe.gl Earth with beam visualization and click-to-rotate |
| **Log History** | Searchable QSO database with ADIF import/export |
| **CAT Control** | Rig frequency/mode sync via the server |
| **Rotator Control** | Azimuth control integrated with callsign lookups |

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────┐
│                        Browser (Any Device)                  │
│  ┌─────────┐ ┌─────────┐ ┌─────────┐ ┌─────────┐           │
│  │Log Entry│ │ Cluster │ │3D Globe │ │ History │  ...more  │
│  └────┬────┘ └────┬────┘ └────┬────┘ └────┬────┘           │
│       └───────────┴──────┬────┴───────────┘                 │
│                     SignalR WebSocket                        │
└─────────────────────────────┬───────────────────────────────┘
                              │
┌─────────────────────────────┴───────────────────────────────┐
│                    Log4YM Server (.NET 8)                    │
│  ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌──────────┐       │
│  │ QRZ/HQTH │ │  CAT/Rig │ │ Rotator  │ │ Cluster  │       │
│  │  Lookup  │ │  Control │ │  Control │ │UDP Spots │       │
│  └──────────┘ └──────────┘ └──────────┘ └──────────┘       │
│                         MongoDB                              │
└─────────────────────────────────────────────────────────────┘
```

## Getting Started

```bash
# Clone and run with Docker
git clone https://github.com/brianbruff/Log4YM.git
cd Log4YM
docker-compose up -d

# Open http://localhost:5000 in your browser
```

For development:
```bash
cd src/Log4YM.Web && npm install && npm run dev
cd src/Log4YM.Server && dotnet run
```

## Roadmap

- [ ] QRZ/HamQTH callsign lookup integration
- [ ] ADIF import/export
- [ ] Contest mode with serial number exchange
- [ ] Cloudlog sync
- [ ] WSJT-X/JTDX integration
- [ ] Award tracking (DXCC, WAS, IOTA)
- [ ] Mobile-optimized compact view

## Tech Stack

- **Frontend**: React 18, Vite, Tailwind CSS, FlexLayout, globe.gl
- **Backend**: ASP.NET Core 8, SignalR, MongoDB
- **Deployment**: Docker, docker-compose

## Contributing

Pull requests welcome! Check out the [docs/prds](docs/prds/) folder for architecture details and planned features.

## License

MIT

---

*73 de EI2KC*
