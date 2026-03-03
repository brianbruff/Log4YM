# TCI Multiple Connections Investigation - Summary

**Issue:** User reported that only one application can connect to Thetis via TCI at a time
**Date:** 2026-03-03
**Status:** ✅ Investigation Complete

---

## Key Findings

### ✅ TCI Protocol DOES Support Multiple Simultaneous Connections

The TCI (Transceiver Control Interface) protocol was **explicitly designed** to support multiple concurrent client connections. This is a core feature, not a limitation.

**Evidence:**
- Official TCI documentation specifies multi-client architecture
- Community reports confirm multiple apps work simultaneously
- Examples: JTDX + Logger + Cluster Bridge all connected at once
- WebSocket protocol inherently supports multiple clients

### 🔧 Root Cause: Configuration Issue, NOT Protocol Limitation

The reported problem is likely due to:

1. **Port Number Mismatch** ⚠️ **PRIMARY ISSUE**
   - Log4YM uses port **50001** (line 27 in `TciRadioService.cs`)
   - TCI standard specifies port **40001**
   - Thetis likely listening on 40001

2. **Network/Firewall Configuration**
   - Windows Firewall blocking connections
   - Router port forwarding misconfigured
   - Network permissions issues

3. **Thetis Configuration**
   - TCI server not enabled
   - Connection limits set too low
   - Incorrect port binding

### 🎯 Log4YM's Implementation is Multi-Client Compatible

**Analysis of `TciRadioService.cs`:**
- Uses standard `ClientWebSocket`
- No exclusive locking mechanisms
- No connection monopolization
- Follows standard WebSocket client patterns
- **Conclusion:** Code is architecturally sound for concurrent usage

---

## Immediate Recommendations

### 1. Fix Default Port (Critical)

**Change Required:**
```csharp
// File: src/Log4YM.Server/Services/TciRadioService.cs
// Line: 27

// FROM:
private const int TciDefaultPort = 50001;

// TO:
private const int TciDefaultPort = 40001;  // TCI standard port
```

**Impact:** Aligns with TCI protocol standard, increases compatibility

### 2. Add Port Configuration (High Priority)

Allow users to specify TCI port in settings:
- Add port field to RadioConfigEntity
- Add UI input in Settings panel
- Validate port numbers (1024-65535)
- Provide tooltip: "Default: 40001"

### 3. Improve Diagnostics (Medium Priority)

- Log exact connection URI: `ws://192.168.1.100:40001`
- Display port number in connection error messages
- Show "Connected clients: 2" indicator (future)

### 4. Create User Documentation (High Priority)

Troubleshooting guide covering:
- How to enable TCI in Thetis
- Verifying port numbers match
- Firewall configuration
- Multi-client setup instructions
- Common error messages and solutions

---

## Technical Details

### TCI Architecture
```
┌─────────────┐
│   Thetis    │  ← TCI Server (WebSocket)
│  Port 40001 │     Listens for connections
└──────┬──────┘     Broadcasts updates
       │
   ────┼────────────
   │   │           │
   ▼   ▼           ▼
┌──────┐ ┌──────┐ ┌──────┐
│Log4YM│ │JTDX  │ │N1MM+ │
│Client│ │Client│ │Client│
└──────┘ └──────┘ └──────┘
```

### Connection Flow
1. Client connects to `ws://{host}:40001`
2. Server accepts WebSocket handshake
3. Server sends: `protocol:`, `device:`, `ready:`
4. Server broadcasts frequency/mode updates
5. Client can send commands
6. All connected clients receive broadcasts

### Why Multiple Connections Work
- Each WebSocket connection is independent
- Server maintains list of connected clients
- Updates sent to all clients individually
- No protocol-level connection limits
- Only server resources limit client count

---

## Testing Checklist

To verify multiple connections work:

- [ ] Start Thetis with TCI enabled (port 40001)
- [ ] Connect Log4YM to TCI
- [ ] Verify Log4YM shows "Connected"
- [ ] Connect second app (JTDX/StationMaster)
- [ ] Verify both apps show "Connected"
- [ ] Change frequency in Thetis
- [ ] Verify both apps receive update
- [ ] Change mode in Thetis
- [ ] Verify both apps receive update
- [ ] Disconnect one app
- [ ] Verify other app remains connected

**If test fails:**
1. Check port numbers match (all use 40001)
2. Restart Thetis TCI server
3. Check firewall rules
4. Review application logs

---

## Files Created

1. **Investigation Report**
   `docs/investigations/tci-multiple-connections-investigation.md`
   - Complete technical analysis
   - Protocol documentation
   - Code review findings
   - Research references

2. **Implementation Plan**
   `docs/investigations/IMPLEMENTATION_PLAN.md`
   - Detailed code changes
   - UI mockups
   - Testing strategy
   - Timeline estimates

3. **This Summary**
   `docs/investigations/SUMMARY.md`
   - Quick reference
   - Key takeaways
   - Action items

---

## Next Steps

### For Development Team:
1. Review investigation findings
2. Approve port number change (50001 → 40001)
3. Implement changes per IMPLEMENTATION_PLAN.md
4. Test with real hardware
5. Update user documentation

### For Users (Workaround):
Until the port fix is released:
1. Check Thetis TCI port setting
2. If Thetis uses 50001, both apps should work
3. If Thetis uses 40001, change to 50001 temporarily
4. Or use direct connection with correct port

---

## Conclusion

**The good news:** TCI fully supports multiple simultaneous connections. Log4YM's implementation is compatible with this design.

**The fix:** A simple port number correction (50001 → 40001) should resolve the reported issue and align Log4YM with the TCI standard.

**The benefit:** Users will be able to run Log4YM alongside StationMaster, JTDX, and other TCI applications without conflicts.

---

## References

- **TCI Specification:** https://github.com/ExpertSDR3/TCI
- **WebSocket RFC:** https://tools.ietf.org/html/rfc6455
- **Thetis Documentation:** https://github.com/TAPR/OpenHPSDR-Thetis
- **Community Forum:** https://community.apache-labs.com

---

**Investigation completed:** 2026-03-03
**Estimated implementation time:** 2 days
**Priority:** Medium (improves compatibility and user experience)
