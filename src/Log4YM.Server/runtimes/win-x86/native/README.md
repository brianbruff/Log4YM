# Windows 32-bit (x86) Hamlib Native Libraries

This directory should contain the 32-bit Windows native libraries for Hamlib.

## Required Files

Download Hamlib 4.6.5 (or later) 32-bit Windows binaries from:
https://github.com/Hamlib/Hamlib/releases

Look for the file: `hamlib-w32-4.6.5.zip` or `hamlib-w32-4.6.5.exe`

## Files to Place Here

Extract and place the following DLL files from the w32 package:

1. **libhamlib-4.dll** - Main Hamlib library
2. **libgcc_s_dw2-1.dll** or **libgcc_s_seh-1.dll** - GCC runtime (check which one is in the w32 package)
3. **libwinpthread-1.dll** - Windows pthread library
4. **libusb-1.0.dll** - USB library

**Note**: The 32-bit version may use different GCC runtime DLL names compared to 64-bit:
- 64-bit uses: `libgcc_s_seh-1.dll` (SEH = Structured Exception Handling)
- 32-bit may use: `libgcc_s_dw2-1.dll` (DW2 = DWARF-2) or `libgcc_s_sjlj-1.dll` (SJLJ = SetJump/LongJump)

Include whichever GCC runtime DLL is provided in the w32 package.

## Installation Steps

```bash
# 1. Download the 32-bit Hamlib release
wget https://github.com/Hamlib/Hamlib/releases/download/4.6.5/hamlib-w32-4.6.5.zip

# 2. Extract the archive
unzip hamlib-w32-4.6.5.zip

# 3. Copy all DLL files from bin/ directory to this folder
cp hamlib-w32-4.6.5/bin/*.dll ./

# 4. Verify the files
ls -la
```

## Why This Directory Exists

When building a 32-bit (ia32) Electron application for Windows, the .NET backend will be compiled for win-x86. At runtime, the application will look for native Hamlib libraries in the `runtimes/win-x86/native/` directory.

Without these files, radio control functionality will not work in the 32-bit version.

## Compatibility

- **Hamlib Version**: 4.5 or later recommended
- **Architecture**: 32-bit Windows (x86/ia32)
- **OS Support**: Windows 10 32-bit, Windows 11 (no official 32-bit version)

## Size Considerations

The 32-bit Hamlib DLLs total approximately:
- libhamlib-4.dll: ~11 MB
- Dependencies: ~1-2 MB
- **Total**: ~12-13 MB

This will be included in the 32-bit installer package.
