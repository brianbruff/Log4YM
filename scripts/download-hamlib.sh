#!/usr/bin/env bash
set -euo pipefail

# Download and provision Hamlib native binaries for each platform.
# Uses official pre-built binaries: GitHub releases (Windows), Homebrew (macOS), apt (Linux).
#
# Usage:
#   ./scripts/download-hamlib.sh [--platform auto|macos|linux|windows] [--version 4.6.5]
#
# When run without arguments, auto-detects the current platform.
# In CI, pass --platform explicitly to match the build target.

HAMLIB_VERSION="${HAMLIB_VERSION:-4.6.5}"
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
RUNTIMES_DIR="$REPO_ROOT/src/Log4YM.Server/runtimes"

PLATFORM="auto"

while [[ $# -gt 0 ]]; do
    case "$1" in
        --platform) PLATFORM="$2"; shift 2 ;;
        --version) HAMLIB_VERSION="$2"; shift 2 ;;
        *) echo "Unknown option: $1"; exit 1 ;;
    esac
done

if [[ "$PLATFORM" == "auto" ]]; then
    case "$(uname -s)" in
        Darwin) PLATFORM="macos" ;;
        Linux)  PLATFORM="linux" ;;
        MINGW*|MSYS*|CYGWIN*) PLATFORM="windows" ;;
        *) echo "Unsupported OS: $(uname -s)"; exit 1 ;;
    esac
fi

echo "==> Provisioning Hamlib $HAMLIB_VERSION for $PLATFORM"

provision_macos() {
    local arch
    arch="$(uname -m)"

    if [[ "$arch" == "arm64" ]]; then
        local rid="osx-arm64"
        local brew_prefix="/opt/homebrew"
    else
        local rid="osx-x64"
        local brew_prefix="/usr/local"
    fi

    local dest="$RUNTIMES_DIR/$rid/native"
    mkdir -p "$dest"

    # Install via Homebrew (official pre-built bottles)
    if ! brew list hamlib &>/dev/null; then
        echo "  Installing hamlib via Homebrew..."
        brew install hamlib
    else
        echo "  Hamlib already installed via Homebrew"
    fi

    local hamlib_lib="$brew_prefix/opt/hamlib/lib/libhamlib.4.dylib"
    local libusb_lib="$brew_prefix/opt/libusb/lib/libusb-1.0.0.dylib"

    if [[ ! -f "$hamlib_lib" ]]; then
        echo "ERROR: Hamlib library not found at $hamlib_lib"
        exit 1
    fi

    echo "  Copying libraries to $dest..."
    cp "$hamlib_lib" "$dest/libhamlib.4.dylib"

    # Bundle libusb dependency if present
    if [[ -f "$libusb_lib" ]]; then
        cp "$libusb_lib" "$dest/libusb-1.0.0.dylib"

        # Fix rpaths so libhamlib finds libusb relative to itself
        echo "  Fixing library rpaths..."
        install_name_tool -change \
            "$brew_prefix/opt/libusb/lib/libusb-1.0.0.dylib" \
            "@loader_path/libusb-1.0.0.dylib" \
            "$dest/libhamlib.4.dylib" 2>/dev/null || true

        # Update the library's own install name to be portable
        install_name_tool -id \
            "@loader_path/libhamlib.4.dylib" \
            "$dest/libhamlib.4.dylib" 2>/dev/null || true

        install_name_tool -id \
            "@loader_path/libusb-1.0.0.dylib" \
            "$dest/libusb-1.0.0.dylib" 2>/dev/null || true
    else
        echo "  WARNING: libusb not found at $libusb_lib - USB radio support may not work"
    fi

    # Re-sign libraries after rpath modifications (install_name_tool invalidates
    # the original Homebrew signatures; unsigned dylibs cause SIGKILL on macOS)
    echo "  Re-signing libraries (ad-hoc)..."
    codesign --force --sign - "$dest/libhamlib.4.dylib"
    if [[ -f "$dest/libusb-1.0.0.dylib" ]]; then
        codesign --force --sign - "$dest/libusb-1.0.0.dylib"
    fi

    echo "  Verifying library dependencies..."
    otool -L "$dest/libhamlib.4.dylib" | grep -v "/usr/lib/" | grep -v "@loader_path" | grep -v "libhamlib" | head -5 || true

    echo "  Done: $rid"
    ls -lh "$dest/"
}

