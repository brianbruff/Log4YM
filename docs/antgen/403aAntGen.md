# 4O3A Antenna Genius 8x2 Integration PRD

## Executive Summary

This document outlines the integration of the 4O3A Antenna Genius 8x2 antenna switch into Log4YM. The Antenna Genius is a network-enabled 8x2 antenna switch that allows two radios (A and B) to independently select from up to 8 configured antennas. This integration will provide real-time antenna status display and allow users to manually select antennas for each radio port directly from the Log4YM interface.

## Background

### What is the 4O3A Antenna Genius 8x2?

The Antenna Genius 8x2 is a high-isolation, network-enabled antenna switch manufactured by 4O3A Signature. Key features:

- **8 Antenna Ports**: Supports up to 8 different antennas
- **2 Radio Ports**: Two independent radio connections (A and B)
- **Network Control**: Full TCP/IP control via port 9007
- **Auto Discovery**: UDP broadcast discovery on port 9007
- **Real-time Status**: Asynchronous status updates via subscriptions
- **Band Awareness**: Can automatically switch antennas based on band/frequency

### Problem Statement

Ham radio operators using the Antenna Genius currently need to use the separate Windows utility or manually configure their antenna selection. Integration with Log4YM will provide:

1. Single-pane-of-glass control for all station equipment
2. Visual indication of current antenna selection for both radios
3. Quick antenna switching without leaving the logging application

## Technical Research

### API Documentation Reference

