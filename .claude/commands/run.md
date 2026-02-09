---
allowed-tools: Bash(lsof:*), Bash(kill:*), Bash(xargs:*), Bash(cd * && dotnet run*), Bash(cd * && npm run dev*), Bash(sleep:*)
description: Start the .NET backend and Vite frontend dev servers
---

## Your task

Kill any existing processes on ports 5060 and 5183, then start both dev servers as background tasks.

Run this first to clean up existing processes:

```bash
lsof -ti :5060 | xargs kill 2>/dev/null; lsof -ti :5183 | xargs kill 2>/dev/null; sleep 1
```

Then start both servers in parallel as background bash tasks:

1. .NET backend: `cd src/Log4YM.Server && ASPNETCORE_URLS=http://localhost:5060 dotnet run`
2. Vite frontend: `cd src/Log4YM.Web && npm run dev`

Do not send any other text besides the tool calls.
