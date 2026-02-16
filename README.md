# Log4YM

**Modern amateur radio logging** — because Log4OM is for Old Men.

> **Alpha Software** — Log4YM is under active development. Features may change and bugs are expected.

![Log4YM](screenshots/hero.png)

Log4YM is an amateur radio logger for Windows, macOS, and Linux with drag-and-drop panels, real-time DX cluster, interactive maps, AI talk points, and cloud sync via MongoDB.

## [Read the Wiki](https://github.com/brianbruff/Log4YM/wiki)

Everything you need — quick start, feature guides, hardware setup, and developer docs — lives in the **[Wiki](https://github.com/brianbruff/Log4YM/wiki)**.

## Download

Grab the latest release from the **[Releases page](https://github.com/brianbruff/Log4YM/releases/latest)**.

| Platform | File | Notes |
|----------|------| ------ |
| Windows | `.exe` installer ||
| macOS (Apple Silicon) | `.dmg` (arm64) | see quick start wiki |
| macOS (Intel) | `.dmg` (x64) | see quick start wiki |
| Linux | `.AppImage` ||

### macOS — Removing Gatekeeper Warning

Log4YM is not signed with an Apple Developer certificate, so macOS will block it on first launch. To fix this, open Terminal and run:

```bash
xattr -cr /Applications/Log4YM.app
```

If you still see a security warning, go to **System Settings → Privacy & Security** and click **Open Anyway**.

## License

MIT

---

*73 de EI6LF*
