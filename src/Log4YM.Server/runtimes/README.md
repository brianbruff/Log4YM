# Hamlib Native Libraries

This directory contains native Hamlib libraries for each supported platform. The application will automatically load the correct library based on the runtime platform.

## Bundled Libraries

**Hamlib 4.6.5** binaries are included for:
- **Windows x64** - All required DLLs bundled
- **macOS ARM64** - For Apple Silicon Macs

For other platforms, the application will automatically search system library paths.

## Directory Structure

```
runtimes/
├── win-x64/native/
│   ├── libhamlib-4.dll       (main library)
│   ├── libgcc_s_seh-1.dll    (dependency)
│   ├── libwinpthread-1.dll   (dependency)
│   └── libusb-1.0.dll        (dependency)
├── linux-x64/native/
│   └── (install via package manager - see below)
├── linux-arm64/native/
│   └── (install via package manager - see below)
├── osx-x64/native/
│   └── (install via Homebrew - see below)
└── osx-arm64/native/
    └── libhamlib.4.dylib     (bundled)
```

## Platform Setup

### Windows x64
**Pre-bundled** - No action required. Libraries are included in this repository.

### macOS ARM64 (Apple Silicon)
**Pre-bundled** - No action required. Libraries are included in this repository.

### macOS x64 (Intel)
Install via Homebrew (library will be auto-detected from system path):
```bash
brew install hamlib
```

Or copy to the runtimes folder:
```bash
cp /usr/local/opt/hamlib/lib/libhamlib.4.dylib runtimes/osx-x64/native/
```

### Linux (x64/ARM64)
Install via your package manager (library will be auto-detected from system path):

```bash
# Debian/Ubuntu
sudo apt install libhamlib4

# Fedora/RHEL
sudo dnf install hamlib

# Arch Linux
sudo pacman -S hamlib
```

The application searches these system paths automatically:
- `/usr/lib/x86_64-linux-gnu/libhamlib.so.4` (Debian x64)
- `/usr/lib/aarch64-linux-gnu/libhamlib.so.4` (Debian ARM64)
- `/usr/lib64/libhamlib.so.4` (Fedora/RHEL)
- `/usr/local/lib/libhamlib.so.4` (manual install)

## Updating Libraries

### Windows
Download from [Hamlib Releases](https://github.com/Hamlib/Hamlib/releases):
```bash
# Download and extract hamlib-w64-X.X.X.zip
# Copy all .dll files from bin/ to runtimes/win-x64/native/
```

### macOS
```bash
brew upgrade hamlib
cp /opt/homebrew/opt/hamlib/lib/libhamlib.4.dylib runtimes/osx-arm64/native/
# or for Intel:
cp /usr/local/opt/hamlib/lib/libhamlib.4.dylib runtimes/osx-x64/native/
```

## Minimum Version

Hamlib 4.5 or later is recommended for full compatibility.

## Troubleshooting

If the native library fails to load, check the server console for `[Hamlib]` prefixed messages which show:
- Which paths were searched
- Whether the library was loaded
- Any error messages

Common issues:
1. **Library not found** - Ensure library exists in `runtimes/{RID}/native/` or is installed system-wide
2. **Permission denied** - On Linux/macOS, verify the library has read permissions
3. **Missing dependencies** - On Windows, ensure all DLLs from the Hamlib release are present
4. **Architecture mismatch** - Ensure the library matches your CPU architecture (x64 vs arm64)
