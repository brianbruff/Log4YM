# PGXL TCI Integration PRD

**Version:** 1.0
**Author:** Log4YM Development Team
**Date:** 2025-12-21
**Status:** Draft

---

## Executive Summary

This PRD defines the requirements for integrating TCI (Thetis Control Interface) radio control with the PowerGenius XL (PGXL) amplifier via LAN. The goal is to allow a TCI-connected radio (ExpertSDR, Thetis, ANAN) to control the PGXL for band tracking and PTT coordination without requiring a physical FlexRadio.

**Key Insight:** The PGXL was designed specifically for FlexRadio integration. When paired with a FlexRadio, the PGXL acts as a **client** that connects TO the radio, not the other way around. This means we cannot simply send commands to the PGXL; we must **emulate a FlexRadio** that the PGXL connects to.

**Critical Constraint:** The PGXL will NOT enable PTT via LAN when paired with a FlexRadio. Hard-wired PTT from the TCI radio to the PGXL is required.

---

## Background

### Current State

The existing PGXL integration in Log4YM provides:
- UDP discovery of PGXL devices on port 9008
- TCP connection for status monitoring and operate/standby control
- Display of meter readings (forward power, SWR, temperature)
- Operate/Standby mode switching

### Problem Statement

The PGXL expects band and PTT data from a FlexRadio via its proprietary protocol. When a TCI radio (ExpertSDR3, Thetis, ANAN) is used instead:

1. The PGXL cannot receive band information automatically
2. The PGXL cannot coordinate PTT timing properly
3. The amp goes to STANDBY when keyed because it's waiting for data from a non-existent FlexRadio

### Why Previous Approach Failed

Our initial approach was to:
1. Monitor TCI radio for band/PTT state changes
2. Send commands to PGXL when TCI state changes

This failed because:
- The PGXL has no TCP command to "set band" or "trigger PTT"
- Band data must come from: FlexRadio LAN pairing, CAT serial, BCD input, or Pin2Band
- The PGXL connects TO the FlexRadio as a client, not the other way around
- When paired with FlexRadio serial "714", it expects that radio to send slice/PTT updates

---

## Architecture Overview

### How FlexRadio-PGXL Integration Works

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                         FLEXRADIO INTEGRATION FLOW                          │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│   ┌──────────────┐    UDP 4991 (Discovery)     ┌──────────────────┐        │
│   │  FlexRadio   │ ────────────────────────►   │       PGXL       │        │
│   │  6600/6700   │    VITA-49 Broadcast        │  (Listening for  │        │
│   │              │    "serial=1234..."         │   paired serial) │        │
│   └──────────────┘                             └──────────────────┘        │
│          │                                              │                   │
│          │                                              │ "That's my radio!"│
│          │                                              ▼                   │
│          │◄───────────────────────────────────  TCP Connect to 4992        │
│          │                                                                  │
│          │    PGXL sends:                                                   │
│          │    - amplifier create ip=... model=PowerGeniusXL serial=...     │
│          │    - meter create name=FWD type=AMP ...                         │
│          │    - interlock create type=AMP ...                              │
│          │    - keepalive enable                                           │
│          │    - sub slice all                                              │
│          │                                                                  │
│          │    FlexRadio sends:                                             │
│          │    - S|slice 0 RF_frequency=14.250 mode=USB tx=0               │
│          │    - S|slice 0 tx=1  (PTT pressed)                             │
│          │                                                                  │
│   ┌──────┴──────┐                              ┌──────────────────┐        │
│   │   CRITICAL  │                              │                  │        │
│   │             │                              │   PTT via LAN    │        │
│   │ PTT via LAN │    When paired with Flex:    │   is DISABLED    │        │
│   │ is DISABLED │    Hard-wired PTT required!  │                  │        │
│   │             │                              │                  │        │
│   └─────────────┘                              └──────────────────┘        │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

### Proposed Solution: FlexRadio Emulator

