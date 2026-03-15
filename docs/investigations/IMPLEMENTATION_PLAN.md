# Implementation Plan: TCI Multiple Connections Support

**Date:** 2026-03-03
**Priority:** Medium
**Complexity:** Low (primarily configuration fix)

---

## Problem Statement

User reported that only one application (Log4YM or StationMaster) can connect to Thetis via TCI at a time. Investigation reveals this is likely a configuration issue, not a protocol limitation.

---

## Root Cause

**Primary Issue:** Port number mismatch
- Log4YM uses default port **50001**
- TCI standard specifies port **40001**
- This may cause connection failures or conflicts

**Secondary Issues:**
- No user-facing port configuration
- Limited connection diagnostics
- No troubleshooting documentation

---

## Solution Overview

### Phase 1: Critical Fix (Required)
Fix the default port number to match TCI standard.

### Phase 2: Configuration Enhancement (Recommended)
Add user-configurable TCI port setting.

### Phase 3: Documentation (Required)
Create troubleshooting guide for multi-client setups.

---

## Implementation Details

### Change 1: Fix Default TCI Port

**File:** `src/Log4YM.Server/Services/TciRadioService.cs`

**Current (Line 27):**
```csharp
private const int TciDefaultPort = 50001;
```

**Proposed:**
```csharp
private const int TciDefaultPort = 40001;  // TCI protocol standard port
```

**Impact:**
- ✅ Aligns with TCI protocol standard
- ✅ Increases compatibility with other TCI clients
- ⚠️ May affect existing users with custom port configurations

**Migration Strategy:**
- Include in release notes
- Notify users to verify Thetis port matches
- Consider auto-detection if possible

---

### Change 2: Add Port Configuration UI

**Files to Modify:**
- `src/Log4YM.Contracts/Models/RadioConfigEntity.cs` - Add TciPort property
- `src/Log4YM.Contracts/Models/Settings.cs` - Add default port setting
- `src/Log4YM.Web/src/components/SettingsPanel.tsx` - Add port input field
- `src/Log4YM.Server/Services/TciRadioService.cs` - Use configured port

**UI Mockup:**
```
┌─────────────────────────────────────────┐
│ TCI Radio Settings                      │
├─────────────────────────────────────────┤
│                                         │
│ Host: [192.168.1.100          ]        │
│ Port: [40001                  ] ℹ️      │
│       Default: 40001 (TCI standard)    │
│                                         │
│ Name: [My Thetis              ]        │
│                                         │
│ [Test Connection] [Save]               │
│                                         │
└─────────────────────────────────────────┘
```

**Settings Schema:**
```typescript
interface TciSettings {
  host: string;
  port: number;  // Default: 40001
  name?: string;
  autoConnect: boolean;
}
```

---

### Change 3: Enhanced Connection Logging

**Purpose:** Help diagnose connection issues

**Implementation:**
```csharp
// In TciRadioConnection.ConnectAsync()
_logger.LogInformation(
    "Connecting to TCI radio at ws://{Ip}:{Port} (configured port: {ConfiguredPort}, default: {DefaultPort})",
    _device.IpAddress,
    _device.TciPort,
    configuredPort,
    TciDefaultPort
);

// On connection failure
_logger.LogError(
    "Failed to connect to TCI at ws://{Ip}:{Port}. " +
    "Verify: 1) Thetis TCI server is running, " +
    "2) Port matches Thetis configuration, " +
    "3) Firewall allows port {Port}",
    _device.IpAddress,
    _device.TciPort,
    _device.TciPort
);
```

**User-Facing Messages:**
```
❌ Cannot connect to TCI
   Host: 192.168.1.100
   Port: 40001

   Troubleshooting:
   • Verify Thetis TCI server is enabled
   • Check port number matches Thetis
   • Ensure firewall allows port 40001

   [View Setup Guide] [Retry]
```

---

### Change 4: Documentation

**File:** `docs/user-guides/tci-multiple-clients.md`

**Content Structure:**
```markdown
# Using TCI with Multiple Applications

## Overview
TCI protocol supports multiple applications connecting simultaneously...

## Setup Checklist
- [ ] Enable TCI server in Thetis
- [ ] Verify port number (default: 40001)
- [ ] Configure firewall rules
- [ ] Test connection from first application
- [ ] Connect additional applications

## Troubleshooting
### Only one app connects at a time
- Check all apps use the same port
- Restart Thetis TCI server
- Check Windows Firewall...

### Connection refused
- Verify Thetis TCI is enabled...
```

---

## Testing Plan

### Unit Tests

**File:** `src/Log4YM.Server.Tests/Tests/Services/TciRadioServiceTests.cs`

```csharp
[Fact]
public void DefaultPort_ShouldBe_40001()
{
    // Verify TCI standard port
    Assert.Equal(40001, TciRadioService.TciDefaultPort);
}

[Fact]
public async Task ConnectDirect_ShouldUseProvidedPort()
{
    var service = CreateService();
    await service.ConnectDirectAsync("192.168.1.100", 50001);

    // Verify connection attempted to specified port, not default
    _mockLogger.Verify(
        x => x.Log(
            LogLevel.Information,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, t) => v.ToString().Contains(":50001")),
            null,
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
        Times.Once);
}
```

