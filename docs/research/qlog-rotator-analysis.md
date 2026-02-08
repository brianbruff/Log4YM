# QLog Rotator Configuration Research

## Research Objective
Understand QLog's rotator configuration implementation to enhance Log4YM's rotator settings UI.

## Current Log4YM Implementation Analysis

### 1. Current Rotator Settings Structure

**Location**: `/home/runner/work/Log4YM/Log4YM/src/Log4YM.Contracts/Models/Settings.cs` (lines 102-127)

```csharp
public class RotatorSettings
{
    public bool Enabled { get; set; }
    public string IpAddress { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 4533;  // Default hamlib rotctld port
    public int PollingIntervalMs { get; set; } = 500;
    public string RotatorId { get; set; } = "default";
    public List<RotatorPreset> Presets { get; set; } = new()
    {
        new RotatorPreset { Name = "N", Azimuth = 0 },
        new RotatorPreset { Name = "E", Azimuth = 90 },
        new RotatorPreset { Name = "S", Azimuth = 180 },
        new RotatorPreset { Name = "W", Azimuth = 270 },
    };
}
```

**Current Limitations**:
- Only supports TCP connection to rotctld
- No rotator model selection
- No direct serial port configuration
- No multiple rotator profiles
- Assumes rotctld is already running externally
- No hamlib model enumeration
- No serial port/baud rate settings

### 2. Current UI Implementation

**Location**: `/home/runner/work/Log4YM/Log4YM/src/Log4YM.Web/src/components/SettingsPanel.tsx` (lines 420-789)

**Current Features**:
- Enable/disable toggle
- IP address and port configuration
- Connection testing
- Polling interval adjustment
- Rotator ID field
- Expandable setup help with installation instructions
- Example commands for different platforms

**Missing Features**:
- Rotator model selection dropdown
- Direct serial port configuration
- Baud rate selection
- Multiple rotator profiles
- Hamlib capability detection
- Advanced hamlib parameters

### 3. Current Backend Implementation

**Location**: `/home/runner/work/Log4YM/Log4YM/src/Log4YM.Server/Services/RotatorService.cs`

**Current Approach**:
- Background service that polls rotctld via TCP
- Uses hamlib's text protocol (commands: `p`, `P`, `S`)
- Assumes rotctld daemon is managed externally
- No spawning of rotctld process
- No direct hamlib library integration

## What to Research in QLog Repository

### 1. Rotator Configuration UI Files to Examine

**Primary locations to check**:
- `ui/` or `src/ui/` directory
- Files containing "rotator", "rotor", "hamlib" in name
- Settings/preferences dialog files
- Likely Qt QML or C++ UI files

**Specific UI elements to identify**:
- Rotator model selection dropdown/combobox
- How they populate the list of available rotators
- Connection type selection (serial, network, USB)
- Serial port enumeration method
- Baud rate selection
- Profile management UI (add/edit/delete profiles)
- Test connection functionality
- Advanced settings panel

### 2. Configuration Data Structures

**Files to examine**:
- Settings classes or configuration files
- Rotator profile structures
- How they store multiple rotator configurations

**Key questions**:
```cpp
// What does their RotatorProfile structure look like?
struct RotatorProfile {
    QString name;                    // Profile name
    int model;                       // Hamlib model ID
    QString connectionType;          // "serial", "network", "usb"
    QString serialPort;              // e.g., "/dev/ttyUSB0", "COM3"
    int baudRate;                    // 9600, 19200, etc.
    QString networkHost;             // IP address for network rotators
    int networkPort;                 // Port for network rotators
    QString extraParams;             // Additional hamlib parameters
    bool autoConnect;                // Connect on startup
};
```

### 3. Hamlib Integration Approach

**Critical files to find**:
- Hamlib wrapper or integration layer
- How they enumerate available rotator models
- How they spawn/manage rotctld process
- Whether they use libhamlib directly or via rotctld

**Key questions**:
1. Do they use libhamlib directly or spawn rotctld?
2. How do they get the list of supported rotators?
   - Likely: `rotctl -l` command parsing
   - Or: Direct hamlib API calls
3. Do they manage the rotctld process lifecycle?
4. How do they handle serial port enumeration?
5. Do they support both rotctld daemon and direct serial?

### 4. Rotator Model Selection Implementation

**What to understand**:
```cpp
// How they populate the rotator model dropdown
QList<RotatorModel> getAvailableRotators() {
    // Option 1: Parse output of `rotctl -l`
    // Option 2: Use hamlib API rig_list_foreach()
    // Option 3: Static list from hamlib headers
}

// Common rotator models to look for:
// 1   - Dummy rotator (testing)
// 202 - Easycomm 1
// 204 - Easycomm 2
// 601 - Yaesu GS-232A
// 603 - Yaesu GS-232B
// 901 - SPID Rot2Prog
// 902 - SPID Rot1Prog
// 1201 - Alpha SPID
```

