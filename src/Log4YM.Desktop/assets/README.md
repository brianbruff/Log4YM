# App Icons

This folder should contain the following icon files for packaging:

## Required Icons

- `icon.png` - 512x512 PNG for Linux/generic use
- `icon.ico` - Windows icon (multi-resolution: 16, 32, 48, 64, 128, 256)
- `icon.icns` - macOS icon (multi-resolution)

## Generating Icons

You can use tools like:

1. **IconSet** (macOS): Create an iconset folder and use `iconutil`
2. **electron-icon-maker**: `npm install -g electron-icon-maker`
   ```bash
   electron-icon-maker --input=source.png --output=./
   ```
3. **Online converters**: Various websites can convert PNG to ICO/ICNS

## Recommended Source

Start with a 1024x1024 PNG image and use it to generate all other formats.

## Current Status

Placeholder icons are included for development. Replace with proper Log4YM branding before release.
