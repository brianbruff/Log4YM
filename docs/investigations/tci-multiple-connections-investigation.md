# Investigation: Multiple Simultaneous TCI Connections to Thetis

**Date:** 2026-03-03
**Investigator:** Claude (Agent)
**Status:** ✅ CONFIRMED - Multiple connections are supported by TCI protocol
**Issue Reference:** User report - Only one application can connect to Thetis via TCI at a time

---

## Executive Summary

**Finding:** The TCI (Transceiver Control Interface) protocol **DOES support multiple simultaneous client connections** to Thetis via WebSocket. This is a core design feature of the protocol.

**Current State:** Log4YM's implementation appears to be technically capable of co-existing with other TCI clients (like StationMaster), as it uses standard WebSocket connections without any exclusive locking mechanisms.

**Root Cause Hypothesis:** The issue reported by the user is likely due to:
1. Network configuration (firewall, port conflicts)
2. Thetis configuration settings
3. Client-specific implementation issues in either Log4YM or StationMaster
4. Thetis server capacity limits (though these should be high)

---

## TCI Protocol Architecture

### Design Principles

The TCI protocol was specifically designed as a **"universal multi-client interface"** to replace legacy single-client CAT control systems. Key characteristics:

- **Transport:** WebSocket over TCP (full-duplex)
- **Default Port:** 40001 (configurable)
- **Server:** Thetis/ExpertSDR/ANAN acts as WebSocket server
- **Clients:** Multiple applications connect as WebSocket clients
- **Discovery:** UDP broadcast on port 1024

### Multi-Client Architecture

```
┌─────────────────────────────────────┐
│         Thetis (TCI Server)         │
│         WebSocket Port 40001        │
└─────────────┬───────────────────────┘
              │
      ────────┼────────────────
      │       │               │
      ▼       ▼               ▼
  ┌──────┐ ┌──────┐     ┌──────────┐
  │Log4YM│ │JTDX  │     │Station   │
  │      │ │      │     │Master    │
  └──────┘ └──────┘     └──────────┘
   Client1  Client2      Client3
```

### Protocol Capabilities

According to TCI documentation and community implementations:

1. **Simultaneous Connections:** Multiple clients can connect at once
2. **Independent Subscriptions:** Each client receives its own state updates
3. **Bidirectional Control:** All clients can send commands
4. **Broadcast Updates:** Server pushes frequency/mode changes to all connected clients

### Known Limitations

- **Audio Streaming:** TCI audio to multiple clients not implemented in Thetis (as of recent reports)
- **Resource Limits:** Server-side connection limits may apply (implementation-dependent)
- **Command Conflicts:** Simultaneous commands from different clients could conflict (application-layer issue)

---

## Log4YM TCI Implementation Analysis

### Current Architecture

From `TciRadioService.cs` analysis:

**Connection Management:**
- Uses `ClientWebSocket` from System.Net.WebSockets
- No exclusive locking on WebSocket connection
- Standard TCP/WebSocket connection (not monopolizing the port)
- Connection stored in `_connections` ConcurrentDictionary

**Key Code Locations:**
- Service: `src/Log4YM.Server/Services/TciRadioService.cs`
- Connection: `TciRadioConnection` class (lines 654-1003)
- Protocol: WebSocket at `ws://{ip}:{port}` (default port 50001 in code, should be 40001)

### Potential Issues Identified

#### 1. Port Mismatch
**Location:** Line 27 in `TciRadioService.cs`
```csharp
private const int TciDefaultPort = 50001;
```

**Issue:** TCI standard port is **40001**, not 50001. This could cause connection issues if Thetis is listening on 40001.

**Evidence:**
- Documentation shows TCI default as 40001
- `radio-integration.md` (line 104) confirms: "Default Port: 40001"
- Code uses 50001 as default

**Impact:** If Log4YM connects to wrong port, it may fail or connect to a different service.

#### 2. Discovery Port
**Location:** Line 26 in `TciRadioService.cs`
```csharp
private const int DiscoveryPort = 1024;
```

**Status:** ✅ Correct - TCI uses UDP 1024 for discovery

#### 3. No Exclusive Connection Logic
**Analysis:** Log4YM does NOT:
- Lock the WebSocket port
- Request exclusive access
- Prevent other clients from connecting
- Interfere with other connections