### Integration Tests

1. **Single Client Test**
   - Connect Log4YM to Thetis
   - Verify frequency updates
   - Change mode, verify synchronization

2. **Multi-Client Test**
   ```
   Setup:
   1. Start Thetis with TCI on port 40001
   2. Connect Log4YM
   3. Connect JTDX (or StationMaster if available)

   Tests:
   - Both apps show "Connected" status
   - Change frequency in Thetis → both apps update
   - Change mode in Thetis → both apps update
   - Send command from Log4YM → Thetis responds
   - Send command from JTDX → Thetis responds
   - Disconnect Log4YM → JTDX remains connected
   ```

3. **Port Mismatch Test**
   - Configure Log4YM for port 50001
   - Configure Thetis for port 40001
   - Verify clear error message
   - Update Log4YM port to 40001
   - Verify successful connection

### Manual Testing Checklist

- [ ] Default port is 40001 after code change
- [ ] Can manually specify different port
- [ ] Port persists in configuration
- [ ] Connection logs show attempted port
- [ ] Error messages are clear and actionable
- [ ] Multiple apps can connect simultaneously
- [ ] Disconnecting one app doesn't affect others
- [ ] Frequency/mode updates propagate to all clients

---

## Deployment Strategy

### Release Notes

```markdown
## TCI Multiple Client Support

### Breaking Change: Default TCI Port
The default TCI port has been changed from 50001 to 40001 to
match the TCI protocol standard.

**Action Required:**
- If you've customized your Thetis TCI port to 50001, either:
  1. Change Thetis back to 40001 (recommended), OR
  2. Manually configure Log4YM to use port 50001 in settings

### New Feature: Port Configuration
You can now specify a custom TCI port in Radio Settings.

### Improvement: Multiple TCI Clients
Log4YM now explicitly supports running alongside other TCI
applications (JTDX, StationMaster, etc.). See the TCI Setup
Guide for details.
```

### Migration Path

1. **Detect existing connections:**
   - Check if user has previously connected to TCI
   - If yes, log warning about port change
   - Provide migration wizard

2. **Auto-detection:**
   - On first connection attempt, try both 40001 and 50001
   - Save working port to configuration
   - Inform user of detected port

---

## Risk Assessment

### Low Risk Changes ✅
- Updating default port constant
- Adding logging
- Creating documentation

### Medium Risk Changes ⚠️
- Adding port configuration UI
  - Risk: User confusion if misconfigured
  - Mitigation: Clear defaults, validation, tooltips

### No Risk to Multi-Client Support ✅
- Log4YM code doesn't prevent multiple connections
- Changes don't add exclusive locks
- WebSocket protocol inherently supports multiple clients

---

## Success Metrics

### Technical Metrics
- ✅ Default port matches TCI standard (40001)
- ✅ Connection success rate > 95%
- ✅ Multiple clients can connect simultaneously
- ✅ Connection time < 2 seconds

### User Metrics
- 📉 Reduce "can't connect" support issues
- 📈 Increase successful multi-client setups
- ⭐ Positive feedback on compatibility

---

## Timeline Estimate

| Task | Effort | Dependencies |
|------|--------|--------------|
| Fix default port | 1 hour | None |
| Add port config UI | 4 hours | Backend entity updates |
| Enhanced logging | 2 hours | None |
| Documentation | 3 hours | Testing complete |
| Testing | 4 hours | Code complete |
| **Total** | **14 hours** | **~2 days** |

---

## Future Enhancements

### Phase 4: Advanced Multi-Client Features
- Show "Other TCI clients: 2" indicator
- Detect conflicts (multiple clients sending commands)
- Cooperative frequency management
- Multi-client activity log

### Phase 5: TCI Server Mode
- Let Log4YM act as TCI relay/proxy
- Aggregate multiple radios under one TCI interface
- Add authentication/access control

---

## Appendix: TCI Protocol Details

### Connection Lifecycle
```
Client                          Thetis (Server)
  |                                  |
  |----  WebSocket Handshake  ----->|
  |<---  HTTP 101 Switching   ------|
  |                                  |
  |<---  protocol:name,ver    ------|  (Server info)
  |<---  device:radiomodel    ------|  (Device info)
  |<---  ready:               ------|  (Ready signal)
  |                                  |
  |<---  vfo:0,0,14250000     ------|  (Frequency updates)
  |<---  modulation:0,USB     ------|  (Mode updates)
  |                                  |
  |----  vfo:0,0,14074000     ----->|  (Set frequency)
  |<---  vfo:0,0,14074000     ------|  (Confirmation)
```

### Multi-Client Broadcast Pattern
```
Thetis receives frequency change from hardware
    ↓
Thetis broadcasts vfo: command
    ↓
┌───────────┬─────────────┬──────────────┐
↓           ↓             ↓              ↓
Log4YM    JTDX     StationMaster    N1MM+
(Client1) (Client2)  (Client3)    (Client4)

All clients receive the same update simultaneously
```

---

## Contact

For questions or clarifications on this implementation plan:
- Review investigation document: `tci-multiple-connections-investigation.md`
- Check TCI protocol spec: https://github.com/ExpertSDR3/TCI
- Community forum: https://community.apache-labs.com
