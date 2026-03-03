# TCI Multiple Connections Investigation

This directory contains the complete investigation into TCI (Transceiver Control Interface) multiple simultaneous connection support for Log4YM.

## Quick Start

**TL;DR:** Yes, TCI supports multiple connections. The issue is a port mismatch (50001 vs 40001).

**Read First:** [`SUMMARY.md`](./SUMMARY.md) - Executive summary with key findings

## Documents

### 1. SUMMARY.md
**Purpose:** Quick reference for key findings and recommendations
**Audience:** Everyone
**Read time:** 5 minutes

Contains:
- Key findings at a glance
- Root cause analysis
- Immediate action items
- Quick testing checklist

### 2. tci-multiple-connections-investigation.md
**Purpose:** Complete technical investigation
**Audience:** Developers, technical leads
**Read time:** 15-20 minutes

Contains:
- TCI protocol architecture details
- Log4YM implementation analysis
- Port number discrepancy analysis
- Testing recommendations
- External research and references
- Conclusion with next steps

### 3. IMPLEMENTATION_PLAN.md
**Purpose:** Detailed implementation guide
**Audience:** Developers implementing the fix
**Read time:** 20-25 minutes

Contains:
- Specific code changes required
- UI mockups and design
- Testing plan with unit/integration tests
- Risk assessment
- Timeline estimates
- Deployment strategy
- Success metrics

## Investigation Results

### ✅ Confirmed: Multiple Connections ARE Supported

The TCI protocol was **designed from the ground up** to support multiple simultaneous client connections. This is not a limitation - it's a core feature.

### 🔍 Root Cause Identified

**Primary Issue:** Port number mismatch
- Log4YM default: 50001 (incorrect)
- TCI standard: 40001 (correct)
- Location: `src/Log4YM.Server/Services/TciRadioService.cs:27`

**Secondary Issues:**
- No user-configurable port setting
- Limited diagnostic logging
- Missing troubleshooting documentation

### 🎯 Solution

**Simple Fix:** Change one constant
```csharp
// Change line 27 in TciRadioService.cs from:
private const int TciDefaultPort = 50001;
// To:
private const int TciDefaultPort = 40001;
```

**Complete Solution:** See `IMPLEMENTATION_PLAN.md`

## Testing Multiple Connections

### Prerequisites
- Thetis with TCI enabled on port 40001
- Log4YM built with port fix
- Another TCI-compatible application (JTDX, StationMaster, N1MM+)

### Test Procedure
1. Connect first application (Log4YM)
2. Verify connected status
3. Connect second application
4. Verify both show connected
5. Change frequency in Thetis
6. Confirm both apps update
7. Disconnect one app
8. Verify other remains connected

✅ **Pass Criteria:** Both applications work simultaneously

## For Users Experiencing Issues

### Current Workaround
1. Check Thetis TCI Settings → Port number
2. If set to 40001: Works with other apps but Log4YM may fail
3. If set to 50001: Log4YM works but other apps may fail
4. **Temporary fix:** Set all apps and Thetis to same port

### After Fix is Released
1. Update Log4YM to latest version
2. Verify Thetis uses port 40001 (standard)
3. All TCI apps should now work together

## Technical Background

### TCI Protocol
- **Transport:** WebSocket over TCP
- **Standard Port:** 40001
- **Discovery:** UDP port 1024
- **Multi-client:** Yes, by design
- **Specification:** https://github.com/ExpertSDR3/TCI

### Architecture
```
        Thetis (TCI Server)
               ↓
        Port 40001 WebSocket
               ↓
     ┌─────────┼─────────┐
     ↓         ↓         ↓
  Log4YM    JTDX    StationMaster
 (Client)  (Client)   (Client)
```

Each client maintains independent WebSocket connection. Server broadcasts updates to all connected clients.

## Key Insights

1. **Protocol Capability:** TCI supports unlimited* clients (*server resources permitting)
2. **Log4YM Compatibility:** Code is already multi-client compatible
3. **Simple Fix:** One-line port number change
4. **User Impact:** High - enables multi-application workflows
5. **Risk Level:** Low - minimal code changes required

## Related Documentation

- **Architecture:** `/docs/prds/radio-integration.md`
- **TCI Notes:** `/docs/prds/tci-cluster-tuning.md`
- **Code:** `/src/Log4YM.Server/Services/TciRadioService.cs`

## Questions & Contact

For questions about this investigation:
- Review the three documents in this folder
- Check the TCI specification: https://github.com/ExpertSDR3/TCI
- Review Log4YM issue tracker for related discussions

---

**Investigation Date:** 2026-03-03
**Status:** Complete ✅
**Next Step:** Implement recommendations from IMPLEMENTATION_PLAN.md