**Conclusion:** The Log4YM implementation should allow concurrent connections.

---

## Testing Recommendations

### Verification Tests

1. **Port Configuration Test**
   - Verify Thetis TCI port (Setup → CAT/TCI → Port)
   - Update Log4YM `TciDefaultPort` if needed
   - Test connection after port alignment

2. **Concurrent Connection Test**
   ```
   1. Start Thetis with TCI enabled
   2. Connect Log4YM to TCI
   3. Connect StationMaster to TCI
   4. Verify both show connected state
   5. Change frequency in Thetis
   6. Confirm both applications receive updates
   ```

3. **Network Diagnostics**
   ```bash
   # Check if Thetis is listening on correct port
   netstat -an | grep 40001  # or 50001

   # Monitor WebSocket connections
   tcpdump -i any port 40001
   ```

4. **Thetis Configuration Check**
   - Enable TCI Server in Thetis
   - Verify "Allow Multiple Connections" setting (if exists)
   - Check firewall rules

---

## Recommendations

### Immediate Actions

1. **Fix Port Number in Log4YM**
   ```csharp
   // Change in TciRadioService.cs line 27
   private const int TciDefaultPort = 40001;  // Was: 50001
   ```

2. **Add Configuration Option**
   - Allow users to specify custom TCI port in settings
   - Document the correct port in user documentation

3. **Enhanced Connection Logging**
   - Log the exact URI being connected to
   - Log any connection rejections from server
   - Display port number in UI

### User Documentation

Create troubleshooting guide:

```markdown
## Multiple TCI Applications

TCI supports multiple simultaneous connections. To use Log4YM
with other TCI applications:

1. Ensure all applications use the same TCI port (default: 40001)
2. Verify Thetis TCI server is enabled
3. Check Windows Firewall allows port 40001
4. Each application connects independently - no special configuration needed

If connections fail:
- Check Thetis shows TCI server running
- Verify port numbers match across applications
- Restart Thetis to reset TCI server
- Check for conflicting CAT/COM port settings
```

### Future Enhancements

1. **Multi-Instance Support**
   - Allow Log4YM to select which TCI receiver (RX0, RX1, etc.) to monitor
   - Display all active TCI connections in UI

2. **Connection Health Monitoring**
   - Detect when other TCI clients connect/disconnect
   - Show "TCI clients connected: 2" indicator

3. **Coordination Features**
   - Prevent frequency collision between clients
   - Cooperative PTT management (if implemented)

---

## Technical References

### TCI Protocol Documentation

- **ExpertSDR3 TCI Specification:** https://github.com/ExpertSDR3/TCI
- **TCI Wiki (JTDX):** https://deepwiki.com/jtdx-project/jtdx/4.1-tci-transceiver-interface
- **Cluster-TCI-Bridge:** Multi-client TCI bridge implementation
- **Community:** https://community.apache-labs.com/viewtopic.php?t=5163

### WebSocket Multi-Client Resources

- WebSocket spec supports multiple concurrent connections per server
- Server must manage connection list and broadcast to each client
- No protocol-level restriction on connection count

---

## Conclusion

**The TCI protocol fully supports multiple simultaneous client connections.** The reported issue where only one application can connect at a time is **not a limitation of the protocol**, but likely stems from:

1. **Configuration mismatch** (port numbers, server settings)
2. **Implementation bug** in one of the applications
3. **Network/firewall issues**

Log4YM's implementation is architecturally sound for concurrent usage. The primary issue to address is the **port number mismatch** (50001 vs 40001).

### Recommended Next Steps

1. ✅ Document these findings
2. 🔧 Fix default TCI port to 40001
3. 📝 Create user troubleshooting guide
4. 🧪 Test with multiple concurrent applications
5. 📊 Add telemetry to track connection success rates

---

## References

### Code Analysis
- `src/Log4YM.Server/Services/TciRadioService.cs` - Main TCI service
- `docs/prds/radio-integration.md` - Architecture documentation
- `docs/prds/tci-cluster-tuning.md` - TCI implementation notes

### External Research
- TCI protocol specification (GitHub: ExpertSDR3/TCI)
- Thetis/ANAN user reports on multi-client usage
- WebSocket multi-client architecture patterns