To integrate TCI radios with PGXL, Log4YM will emulate a FlexRadio:

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                           TCI-PGXL INTEGRATION                              │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│   ┌──────────────┐                             ┌──────────────────┐        │
│   │  TCI Radio   │    WebSocket (50001)        │     Log4YM       │        │
│   │  ExpertSDR3  │ ───────────────────────►    │     Server       │        │
│   │  Thetis      │    TCI Protocol             │                  │        │
│   └──────────────┘    freq, mode, trx          └────────┬─────────┘        │
│          │                                              │                   │
│          │                                              │                   │
│   Hard-wired PTT                                        ▼                   │
│   (Required!)                          ┌────────────────────────────────┐  │
│          │                             │   FlexRadio Emulator Service   │  │
│          │                             │                                │  │
│          │                             │   - Broadcasts UDP discovery   │  │
│          │                             │   - Accepts TCP on port 4992   │  │
│          │                             │   - Responds to FLEX API       │  │
│          │                             │   - Sends slice status updates │  │
│          ▼                             └────────────────┬───────────────┘  │
│   ┌──────────────┐                                      │                   │
│   │     PGXL     │◄─────────────────────────────────────┘                   │
│   │  Amplifier   │    UDP 4991 (Discovery)                                  │
│   │              │    TCP 4992 (PGXL connects as client)                    │
│   │              │    S|slice 0 RF_frequency=14.250 tx=1                    │
│   └──────────────┘                                                          │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## Protocol Details

### FlexRadio Discovery Protocol

The PGXL listens for VITA-49 discovery packets on **UDP port 4991**:

```c
// VITA-49 Discovery Packet Structure
typedef struct {
    uint32_t header;           // VITA-49 header
    uint32_t stream_id;        // 0x800
    uint32_t class_id_h;       // 0x534CFFFF
    uint32_t class_id_l;
    uint32_t timestamp_int;
    uint32_t timestamp_frac_h;
    uint32_t timestamp_frac_l;
    char payload[256];         // "model=FLEX-6600 serial=1234 version=3.5.0 ..."
} vita_discovery_packet;
```

**Payload Format:**
```
model=FLEX-6600 serial=1234-5678-9012-3456 version=3.5.0 name=My_Radio callsign=EI6LF ip=192.168.1.100 port=4992
```

### FlexRadio TCP API (Port 4992)

Once the PGXL discovers the emulated radio, it connects via TCP and sends:

| Command | Purpose |
|---------|---------|
| `amplifier create ip=... port=9008 model=PowerGeniusXL serial=...` | Register amplifier |
| `meter create name=FWD type=AMP min=30.0 max=63.01 units=DBM` | Create meters |
| `interlock create type=AMP valid_antennas=ANT1,ANT2 name=PG-XL serial=...` | Setup interlock |
| `keepalive enable` | Enable keepalive pings |
| `sub slice all` | Subscribe to slice updates |

### Slice Status Updates

The emulator must send slice status updates when TCI radio changes:

```
S0|slice 0 RF_frequency=14.250000 mode=USB tx=0 active=1
S0|slice 0 tx=1                                            // PTT pressed
S0|slice 0 tx=0                                            // PTT released
S0|slice 0 RF_frequency=7.150000 mode=LSB tx=0            // Band change
```

---

## PTT Considerations

### Critical Constraint

From the PGXL API documentation:

> "Note the amplifier will not enable PTT via the LAN mechanism if paired with a FLEX transceiver."

This means:
- **Hard-wired PTT is REQUIRED** between the TCI radio and PGXL
- The emulator's `tx=1` updates inform the PGXL about band selection for TX
- The physical PTT line triggers the actual amplifier keying

### PTT Timing Diagram

```
TCI Radio PTT Pressed:

    TCI WebSocket      Log4YM Server      FlexEmulator        PGXL
         │                  │                  │                │
         │  trx:0,true;     │                  │                │
         │─────────────────►│                  │                │
         │                  │  Send slice tx=1 │                │
         │                  │─────────────────►│                │
         │                  │                  │ TCP: S|slice tx=1
         │                  │                  │───────────────►│
         │                  │                  │                │ Band ready
         │                  │                  │                │
    ─────┴──────────────────┴──────────────────┴────────────────┴─────
              Hard-wired PTT line (immediate, no latency)
    ─────┬──────────────────┬──────────────────┬────────────────┬─────
         │                  │                  │                │
         │                  │                  │                │ PTT Active
```

