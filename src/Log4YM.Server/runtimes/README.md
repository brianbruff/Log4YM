# Hamlib Native Libraries

This directory contains native Hamlib libraries for each supported platform. The application automatically loads the correct library based on the runtime platform.

## Bundled Libraries

Official pre-built **Hamlib** binaries are bundled for all platforms:

| Platform | RID | Source | Bundled |
|----------|-----|--------|---------|
| Windows x64 | win-x64 | [Hamlib GitHub Releases](https://github.com/Hamlib/Hamlib/releases) | Yes |
| Windows x86 | win-x86 | [Hamlib GitHub Releases](https://github.com/Hamlib/Hamlib/releases) | See win-x86/native/README.md |
| macOS ARM64 | osx-arm64 | Homebrew (`brew install hamlib`) | Yes |
| macOS x64 | osx-x64 | Homebrew (`brew install hamlib`) | CI only |
| Linux x64 | linux-x64 | apt (`libhamlib4`) | CI only |
| Linux ARM64 | linux-arm64 | apt (`libhamlib4`) | CI only |

"CI only" means binaries are provisioned during the release build. For local development, run the download script or install via your package manager.

## Directory Structure

```
runtimes/
├── win-x64/native/
│   ├── libhamlib-4.dll       (main library)
│   ├── libgcc_s_seh-1.dll    (dependency)
│   ├── libwinpthread-1.dll   (dependency)
│   └── libusb-1.0.dll        (dependency)
├── win-x86/native/
│   └── (see win-x86/native/README.md)
├── osx-arm64/native/
│   ├── libhamlib.4.dylib     (main library, rpaths fixed)
│   └── libusb-1.0.0.dylib    (dependency)
├── osx-x64/native/
│   └── (provisioned by CI or download script)
├── linux-x64/native/
│   ├── libhamlib.so.4         (provisioned by CI or download script)
│   └── libusb-1.0.so.0        (dependency)
└── linux-arm64/native/
    └── (provisioned by CI or download script)
```

## Provisioning Binaries

### Automated (recommended)

Use the download script to provision binaries for your current platform:

```bash
# Auto-detect platform
./scripts/download-hamlib.sh

# Specify platform explicitly
./scripts/download-hamlib.sh --platform macos
./scripts/download-hamlib.sh --platform linux
./scripts/download-hamlib.sh --platform windows
```

The script:
- **Windows**: Downloads from Hamlib GitHub releases
- **macOS**: Installs via Homebrew, bundles libusb, fixes rpaths with `install_name_tool`
- **Linux**: Installs via apt/dnf/pacman, bundles libusb, patches RPATH with `patchelf`

### Manual

#### Windows
Download from [Hamlib Releases](https://github.com/Hamlib/Hamlib/releases):
```bash
# Download and extract hamlib-w64-X.X.X.zip
# Copy all .dll files from bin/ to runtimes/win-x64/native/
```

#### macOS
```bash
brew install hamlib
# The download script handles copying and rpath fixing
./scripts/download-hamlib.sh --platform macos
```

#### Linux
```bash
# Debian/Ubuntu
sudo apt install libhamlib4
# Fedora/RHEL
sudo dnf install hamlib-libs
# Arch Linux
sudo pacman -S hamlib
```

The application also searches system library paths as a fallback when bundled libraries are not present.

## Minimum Version

Hamlib 4.5 or later is recommended for full compatibility.

## Troubleshooting

If the native library fails to load, check the server console for `[Hamlib]` prefixed messages which show:
- Which paths were searched
- Whether the library was loaded
- Any error messages

Common issues:
1. **Library not found** - Run `./scripts/download-hamlib.sh` or install via package manager
2. **Permission denied** - On Linux/macOS, verify the library has read permissions
3. **Missing dependencies** - Ensure libusb is bundled alongside libhamlib
4. **Architecture mismatch** - Ensure the library matches your CPU architecture (x64 vs x86 vs arm64)
5. **macOS rpath issues** - Use the download script which fixes rpaths automatically