provision_linux() {
    local arch
    arch="$(uname -m)"

    if [[ "$arch" == "x86_64" ]]; then
        local rid="linux-x64"
        local lib_dir="/usr/lib/x86_64-linux-gnu"
        local lib_dir_alt="/usr/lib64"
    elif [[ "$arch" == "aarch64" ]]; then
        local rid="linux-arm64"
        local lib_dir="/usr/lib/aarch64-linux-gnu"
        local lib_dir_alt="/usr/lib64"
    else
        echo "ERROR: Unsupported Linux architecture: $arch"
        exit 1
    fi

    local dest="$RUNTIMES_DIR/$rid/native"
    mkdir -p "$dest"

    # Install via apt (Debian/Ubuntu) or dnf (Fedora/RHEL)
    local hamlib_so=""

    if command -v apt-get &>/dev/null; then
        if ! dpkg -l libhamlib4 &>/dev/null 2>&1 && ! dpkg -l libhamlib4t64 &>/dev/null 2>&1; then
            echo "  Installing libhamlib via apt..."
            sudo apt-get update -qq
            # Ubuntu 24.04+ uses libhamlib4t64, older uses libhamlib4
            sudo apt-get install -y libhamlib4t64 2>/dev/null || sudo apt-get install -y libhamlib4
        else
            echo "  Hamlib already installed via apt"
        fi

        # Find the actual .so file
        hamlib_so="$(find "$lib_dir" "$lib_dir_alt" /usr/local/lib /usr/lib -maxdepth 1 -name 'libhamlib.so.4*' -type f 2>/dev/null | head -1)"

    elif command -v dnf &>/dev/null; then
        if ! rpm -q hamlib-libs &>/dev/null 2>&1; then
            echo "  Installing hamlib via dnf..."
            sudo dnf install -y hamlib-libs
        else
            echo "  Hamlib already installed via dnf"
        fi
        hamlib_so="$(find "$lib_dir" "$lib_dir_alt" /usr/local/lib /usr/lib -maxdepth 1 -name 'libhamlib.so.4*' -type f 2>/dev/null | head -1)"

    elif command -v pacman &>/dev/null; then
        if ! pacman -Q hamlib &>/dev/null 2>&1; then
            echo "  Installing hamlib via pacman..."
            sudo pacman -S --noconfirm hamlib
        fi
        hamlib_so="$(find /usr/lib -maxdepth 1 -name 'libhamlib.so.4*' -type f 2>/dev/null | head -1)"
    else
        echo "ERROR: No supported package manager found (apt, dnf, pacman)"
        exit 1
    fi

    if [[ -z "$hamlib_so" ]]; then
        echo "ERROR: Could not find libhamlib.so.4 after installation"
        exit 1
    fi

    echo "  Found library: $hamlib_so"
    echo "  Copying to $dest..."
    cp "$hamlib_so" "$dest/libhamlib.so.4"

    # Bundle libusb dependency if present
    local libusb_so
    libusb_so="$(find "$lib_dir" "$lib_dir_alt" /usr/local/lib /usr/lib -maxdepth 1 -name 'libusb-1.0.so*' -type f 2>/dev/null | head -1)"
    if [[ -n "$libusb_so" ]]; then
        cp "$libusb_so" "$dest/libusb-1.0.so.0"
        echo "  Bundled libusb: $libusb_so"

        # Set RPATH so libhamlib finds libusb relative to itself
        if command -v patchelf &>/dev/null; then
            echo "  Patching RPATH..."
            patchelf --set-rpath '$ORIGIN' "$dest/libhamlib.so.4" 2>/dev/null || true
        else
            echo "  NOTE: patchelf not available - RPATH not patched (system libusb will be used as fallback)"
        fi
    fi

    echo "  Verifying library dependencies..."
    ldd "$dest/libhamlib.so.4" 2>/dev/null | head -10 || true

    echo "  Done: $rid"
    ls -lh "$dest/"
}

provision_windows() {
    local rid="win-x64"
    local dest="$RUNTIMES_DIR/$rid/native"
    mkdir -p "$dest"

    # Check if already provisioned
    if [[ -f "$dest/libhamlib-4.dll" ]]; then
        echo "  Windows x64 binaries already present"
        ls -lh "$dest/"
        return 0
    fi

    local url="https://github.com/Hamlib/Hamlib/releases/download/${HAMLIB_VERSION}/hamlib-w64-${HAMLIB_VERSION}.zip"
    local tmpdir
    tmpdir="$(mktemp -d)"

    echo "  Downloading Hamlib w64 $HAMLIB_VERSION..."
    curl -fsSL "$url" -o "$tmpdir/hamlib-w64.zip"

    echo "  Extracting..."
    unzip -q "$tmpdir/hamlib-w64.zip" -d "$tmpdir/hamlib"

    # Copy required DLLs from the bin directory
    local bindir
    bindir="$(find "$tmpdir/hamlib" -type d -name bin | head -1)"

    if [[ -z "$bindir" ]]; then
        echo "ERROR: Could not find bin directory in Hamlib release archive"
        rm -rf "$tmpdir"
        exit 1
    fi

    for dll in libhamlib-4.dll libgcc_s_seh-1.dll libwinpthread-1.dll libusb-1.0.dll; do
        if [[ -f "$bindir/$dll" ]]; then
            cp "$bindir/$dll" "$dest/"
            echo "  Copied: $dll"
        else
            echo "  WARNING: $dll not found in release"
        fi
    done

    rm -rf "$tmpdir"

    echo "  Done: $rid"
    ls -lh "$dest/"
}

case "$PLATFORM" in
    macos)   provision_macos ;;
    linux)   provision_linux ;;
    windows) provision_windows ;;
    *)       echo "Unknown platform: $PLATFORM"; exit 1 ;;
esac

echo ""
echo "==> Hamlib native binaries provisioned successfully"