### Why Hard-Wired PTT is Safe

The hard-wired PTT approach is actually safer:
1. **No network latency** - PTT transitions are immediate
2. **PGXL internal delay** - PTT-in doesn't reflect to PTT-out until amp is ready
3. **Failsafe** - If network fails, amplifier won't key (no hot switching)

---

## Implementation Plan

### Phase 1: FlexRadio Emulator Service

Create a new `FlexEmulatorService` in Log4YM.Server:

```csharp
public class FlexEmulatorService : BackgroundService
{
    // Configuration
    private string _emulatedSerial = "LOG4-YM00-0000-0001";
    private string _emulatedModel = "FLEX-6600";
    private int _discoveryPort = 4991;
    private int _apiPort = 4992;

    // UDP Discovery (broadcast every 1 second)
    private async Task BroadcastDiscoveryAsync();

    // TCP API Server (accept PGXL connections)
    private async Task HandleClientAsync(TcpClient client);

    // Send slice status to connected PGXL
    public async Task SendSliceStatusAsync(double freqMHz, string mode, bool tx);
}
```

### Phase 2: TCI-to-Emulator Bridge

Connect TCI radio state to the emulator:

```csharp
// When TCI radio state changes:
tciService.OnFrequencyChanged += async (freq, mode) =>
{
    await flexEmulator.SendSliceStatusAsync(freq / 1_000_000.0, mode, false);
};

tciService.OnTxStateChanged += async (tx) =>
{
    await flexEmulator.SendSliceStatusAsync(currentFreq, currentMode, tx);
};
```

### Phase 3: PGXL Configuration

Add UI to configure the PGXL to pair with the emulated radio:

1. **Generate unique serial** for the emulator
2. **Configure PGXL** via TCP command:
   ```
   flexradio ampslice=A serial=LOG4-YM00-0000-0001 txant=ANT1 ptt=LAN active=1
   save
   ```
3. **PGXL reboots** and discovers the emulated radio

---

## Configuration

### Settings Required

| Setting | Description | Default |
|---------|-------------|---------|
| `FlexEmulator.Enabled` | Enable/disable FlexRadio emulation | `false` |
| `FlexEmulator.Serial` | Emulated serial number | `LOG4-YM00-0000-0001` |
| `FlexEmulator.Model` | Emulated model | `FLEX-6600` |
| `FlexEmulator.ApiPort` | TCP API port | `4992` |
| `FlexEmulator.DiscoveryPort` | UDP discovery port | `4991` |
| `PGXL.TciLink.SideA` | TCI radio ID linked to PGXL Side A | `null` |
| `PGXL.TciLink.SideB` | TCI radio ID linked to PGXL Side B | `null` |

---

## User Workflow

### Initial Setup

1. **Connect TCI radio** to Log4YM via WebSocket
2. **Enable FlexRadio Emulator** in Log4YM settings
3. **Configure PGXL** to pair with emulated serial:
   - Open PGXL Settings in Log4YM
   - Click "Configure as TCI Amplifier"
   - This sends the `flexradio ampslice=A serial=... active=1` command
   - PGXL reboots automatically
4. **Connect hard-wired PTT** from TCI radio to PGXL PTT-IN
5. **Test**: Change frequency on TCI radio, verify PGXL shows correct band

### Normal Operation

