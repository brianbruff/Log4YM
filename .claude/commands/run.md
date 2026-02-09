---
allowed-tools: Bash(lsof:*), Bash(kill:*), Bash(xargs:*), Bash(cd * && ASPNETCORE_URLS=* dotnet run*), Bash(cd * && BACKEND_PORT=* npm run dev*), Bash(cd * && npm run dev*), Bash(cd * && dotnet run*), Bash(sleep:*)
description: Start the .NET backend and Vite frontend dev servers
---

## Your task

The argument is: $ARGUMENTS

The base ports are **5050** (backend) and **5173** (frontend).

If the argument is a number (e.g. `10`), add it to both base ports. So `/run 10` means backend=5060, frontend=5183. If the argument is empty or not a number, use the base ports as-is.

Kill any existing processes on the calculated ports, then start both dev servers as background tasks.

Run this first to clean up existing processes:

```bash
lsof -ti :<backend_port> | xargs kill 2>/dev/null; lsof -ti :<frontend_port> | xargs kill 2>/dev/null; sleep 1
```

Then start both servers in parallel as background bash tasks:

1. .NET backend: `cd src/Log4YM.Server && ASPNETCORE_URLS=http://localhost:<backend_port> dotnet run`
2. Vite frontend: `cd src/Log4YM.Web && BACKEND_PORT=<backend_port> npm run dev -- --port <frontend_port>`

The `BACKEND_PORT` env var tells the Vite proxy which backend port to forward `/api` and `/hubs` requests to.

Replace `<backend_port>` and `<frontend_port>` with the calculated values.

Do not send any other text besides the tool calls.
