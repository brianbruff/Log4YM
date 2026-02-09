# 32-bit Windows Support Investigation

## Executive Summary

This document outlines the feasibility, requirements, and implementation steps for adding 32-bit Windows (win-x86/ia32) support to Log4YM.

**Status**: ✅ **VIABLE** - All components support 32-bit Windows

**Key Finding**: Supporting 32-bit Windows is technically feasible with minimal changes to the build system. All core dependencies (.NET 10, Electron, Hamlib, MongoDB.Driver) provide full 32-bit Windows support.

## Current Architecture Support

### Current State
Log4YM currently builds and distributes artifacts for:
- **Windows**: x64 only
- **macOS**: arm64 only (Apple Silicon)
- **Linux**: x64 only

### Components Analysis

#### 1. .NET 10 Runtime
- ✅ **Supports win-x86 (32-bit Windows)**
- Runtime Identifier: `win-x86`
- All .NET 10 components available: Runtime, Desktop Runtime, ASP.NET Core Runtime
- Can build self-contained applications for win-x86
- Source: [.NET 10 Download Page](https://dotnet.microsoft.com/download/dotnet/10.0)

#### 2. Electron
- ✅ **Supports ia32 (32-bit Windows)**
- Architecture identifier: `ia32`
- Official binaries provided for Windows ia32/x86
- electron-builder supports `--ia32` flag for building 32-bit installers
- Source: [Electron GitHub](https://github.com/electron/electron)

#### 3. Hamlib (Native Radio Control Library)
- ✅ **Provides 32-bit Windows binaries**
- Package naming: `hamlib-w32-<version>.zip` or `.exe`
- Both stable releases (e.g., 4.6.5) and daily snapshots available
- Includes all required DLL dependencies for 32-bit systems
- Source: [Hamlib Releases](https://github.com/Hamlib/Hamlib/releases)

#### 4. MongoDB.Driver NuGet Package
- ✅ **Platform-neutral, supports 32-bit Windows**
- Targets .NET 10.0, which supports win-x86
- No architecture-specific native dependencies
- Will run as 32-bit when application is built for x86
- Source: [MongoDB.Driver NuGet](https://www.nuget.org/packages/MongoDB.Driver)

#### 5. Other Dependencies
- System.IO.Ports: ✅ Supports win-x86
- SignalR: ✅ Platform-neutral .NET library
- Serilog: ✅ Platform-neutral .NET library
- React/Vite frontend: ✅ Not architecture-dependent (JavaScript)

## Required Changes

### 1. Electron Configuration (`src/Log4YM.Desktop/electron-builder.yml`)

**Current:**
```yaml
win:
  target:
    - target: nsis
      arch: [x64]
```

**Proposed:**
```yaml
win:
  target:
    - target: nsis
      arch: [x64, ia32]
```

This change enables electron-builder to create both 64-bit and 32-bit Windows installers.

### 2. Electron Main Process (`src/Log4YM.Desktop/main.js`)

**Current (Line 65-66):**
```javascript
if (platform === 'win32') {
  runtimeId = 'win-x64';
```

**Proposed:**
```javascript
if (platform === 'win32') {
  runtimeId = process.arch === 'ia32' ? 'win-x86' : 'win-x64';
```

This change allows Electron to detect and load the correct .NET backend based on the architecture it's running on.

### 3. Package.json Scripts (`src/Log4YM.Desktop/package.json`)

**Add new scripts:**
```json
"copy:frontend:win-x86": "cp -r ../Log4YM.Web/dist ../Log4YM.Server/bin/Release/net10.0/win-x86/publish/wwwroot",
"build:backend:win-x86": "dotnet publish ../Log4YM.Server -c Release --self-contained -r win-x86 -o ../Log4YM.Server/bin/Release/net10.0/win-x86/publish && npm run copy:frontend:win-x86",
"package:win-x86": "npm run build:frontend && npm run build:backend:win-x86 && electron-builder --win --ia32"
```

These scripts enable building the 32-bit backend and packaging the 32-bit Electron application.

### 4. Native Hamlib Libraries (`src/Log4YM.Server/runtimes/`)

**Create new directory:**
```
src/Log4YM.Server/runtimes/win-x86/native/
```

**Download and place 32-bit Hamlib DLLs:**
1. Download `hamlib-w32-4.6.5.zip` from [Hamlib Releases](https://github.com/Hamlib/Hamlib/releases/tag/4.6.5)
2. Extract the following files to `runtimes/win-x86/native/`:
   - `libhamlib-4.dll`
   - `libgcc_s_seh-1.dll` (or `libgcc_s_dw2-1.dll` for 32-bit)
   - `libwinpthread-1.dll`
   - `libusb-1.0.dll`

**Note**: The 32-bit version may use different GCC runtime DLL names (e.g., `libgcc_s_dw2-1.dll` instead of `libgcc_s_seh-1.dll`). Ensure all DLLs from the w32 package are included.

### 5. GitHub Actions Workflow (`.github/workflows/release.yml`)

**Add new matrix entry:**
```yaml
- os: windows-latest
  platform: win
  dotnet_rid: win-x86
  electron_arch: ia32
```

This enables automated building of 32-bit Windows artifacts on version releases.

### 6. Documentation Updates

Update `src/Log4YM.Server/runtimes/README.md`:
- Add section for Windows x86 (32-bit)
- Document bundled 32-bit Hamlib libraries
- Update directory structure diagram

## Build Process for 32-bit Windows

### Local Development Build

```bash
cd src/Log4YM.Desktop

# Build 32-bit Windows application
npm run package:win-x86
```

### CI/CD Build (GitHub Actions)

On version tag push (e.g., `v1.7.0`), the workflow will:
1. Build frontend (architecture-neutral)
2. Build .NET backend for win-x86 (self-contained)
3. Copy frontend to backend wwwroot
4. Package with electron-builder for ia32
5. Create installer: `Log4YM-<version>-win-ia32.exe`

## Testing Considerations

### Minimum System Requirements (32-bit)
- **OS**: Windows 10 32-bit or Windows 11 32-bit (if available)
- **RAM**: 2GB minimum (4GB recommended)
- **Disk**: 200MB for application
- **.NET**: Not required (self-contained deployment)

### Test Scenarios
1. **Installation**: Verify installer works on 32-bit Windows 10
2. **Backend Startup**: Confirm .NET backend starts and binds to port
3. **Frontend Loading**: Verify Electron loads React frontend
4. **Radio Control**: Test Hamlib integration with connected radio
5. **Database**: Confirm MongoDB connectivity (if using 32-bit MongoDB server)
6. **DX Cluster**: Test network connectivity and parsing
7. **Logging**: Verify QSO logging and retrieval

### Known Limitations on 32-bit Systems
- **Memory**: 32-bit processes limited to ~2GB address space
  - Large log files or extensive QSO history may hit memory limits
  - Consider pagination/lazy loading for large datasets
- **Performance**: May be slower than 64-bit on same hardware
- **Modern Hardware**: Most modern PCs are 64-bit; 32-bit primarily for legacy systems

## Distribution Strategy

### Artifact Naming
- **64-bit**: `Log4YM-<version>-win-x64.exe`
- **32-bit**: `Log4YM-<version>-win-ia32.exe` (or `win-x86`)

### Release Notes Guidance
Recommend users install 64-bit version unless they specifically need 32-bit:
- 64-bit Windows users: Download `win-x64` installer
- 32-bit Windows users: Download `win-ia32` installer
- Unsure? Right-click "This PC" → Properties → Check "System type"

## Estimated Effort

### Implementation Time
- **Code changes**: 1-2 hours
- **Hamlib library acquisition**: 30 minutes
- **Testing**: 2-4 hours (requires 32-bit Windows VM/machine)
- **Documentation updates**: 1 hour
- **Total**: ~5-8 hours

### Maintenance Burden
- **Low**: Automated builds handle both architectures
- **Hamlib updates**: Must download both w64 and w32 packages
- **Testing**: Requires access to 32-bit Windows for verification

## Recommendations

### ✅ Recommended: Add 32-bit Support

**Reasons:**
1. **Low Effort**: Minimal code changes required
2. **Full Compatibility**: All dependencies support 32-bit
3. **User Accessibility**: Serves users on legacy hardware
4. **Amateur Radio Community**: Some operators use older shack computers
5. **Automated Builds**: CI/CD handles both architectures equally

### Implementation Priority
1. **Phase 1** (High Priority):
   - Update Electron config to build ia32
   - Add win-x86 backend build scripts
   - Download and commit 32-bit Hamlib libraries
   - Update main.js architecture detection

2. **Phase 2** (Medium Priority):
   - Add GitHub Actions workflow for automated builds
   - Update documentation

3. **Phase 3** (Low Priority):
   - Test on 32-bit Windows 10
   - Gather user feedback
   - Address any 32-bit-specific issues

### Long-term Considerations
- **Microsoft Support**: Windows 10 32-bit is still supported until October 2025
- **Windows 11**: No official 32-bit version available
- **Future**: 32-bit support may become less important over time
- **Recommendation**: Support 32-bit while demand exists, but don't prioritize 32-bit-specific features

## Alternative: No 32-bit Support

If deciding **not** to support 32-bit:

**Document minimum requirements clearly:**
- Website/README: "Requires 64-bit Windows 10 or later"
- Installer: Could add architecture check (though users would discover during download)

**User impact:**
- Small subset of users unable to run application
- Users would need to upgrade to 64-bit Windows or use different hardware

## Conclusion

Adding 32-bit Windows support to Log4YM is **technically feasible and recommended**. The implementation effort is minimal (5-8 hours), all dependencies are compatible, and it extends the application's reach to users on legacy hardware. Given the amateur radio community's mix of modern and older equipment, providing 32-bit support aligns with the project's accessibility goals.

The primary requirement is acquiring and bundling the 32-bit Hamlib libraries, which are freely available from the Hamlib project. All other changes are configuration adjustments to existing build pipelines.

## Next Steps

If proceeding with 32-bit support:
1. Download Hamlib 4.6.5 w32 binaries
2. Create `runtimes/win-x86/native/` directory
3. Update `electron-builder.yml` with ia32 arch
4. Update `main.js` with architecture detection
5. Add win-x86 build scripts to package.json
6. Test locally with `npm run package:win-x86`
7. Update GitHub Actions workflow
8. Create test plan for 32-bit Windows
9. Update user documentation

---

**Document Version**: 1.0
**Date**: 2026-02-08
**Author**: Investigation by Claude Code Agent