```
┌────────────────────────────────────────────────────────────────┐
│                    NORMAL OPERATION FLOW                       │
├────────────────────────────────────────────────────────────────┤
│                                                                │
│  1. Log4YM starts FlexEmulator                                 │
│     └── Broadcasts discovery packets every 1 second           │
│                                                                │
│  2. PGXL discovers "emulated FlexRadio"                        │
│     └── Connects via TCP to port 4992                         │
│     └── Registers as amplifier                                │
│     └── Subscribes to slice updates                           │
│                                                                │
│  3. TCI radio connected to Log4YM                              │
│     └── Frequency changes pushed to FlexEmulator              │
│     └── FlexEmulator sends S|slice 0 RF_frequency=...         │
│     └── PGXL switches band automatically                      │
│                                                                │
│  4. User presses PTT on TCI radio                              │
│     └── Hard-wired PTT activates PGXL immediately             │
│     └── TCI sends trx:0,true; to Log4YM                       │
│     └── FlexEmulator sends S|slice 0 tx=1                     │
│     └── PGXL already keyed via hard-wire, now has band info   │
│                                                                │
└────────────────────────────────────────────────────────────────┘
```

---

## Alternative Approaches Considered

### Option A: BCD Band Input (Rejected)

Use BCD parallel interface for band data:
- **Pro:** Simple, no emulation needed
- **Con:** Requires additional hardware (BCD encoder)
- **Con:** No computer needed, but defeats LAN-only goal

### Option B: CAT Serial (Rejected)

Use CAT/CIV serial for band data:
- **Pro:** Supported by many radios
- **Con:** Requires USB serial adapter
- **Con:** TCI radios don't have native CAT output

### Option C: FlexRadio Emulation (Selected)

Emulate a FlexRadio on the network:
- **Pro:** Pure LAN solution (plus hard-wired PTT)
- **Pro:** Uses existing PGXL infrastructure
- **Pro:** No additional hardware
- **Con:** More complex implementation
- **Con:** Hard-wired PTT still required

---

## Risks and Mitigations

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| PGXL firmware rejects non-Flex client | Medium | High | Test with actual PGXL hardware |
| Network latency affects band switching | Low | Medium | Send band updates immediately on TCI change |
| User forgets hard-wired PTT | High | High | Clear warning in UI, documentation |
| Discovery packet format changes | Low | Low | Monitor FlexRadio updates |

---

## Testing Strategy

### Unit Tests
- VITA-49 packet generation
- TCP command parsing
- Slice status message formatting

### Integration Tests
- TCI state changes propagate to emulator
- PGXL connects to emulator successfully
- Band changes reflected in PGXL status

### Manual Testing Checklist

- [ ] FlexEmulator starts and broadcasts discovery
- [ ] PGXL discovers emulated radio (check PGXL utility)
- [ ] PGXL connects to emulator via TCP
- [ ] Frequency change on TCI radio updates PGXL band
- [ ] Mode change on TCI radio (if applicable)
- [ ] Hard-wired PTT keys amplifier correctly
- [ ] Network disconnect doesn't cause TX issues
- [ ] PGXL reconnects after network interruption

---

## Open Questions

1. **Q:** Does PGXL validate the FlexRadio model or just the serial?
   **A:** TBD - needs testing with actual hardware.

2. **Q:** Can we avoid the hard-wired PTT requirement?
   **A:** Not when paired with FlexRadio. The PGXL firmware explicitly disables LAN PTT in this mode. Alternative would be to NOT pair with FlexRadio and use BCD/CAT, but that defeats the LAN-only goal.

3. **Q:** What happens if both TCI and real FlexRadio are on the network?
   **A:** The PGXL will only connect to the radio with the paired serial number.

---

## References

- [PGXL API Documentation](./PGXL-Amplifier-to-Radio-API-Documentation.pdf) - FlexRadio PGXL API (in this folder)
- [SmartSDR API Wiki](https://github.com/flexradio/smartsdr-api-docs/wiki) - FlexRadio Ethernet API
- [Discovery Protocol](https://github.com/flexradio/smartsdr-api-docs/wiki/Discovery-Protocol) - VITA-49 Discovery
- [Metering Protocol](https://github.com/flexradio/smartsdr-api-docs/wiki/Metering-Protocol) - FLEX Metering
- [TCI Protocol](https://github.com/ExpertSDR3/TCI) - TCI specification
- [FlexRadio Community](https://community.flexradio.com/) - Developer discussions