### 5. Multiple Profile Management

**UI patterns to identify**:
- Profile list/table view
- Add/Edit/Delete profile buttons
- Active profile indicator
- Profile switching mechanism
- Import/Export profiles functionality

**Storage approach**:
- How are profiles stored? (JSON, XML, database, Qt Settings)
- Per-user or global profiles?
- Default profile selection

### 6. Connection Type Handling

**Interface types to look for**:

1. **Serial Connection**:
   - Port selection (dropdown populated from OS)
   - Baud rate: 1200, 2400, 4800, 9600, 19200, 38400, 57600, 115200
   - Data bits, stop bits, parity
   - Flow control (RTS/CTS, XON/XOFF)

2. **Network Connection**:
   - TCP or UDP selection
   - Hostname/IP address
   - Port number
   - Timeout settings

3. **USB Connection**:
   - USB device enumeration
   - VID/PID selection

### 7. Advanced Hamlib Parameters

**Parameters QLog might expose**:
- Debug level (hamlib verbosity)
- Retry count
- Timeout values
- PTT settings (if combined rig/rotator)
- VFO settings
- Custom initialization commands

### 8. Error Handling & User Feedback

**UI patterns**:
- Connection status indicators
- Error message display
- Troubleshooting help text
- Link to hamlib documentation
- Firmware update recommendations

## Recommended Enhancements for Log4YM

Based on typical hamlib integration patterns, here's what Log4YM should add:

### Phase 1: Basic Model Selection
1. **Add rotator model field** to `RotatorSettings`:
   ```csharp
   public int HamlibModelId { get; set; } = 1; // 1 = Dummy
   public string ModelName { get; set; } = "Dummy";
   ```

2. **Create rotator model list** endpoint:
   ```csharp
   // Parse output of `rotctl -l` command
   // Return: List<RotatorModel> { Id, Manufacturer, Model }
   ```

3. **Add dropdown to UI** for model selection:
   ```tsx
   <select value={rotator.hamlibModelId}>
     {rotatorModels.map(m =>
       <option value={m.id}>{m.manufacturer} {m.model}</option>
     )}
   </select>
   ```

### Phase 2: Direct Serial Support
1. **Add connection type selection**:
   ```csharp
   public string ConnectionType { get; set; } = "network"; // "network" | "serial"
   public string SerialPort { get; set; } = "";
   public int BaudRate { get; set; } = 9600;
   ```

2. **Add serial port enumeration** API:
   ```csharp
   // Use System.IO.Ports.SerialPort.GetPortNames()
   // Return list of available COM ports
   ```

3. **Spawn rotctld process** from backend:
   ```csharp
   // When ConnectionType == "serial":
   // Process.Start("rotctld", $"-m {ModelId} -r {SerialPort} -s {BaudRate}")
   // Then connect to localhost:4533
   ```

### Phase 3: Multiple Profiles
1. **Add profile structure**:
   ```csharp
   public class RotatorProfile
   {
       public string Id { get; set; }
       public string Name { get; set; }
       public int HamlibModelId { get; set; }
       public string ConnectionType { get; set; }
       public string SerialPort { get; set; }
       public int BaudRate { get; set; }
       public string NetworkHost { get; set; }
       public int NetworkPort { get; set; }
   }

   public List<RotatorProfile> Profiles { get; set; }
   public string ActiveProfileId { get; set; }
   ```

2. **UI for profile management**:
   - Profile selector dropdown
   - "Add Profile" button
   - Edit/Delete profile actions
   - Profile configuration dialog

### Phase 4: Advanced Features
1. **Hamlib debug logging**
2. **Auto-detect rotators** (scan USB devices)
3. **Calibration settings** (azimuth offset, min/max limits)
4. **Speed control** (if supported by rotator)
5. **Parking position** (auto-park on shutdown)

## QLog Repository Files to Search For

### Search Patterns
```bash
# In QLog repository:
find . -name "*rotat*" -o -name "*hamlib*"
grep -r "rotator" --include="*.cpp" --include="*.h" --include="*.qml"
grep -r "rotctl" --include="*.cpp" --include="*.h"
grep -r "rig_model" --include="*.cpp" --include="*.h"
```

### Likely File Locations
```
qlog/
├── ui/
│   ├── RotatorSettingsDialog.cpp
│   ├── RotatorSettingsDialog.ui
│   └── RotatorWidget.cpp
├── models/
│   ├── RotatorProfile.cpp
│   └── SqliteModels.h (rotator_profiles table)
├── core/
│   ├── HamlibRotator.cpp
│   ├── RotatorManager.cpp
│   └── SerialPort.cpp
├── data/
│   └── Database.sql (schema for rotator configs)
└── docs/
    └── rotator-setup.md
```