The complete API documentation has been cloned to `docs/antgen/wiki/` from the official [4O3A Genius API Docs](https://github.com/4o3a/genius-api-docs/wiki).

### Network Protocol Overview

#### Discovery Protocol (UDP 9007)

The Antenna Genius broadcasts its presence on the local network every 1 second via UDP port 9007:

```
AG ip=192.168.1.39 port=9007 v=4.0.22 serial=9A-3A-DC name=Ranko_4O3A ports=2 antennas=8 mode=master uptime=3034
```

Key parameters:
- `ip`: Device IP address
- `port`: TCP port for commands (always 9007)
- `v`: Firmware version
- `serial`: Unique device identifier
- `name`: User-defined device name
- `ports`: Number of radio ports (2 for 8x2)
- `antennas`: Number of antenna ports (8 for 8x2)
- `mode`: Stack mode (master/slave)

#### TCP/IP Command Protocol (TCP 9007)

**Connection Prologue:**
Upon connecting, the device sends: `V<version> AG[ AUTH]`
- Example: `V4.0.22 AG`
- `AUTH` suffix indicates WAN connection requiring authentication

**Command Format:**
```
C<seq_number>|command<CR>
```
- `seq_number`: 1-255, used to match responses to commands

**Response Format:**
```
R<seq_number>|<hex_response>|<message>
```
- `hex_response`: 0 = success, non-zero = error code

**Status Format (asynchronous):**
```
S0|<message>
```

### Key Commands for Integration

#### 1. Get Antenna List
```
C1|antenna list
```
Response:
```
R1|0|antenna 1 name=Dummy_Load_1 tx=0000 rx=0001 inband=0000
R1|0|antenna 2 name=Dummy_Load_2 tx=0000 rx=0001 inband=0000
R1|0|antenna 3 name=EFHW_Dipole tx=0000 rx=0001 inband=0000
...
R1|0|
```

Parameters:
- `name`: Custom antenna name (underscore for spaces)
- `tx`: TX band mask (hex) - which bands can transmit on this antenna
- `rx`: RX band mask (hex) - which bands can receive on this antenna
- `inband`: Inband mask (hex)

#### 2. Get Radio Port Status
```
C2|port get 1    # Port A
C3|port get 2    # Port B
```
Response:
```
R2|0|port 1 auto=1 source=AUTO band=5 rxant=3 txant=3 tx=0 inhibit=0
```

Parameters:
- `auto`: Auto band detection enabled
- `source`: Band source (AUTO, MANUAL, FLEX, etc.)
- `band`: Current band index (0-15)
- `rxant`: Selected RX antenna (0 = none, 1-8 = antenna number)
- `txant`: Selected TX antenna (0 = none, 1-8 = antenna number)
- `tx`: Currently transmitting (0/1)
- `inhibit`: Port is inhibited from transmitting

#### 3. Set Antenna for Radio Port
```
C4|port set 1 rxant=3 txant=3    # Set Port A to antenna 3
C5|port set 2 rxant=5 txant=5    # Set Port B to antenna 5
```

#### 4. Subscribe to Status Updates
```
C6|sub port all    # Subscribe to all port changes
C7|sub antenna     # Subscribe to antenna config changes
C8|sub relay       # Subscribe to relay state changes
```

After subscription, status messages are received asynchronously:
```
S0|port 1 auto=1 source=AUTO band=5 rxant=3 txant=3 tx=0 inhibit=0
S0|relay tx=00 rx=04 state=04
```

#### 5. Keepalive
```
C9|ping
```
Used to maintain connection and prevent timeout.

### Error Codes
```
0x00  - OK (success)
0x01  - Invalid command format
0x10  - Unknown command
0x20  - Invalid command parameters
0x30  - Invalid subscription object
0xFF  - Client not authorized
```

## Objectives

### Primary Goals

1. **Device Discovery**: Auto-discover Antenna Genius devices on the local network
2. **Status Display**: Show current antenna selection for Radio A and Radio B
3. **Manual Control**: Allow users to click and select antennas for each radio
4. **Real-time Updates**: Subscribe to status changes and update UI in real-time

### Non-Goals (Future Considerations)

- Band-based automatic antenna switching (relies on rig integration)
- Antenna configuration (name, band masks)
- Multiple Antenna Genius support (stacked configurations)
- FlexRadio integration features

## User Interface Design

### Antenna Genius Panel

Based on the Windows utility design, the panel will display:

```
+----------------------------------+
|        Antenna Genius            |
|----------------------------------|
|    FLEX        |      FLEX       |
|    12m         |      None       |
|----------------------------------|
|  A [Dummy Load 1          ] B    |
|  A [Dummy Load 2          ] B    |
|  A [EFHW Dipole        *  ] B    |  <- * indicates selected
|  A [Spare antenna         ] B    |
|  ...                             |
+----------------------------------+
```

Key UI Elements:
- **Header**: Device name and connection status
- **Band Indicator**: Current band for each radio (from port status)
- **Antenna List**: All configured antennas (skip unconfigured ones)
- **Selection Buttons**: A and B buttons for each antenna row
  - Highlighted when antenna is selected for that radio
  - Click to select antenna for that radio

### Color Scheme

Following the existing Log4YM dark theme:
- Selected antenna for A: Blue highlight (accent-primary)
- Selected antenna for B: Green highlight (accent-secondary)
- Antenna name: White text
- Unconfigured/disabled: Gray/dimmed

## Implementation Plan

### Phase 1: Backend Service

#### 1.1 Create Antenna Genius Service (`AntennaGeniusService.cs`)

```csharp
public class AntennaGeniusService : BackgroundService
{
    // UDP discovery listener
    // TCP command connection
    // Status subscription management
    // SignalR event broadcasting
}
```

Responsibilities:
- Listen for UDP discovery broadcasts
- Maintain TCP connection to device
- Parse command responses and status updates
- Broadcast changes via SignalR

#### 1.2 Data Models (`AntennaGeniusModels.cs`)

```csharp
public record AntennaInfo(int Id, string Name, ushort TxMask, ushort RxMask);
public record PortStatus(int PortId, int Band, int RxAnt, int TxAnt, bool IsTx);
public record AntennaGeniusStatus(
    string DeviceName,
    string IpAddress,
    string Version,
    bool Connected,
    List<AntennaInfo> Antennas,
    PortStatus PortA,
    PortStatus PortB
);
```

#### 1.3 SignalR Events

Add to `ILogHubClient`:
```csharp
Task OnAntennaGeniusStatus(AntennaGeniusStatusEvent evt);
Task OnAntennaGeniusPortChanged(AntennaGeniusPortChangedEvent evt);
```

Add hub methods:
```csharp
Task SelectAntenna(int portId, int antennaId);
```

### Phase 2: Frontend Plugin

#### 2.1 Create `AntennaGeniusPlugin.tsx`

React component implementing:
- Connection status indicator
- Antenna list with selection buttons
- Real-time status updates via SignalR
- Click handlers for antenna selection

#### 2.2 State Management

Add to `appStore.ts`:
```typescript
interface AntennaGeniusState {
  connected: boolean;
  deviceName: string;
  antennas: AntennaInfo[];
  portA: PortStatus;
  portB: PortStatus;
}
```

#### 2.3 SignalR Integration

Add event handlers in `useSignalR.ts`:
- `OnAntennaGeniusStatus`
- `OnAntennaGeniusPortChanged`

### Phase 3: Configuration

#### 3.1 Settings

- Enable/disable Antenna Genius integration
- Manual IP address override (optional, uses discovery by default)
- Device selection (if multiple discovered)

## API Sequence Diagrams

### Connection Sequence

```
Log4YM Server                    Antenna Genius
      |                                |
      |<---- UDP Discovery (1/sec) ----|
      |                                |
      |---- TCP Connect :9007 -------->|
      |<---- V4.0.22 AG ---------------|
      |                                |
      |---- C1|antenna list ---------->|
      |<---- R1|0|antenna 1 name=... --|
      |<---- R1|0|antenna 2 name=... --|
      |<---- R1|0| -------------------|
      |                                |
      |---- C2|port get 1 ------------>|
      |<---- R2|0|port 1 auto=1... ----|
      |                                |
      |---- C3|port get 2 ------------>|
      |<---- R3|0|port 2 auto=1... ----|
      |                                |
      |---- C4|sub port all ---------->|
      |<---- R4|0| -------------------|
      |                                |
      |---- C5|sub relay ------------->|
      |<---- R5|0| -------------------|
      |                                |
```

### User Selects Antenna

```
Browser           Log4YM Server           Antenna Genius
   |                    |                       |
   |-- SelectAntenna -->|                       |
   |    (port=1, ant=3) |                       |
   |                    |---- C6|port set 1 --->|
   |                    |      rxant=3 txant=3  |
   |                    |<---- R6|0| -----------|
   |                    |                       |
   |                    |<---- S0|port 1... ----|
   |<-- PortChanged ----|                       |
   |    (status update) |                       |
```

## Testing Strategy

### Unit Tests
- Command parser tests
- Response parser tests
- Status message parser tests

### Integration Tests
- Mock TCP server for command/response testing
- SignalR event propagation tests

### Manual Testing
- Real device connection testing
- UI interaction testing
- Network failure/reconnection testing

## Configuration Options

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `AntennaGenius:Enabled` | bool | false | Enable AG integration |
| `AntennaGenius:IpAddress` | string | null | Manual IP (auto-discover if null) |
| `AntennaGenius:Port` | int | 9007 | TCP port |
| `AntennaGenius:ReconnectDelay` | int | 5000 | Reconnect delay in ms |
| `AntennaGenius:KeepAliveInterval` | int | 30000 | Ping interval in ms |

## Risks and Mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| Network instability | Connection drops | Auto-reconnect with backoff |
| Multiple AG devices | Confusion | Device selector in settings |
| Firmware incompatibility | Protocol errors | Version check on connect |
| UDP discovery blocked | No auto-discover | Manual IP configuration |

## Success Metrics

1. Successful auto-discovery of Antenna Genius on local network
2. Real-time antenna status display (< 100ms latency)
3. Successful antenna switching via UI
4. Graceful handling of connection loss/recovery

## References

- [4O3A Antenna Genius Product Page](https://4o3a.com/products/genius-family/antenna-genius)
- [Antenna Genius API Documentation](https://github.com/4o3a/genius-api-docs/wiki)
- Local API docs: `docs/antgen/wiki/`
