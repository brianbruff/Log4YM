# PRD: 3rd Party Plugin Architecture

## 1. Overview

This document proposes an architecture to allow third-party developers to create plugins for Log4YM. The goal is to enable external applications to interact with Log4YM in a secure and documented way, allowing the community to extend the application's functionality.

Currently, Log4YM has two types of "plugins":
- **Backend:** C# classes that are compiled with the server.
- **Frontend:** React components that are built into the web application.

Neither of these is suitable for external, third-party development. This proposal outlines a new approach based on web standards.

## 2. Proposed Architecture

The proposed architecture is based on a client-server model where third-party plugins are external clients that communicate with the Log4YM server over the network.

- **Event Bus (Server -> Plugin):** Plugins will receive real-time events from Log4YM by connecting to its **SignalR WebSocket endpoint**.
- **API (Plugin -> Server):** Plugins will perform actions and retrieve data by calling a **REST API**.

This approach is language-agnostic, allowing plugins to be written in Python, JavaScript/Node.js, C#, or any other language that can handle HTTP requests and WebSockets.

![Architecture Diagram](https://i.imgur.com/example.png)  
*(Note: A real diagram would be created here showing the relationship between Log4YM Server, the WebSocket, the REST API, and an external plugin.)*

## 3. Security: API Key Authentication

The current API and WebSocket endpoints are unsecured, which is a significant risk when opening them up to external applications. We will implement an API Key system to address this.

### 3.1. API Key Management

- A new section in the Log4YM **Settings** page will be created for managing API keys.
- Users will be able to:
    - Generate new API keys.
    - Revoke existing API keys.
    - See a list of their keys (with the key itself masked for security).
- Each API key will be a cryptographically strong, randomly generated string.

### 3.2. Authenticating API Calls

- To make a REST API call, a plugin must include the API key in an `X-Api-Key` HTTP header.
- The Log4YM server will have a new middleware that inspects incoming requests, validates the API key, and rejects any request that doesn't have a valid key (with a `401 Unauthorized` response).

### 3.3. Authenticating WebSocket Connections

- To connect to the SignalR WebSocket, a plugin must pass its API key as a query string parameter.
- **Example Connection URL:** `ws://<log4ym_host>:<port>/loghub?apiKey=YOUR_API_KEY`
- The `LogHub` class on the server will be modified to check for this parameter on connection. If the key is missing or invalid, the connection will be rejected.

## 4. Communication Protocol

### 4.1. Event Bus (WebSocket)

Plugins can listen for a rich set of real-time events. To do so, they will establish a WebSocket connection to the `/loghub` endpoint.

**Available Events (Server -> Plugin):**

This is a partial list of events defined in `ILogHubClient`. The full list should be documented for plugin developers.

- `OnCallsignFocused(CallsignFocusedEvent)`: A user has entered a callsign.
- `OnCallsignLookedUp(CallsignLookedUpEvent)`: QRZ lookup information is available.
- `OnQsoLogged(QsoLoggedEvent)`: A new QSO has been logged.
- `OnSpotReceived(SpotReceivedEvent)`: A new spot has been received from the DX cluster.
- `OnRadioStateChanged(RadioStateChangedEvent)`: The state of a connected radio has changed (frequency, mode, etc.).
- `OnRotatorPosition(RotatorPositionEvent)`: The rotator has moved.

### 4.2. REST API

Plugins can call the REST API to perform actions or query data.

**Key API Endpoints (Plugin -> Server):**

This is a selection of useful endpoints. The full API should be documented (e.g., using Swagger/OpenAPI).

- **QRZ Lookup:**
  - `GET /api/qrz/lookup/{callsign}`: Retrieves QRZ information for a callsign.
- **QSOs:**
  - `GET /api/qsos`: Gets a list of logged QSOs.
  - `POST /api/qsos`: Logs a new QSO.
- **Log Entry / Radio Control:**
  - The `LogHub` class exposes methods that can be invoked by clients (e.g., `FocusCallsign`, `SelectSpot`). We should consider exposing these as REST endpoints as well to provide a consistent API for plugins. For example:
  - `POST /api/log/focus`: Sets the callsign in the log entry panel.
  - `POST /api/radio/tune`: Tunes a connected radio to a specific frequency and mode.

## 5. Example Plugin (Python)

This example shows a simple Python plugin that listens for callsigns, looks up their country, and prints it to the console.

```python
import asyncio
import signal
import websockets
import requests

LOG4YM_HOST = "localhost"
LOG4YM_PORT = 5000 # Adjust to the correct port
API_KEY = "your_generated_api_key"

API_BASE_URL = f"http://{LOG4YM_HOST}:{LOG4YM_PORT}/api"
WEBSOCKET_URL = f"ws://{LOG4YM_HOST}:{LOG4YM_PORT}/loghub?apiKey={API_KEY}"

# The SignalR protocol requires a handshake message
# This is a simplified version. A real implementation should use a proper SignalR client library.
HANDSHAKE = '{"protocol":"json","version":1}\x1e'

async def callsign_lookup(callsign):
    """Calls the Log4YM API to look up a callsign."""
    try:
        headers = {"X-Api-Key": API_KEY}
        response = requests.get(f"{API_BASE_URL}/qrz/lookup/{callsign}", headers=headers)
        response.raise_for_status()
        data = response.json()
        print(f"Callsign: {data['callsign']}, Country: {data['country']}")
    except requests.exceptions.RequestException as e:
        print(f"Error looking up {callsign}: {e}")

async def event_listener():
    """Connects to the WebSocket and listens for events."""
    async with websockets.connect(WEBSOCKET_URL) as websocket:
        # Send the SignalR handshake
        await websocket.send(HANDSHAKE)
        print(await websocket.recv()) # Should be an empty handshake response from server

        print("Connected to Log4YM event stream...")

        while True:
            try:
                message = await websocket.recv()
                # A proper client would parse the SignalR message format
                # For this example, we'll just look for the event name
                if '"target":"OnCallsignFocused"' in str(message):
                    # In a real client, you'd deserialize the JSON payload
                    # This is a simplified way to extract the callsign
                    import json
                    # Strip the trailing terminator character
                    if message.endswith('\x1e'):
                        message = message[:-2]
                    payload = json.loads(message)
                    callsign = payload['arguments'][0]['callsign']
                    print(f"Callsign focused: {callsign}")
                    await callsign_lookup(callsign)

            except websockets.ConnectionClosed:
                print("Connection closed.")
                break

if __name__ == "__main__":
    loop = asyncio.get_event_loop()
    main_task = asyncio.ensure_future(event_listener())
    for sig in [signal.SIGINT, signal.SIGTERM]:
        loop.add_signal_handler(sig, main_task.cancel)
    try:
        loop.run_until_complete(main_task)
    finally:
        loop.close()
```

## 6. Implementation Plan

1.  **Backend: API Key Service**
    - Create a service to generate, store, and validate API keys.
    - Add a new repository for storing API keys securely (e.g., hashed in the database).
    - Create a new API controller (`/api/apikeys`) for managing keys from the UI.

2.  **Backend: Authentication Middleware**
    - Create a new ASP.NET Core middleware to inspect the `X-Api-Key` header on API requests.
    - Modify `LogHub.cs` to validate the `apiKey` query parameter during the `OnConnectedAsync` event.

3.  **Frontend: API Key Management UI**
    - Create a new settings page or a new section in the existing settings panel.
    - Develop React components for displaying, generating, and revoking API keys.

4.  **Documentation**
    - Create a developer-facing document in the `docs` folder detailing the WebSocket events and REST API endpoints.
    - The document should include examples in at least Python and JavaScript.