## Specific QLog Features to Document

### 1. Model Selection UI
- Screenshot or description of the model selection dropdown
- How they organize models (by manufacturer? alphabetically?)
- Search/filter functionality
- "Popular models" quick selection

### 2. Connection Wizard
- Step-by-step setup process
- Auto-detection features
- Test connection at each step
- Troubleshooting tips

### 3. Profile Management
- How profiles are displayed
- Switching between profiles
- Cloning profiles
- Import/export functionality

### 4. Serial Port Handling
- Port auto-refresh mechanism
- Permission error handling (Linux /dev/ttyUSB0)
- Windows COM port detection
- macOS /dev/cu.* handling

### 5. Network Configuration
- Support for rotctld over network
- Multiple network rotators
- Firewall configuration help

## Expected QLog Files Content Examples

### Example: Rotator Settings Dialog (Qt)
```cpp
// RotatorSettingsDialog.cpp
void RotatorSettingsDialog::populateRotatorModels() {
    // Execute: rotctl -l
    QProcess process;
    process.start("rotctl", QStringList() << "-l");
    process.waitForFinished();

    QString output = process.readAllStandardOutput();
    QStringList lines = output.split("\n");

    foreach(QString line, lines) {
        // Parse: "  1    Hamlib    Dummy"
        QStringList parts = line.split(QRegExp("\\s+"));
        if (parts.count() >= 3) {
            int id = parts[0].toInt();
            QString mfg = parts[1];
            QString model = parts[2];
            ui->rotatorModelCombo->addItem(
                QString("%1 %2").arg(mfg, model),
                id
            );
        }
    }
}
```

### Example: Profile Structure
```cpp
// RotatorProfile.h
class RotatorProfile {
public:
    QString id;
    QString name;
    int hamlibModelId;
    ConnectionType connType; // SERIAL, NETWORK, USB
    QString serialDevice;
    int baudRate;
    QString networkHost;
    int networkPort;
    QMap<QString, QString> extraParams;

    void save();
    static QList<RotatorProfile> loadAll();
};
```

## Integration Recommendations for Log4YM

### Short-term (Current Sprint)
1. Add rotator model selection dropdown
2. Parse `rotctl -l` output to populate models
3. Store selected model ID in settings
4. Display model name in setup help

### Medium-term
1. Add serial port configuration option
2. Enumerate available serial ports
3. Add connection type radio buttons (TCP/Serial)
4. Spawn rotctld process when using serial

### Long-term
1. Multiple rotator profile support
2. Auto-detect USB rotators
3. Calibration settings UI
4. Import/export configurations
5. Advanced hamlib parameter exposure

## Research Deliverables

Once QLog repository access is available, document:

1. **UI Screenshots**:
   - Rotator settings dialog
   - Model selection dropdown
   - Profile management interface
   - Serial port configuration

2. **Code Snippets**:
   - Model enumeration method
   - Profile data structure
   - Connection establishment code
   - Error handling patterns

3. **Configuration Files**:
   - Example saved profile JSON/XML
   - Database schema for profiles
   - Default settings

4. **Documentation**:
   - User-facing setup instructions
   - Developer integration notes
   - Supported rotator list

## Questions for QLog Analysis

1. Does QLog manage the rotctld process lifecycle or assume external management?
2. How do they handle rotctld crashes/disconnections?
3. What's their approach to testing without physical hardware?
4. Do they support rotator calibration/offset settings?
5. How do they handle rotators with limited range (non-360°)?
6. Do they support elevation control for az/el rotators?
7. What's their approach to rotator speed control?
8. How do they handle multiple simultaneous rotators?

## Useful Hamlib Commands to Test

```bash
# List all rotators
rotctl -l

# Test connection (model 1 = dummy)
rotctl -m 1

# Get position
rotctl -m 1 -r /dev/ttyUSB0 -s 9600

# Common model numbers:
# 1 - Dummy (testing)
# 202 - Easycomm 1
# 204 - Easycomm 2
# 601 - GS-232A
# 603 - GS-232B
# 901 - SPID Rot2Prog
# 902 - SPID Rot1Prog
```

## Next Steps

1. **Access QLog repository** at https://github.com/foldynl/QLog
2. **Clone locally** for detailed code examination
3. **Search for files** matching rotator patterns
4. **Document UI approach** with screenshots
5. **Extract code patterns** for model selection and profile management
6. **Create enhancement proposal** for Log4YM based on findings
7. **Prioritize features** for implementation roadmap

---

**Research Status**: Pending web/repository access
**Last Updated**: 2026-02-08
**Researcher**: Claude Code Agent
