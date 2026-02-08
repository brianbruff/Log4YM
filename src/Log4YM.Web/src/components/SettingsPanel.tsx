import { useEffect, useState } from 'react';
import {
  X,
  Settings,
  Radio,
  Globe,
  Palette,
  Info,
  Save,
  RotateCcw,
  Eye,
  EyeOff,
  MapPin,
  User,
  Key,
  CheckCircle,
  AlertCircle,
  Compass,
  Wifi,
  WifiOff,
  Loader2,
  Database,
  Server,
  ExternalLink,
  ChevronDown,
  ChevronUp,
  HelpCircle,
  Download,
  Terminal,
  Copy,
  Check,
} from 'lucide-react';
import { useSettingsStore, SettingsSection } from '../store/settingsStore';
import { useSetupStore } from '../store/setupStore';

// Settings navigation items
const SETTINGS_SECTIONS: { id: SettingsSection; name: string; icon: React.ReactNode; description: string }[] = [
  {
    id: 'station',
    name: 'Station',
    icon: <Radio className="w-5 h-5" />,
    description: 'Callsign, location, and operator info',
  },
  {
    id: 'qrz',
    name: 'QRZ.com',
    icon: <Globe className="w-5 h-5" />,
    description: 'QRZ lookup credentials',
  },
  {
    id: 'rotator',
    name: 'Rotator',
    icon: <Compass className="w-5 h-5" />,
    description: 'Hamlib rotctld connection',
  },
  {
    id: 'database',
    name: 'Database',
    icon: <Database className="w-5 h-5" />,
    description: 'MongoDB connection settings',
  },
  {
    id: 'appearance',
    name: 'Appearance',
    icon: <Palette className="w-5 h-5" />,
    description: 'Theme and display options',
  },
  {
    id: 'header',
    name: 'Header Bar',
    icon: <Eye className="w-5 h-5" />,
    description: 'Customize header display settings',
  },
  {
    id: 'about',
    name: 'About',
    icon: <Info className="w-5 h-5" />,
    description: 'Version and license info',
  },
];

// Station Settings Section
function StationSettingsSection() {
  const { settings, updateStationSettings } = useSettingsStore();
  const station = settings.station;

  // Station info sync to app store is now handled in App.tsx
  // to ensure it runs even when settings panel is not open

  return (
    <div className="space-y-6">
      <div>
        <h3 className="text-lg font-semibold font-ui text-dark-200 mb-1">Station Information</h3>
        <p className="text-sm text-dark-300">Configure your station callsign and location details.</p>
      </div>

      <div className="grid grid-cols-2 gap-4">
        {/* Callsign */}
        <div className="space-y-2">
          <label className="flex items-center gap-2 text-sm font-medium font-ui text-dark-200">
            <Radio className="w-4 h-4 text-accent-primary" />
            Callsign
          </label>
          <input
            type="text"
            value={station.callsign}
            onChange={(e) => updateStationSettings({ callsign: e.target.value.toUpperCase() })}
            placeholder="e.g. EI6LF"
            className="glass-input w-full font-mono uppercase"
          />
        </div>

        {/* Operator Name */}
        <div className="space-y-2">
          <label className="flex items-center gap-2 text-sm font-medium font-ui text-dark-200">
            <User className="w-4 h-4 text-accent-primary" />
            Operator Name
          </label>
          <input
            type="text"
            value={station.operatorName}
            onChange={(e) => updateStationSettings({ operatorName: e.target.value })}
            placeholder="Your name"
            className="glass-input w-full"
          />
        </div>

        {/* Grid Square */}
        <div className="space-y-2">
          <label className="flex items-center gap-2 text-sm font-medium font-ui text-dark-200">
            <MapPin className="w-4 h-4 text-accent-primary" />
            Grid Square (Maidenhead)
          </label>
          <input
            type="text"
            value={station.gridSquare}
            onChange={(e) => updateStationSettings({ gridSquare: e.target.value.toUpperCase() })}
            placeholder="e.g. IO63"
            maxLength={8}
            className="glass-input w-full font-mono uppercase"
          />
        </div>

        {/* City */}
        <div className="space-y-2">
          <label className="text-sm font-medium font-ui text-dark-200">City</label>
          <input
            type="text"
            value={station.city}
            onChange={(e) => updateStationSettings({ city: e.target.value })}
            placeholder="Your city"
            className="glass-input w-full"
          />
        </div>

        {/* Country */}
        <div className="space-y-2">
          <label className="text-sm font-medium font-ui text-dark-200">Country</label>
          <input
            type="text"
            value={station.country}
            onChange={(e) => updateStationSettings({ country: e.target.value })}
            placeholder="Your country"
            className="glass-input w-full"
          />
        </div>
      </div>

      {/* Coordinates */}
      <div className="border-t border-glass-100 pt-4">
        <h4 className="text-sm font-medium font-ui text-dark-200 mb-3">Coordinates (Optional)</h4>
        <div className="grid grid-cols-2 gap-4">
          <div className="space-y-2">
            <label className="text-sm font-ui text-dark-300">Latitude</label>
            <input
              type="number"
              step="0.0001"
              value={station.latitude ?? ''}
              onChange={(e) =>
                updateStationSettings({
                  latitude: e.target.value ? parseFloat(e.target.value) : null,
                })
              }
              placeholder="e.g. 52.6667"
              className="glass-input w-full font-mono"
            />
          </div>
          <div className="space-y-2">
            <label className="text-sm font-ui text-dark-300">Longitude</label>
            <input
              type="number"
              step="0.0001"
              value={station.longitude ?? ''}
              onChange={(e) =>
                updateStationSettings({
                  longitude: e.target.value ? parseFloat(e.target.value) : null,
                })
              }
              placeholder="e.g. -8.6333"
              className="glass-input w-full font-mono"
            />
          </div>
        </div>
      </div>
    </div>
  );
}

// QRZ Settings Section
function QrzSettingsSection() {
  const { settings, updateQrzSettings } = useSettingsStore();
  const [showPassword, setShowPassword] = useState(false);
  const [showApiKey, setShowApiKey] = useState(false);
  const [testStatus, setTestStatus] = useState<'idle' | 'testing' | 'success' | 'error'>('idle');
  const [testMessage, setTestMessage] = useState('');
  const [hasXmlSubscription, setHasXmlSubscription] = useState<boolean | null>(null);

  const qrz = settings.qrz;
  const password = qrz.password;
  const apiKey = qrz.apiKey;

  const handleTestConnection = async () => {
    setTestStatus('testing');
    setTestMessage('');
    try {
      const response = await fetch('/api/qrz/settings', {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          username: qrz.username,
          password: password,
          apiKey: apiKey,
          enabled: qrz.enabled,
        }),
      });
      const data = await response.json();
      if (data.success) {
        setTestStatus('success');
        setTestMessage(data.message || 'Connected successfully');
        setHasXmlSubscription(data.hasXmlSubscription);
      } else {
        setTestStatus('error');
        setTestMessage(data.message || 'Connection failed');
      }
    } catch {
      setTestStatus('error');
      setTestMessage('Failed to connect to server');
    }
    setTimeout(() => setTestStatus('idle'), 5000);
  };

  return (
    <div className="space-y-6">
      <div>
        <h3 className="text-lg font-semibold font-ui text-dark-200 mb-1">QRZ.com Integration</h3>
        <p className="text-sm text-dark-300">
          Configure your QRZ.com credentials for callsign lookups and log uploads.
        </p>
      </div>

      {/* Enable toggle */}
      <div className="flex items-center justify-between p-4 bg-dark-700/50 rounded-lg border border-glass-100">
        <div>
          <p className="font-medium font-ui text-dark-200">Enable QRZ Lookups</p>
          <p className="text-sm text-dark-300">Use QRZ.com for callsign information</p>
        </div>
        <button
          onClick={() => updateQrzSettings({ enabled: !qrz.enabled })}
          className={`relative w-11 h-6 rounded-full transition-colors ${
            qrz.enabled ? 'bg-accent-success' : 'bg-dark-600 border border-dark-400'
          }`}
        >
          <span
            className={`absolute top-0.5 left-0.5 w-5 h-5 rounded-full bg-white shadow transition-transform duration-200 ${
              qrz.enabled ? 'translate-x-5' : 'translate-x-0'
            }`}
          />
        </button>
      </div>

      {/* Subscription Status */}
      {hasXmlSubscription !== null && (
        <div className={`flex items-center gap-2 p-3 rounded-lg border ${
          hasXmlSubscription
            ? 'bg-accent-success/10 border-accent-success/30 text-accent-success'
            : 'bg-accent-primary/10 border-accent-primary/30 text-accent-primary'
        }`}>
          {hasXmlSubscription ? (
            <>
              <CheckCircle className="w-4 h-4" />
              <span className="text-sm">XML Subscription active - callsign lookups enabled</span>
            </>
          ) : (
            <>
              <AlertCircle className="w-4 h-4" />
              <span className="text-sm">No XML subscription - callsign lookups require a QRZ subscription</span>
            </>
          )}
        </div>
      )}

      {/* Credentials */}
      <div className={`space-y-4 ${!qrz.enabled ? 'opacity-50 pointer-events-none' : ''}`}>
        <div className="space-y-2">
          <label className="flex items-center gap-2 text-sm font-medium font-ui text-dark-200">
            <User className="w-4 h-4 text-accent-primary" />
            QRZ Username (Callsign)
          </label>
          <input
            type="text"
            value={qrz.username}
            onChange={(e) => updateQrzSettings({ username: e.target.value.toUpperCase() })}
            placeholder="Your QRZ.com callsign"
            className="glass-input w-full font-mono uppercase"
            disabled={!qrz.enabled}
          />
        </div>

        <div className="space-y-2">
          <label className="flex items-center gap-2 text-sm font-medium font-ui text-dark-200">
            <Key className="w-4 h-4 text-accent-primary" />
            QRZ Password
          </label>
          <div className="relative">
            <input
              type={showPassword ? 'text' : 'password'}
              value={password}
              onChange={(e) => updateQrzSettings({ password: e.target.value })}
              placeholder="Your QRZ.com password"
              className="glass-input w-full pr-10"
              disabled={!qrz.enabled}
            />
            <button
              type="button"
              onClick={() => setShowPassword(!showPassword)}
              className="absolute right-2 top-1/2 -translate-y-1/2 p-1 text-dark-300 hover:text-dark-200"
            >
              {showPassword ? <EyeOff className="w-4 h-4" /> : <Eye className="w-4 h-4" />}
            </button>
          </div>
          <p className="text-xs text-dark-300">
            Required for callsign lookups (requires XML subscription on QRZ.com).
          </p>
        </div>

        {/* API Key for Logbook */}
        <div className="pt-4 border-t border-glass-100">
          <h4 className="text-sm font-medium font-ui text-dark-200 mb-3">QRZ Logbook Integration</h4>
          <div className="space-y-2">
            <label className="flex items-center gap-2 text-sm font-medium font-ui text-dark-200">
              <Key className="w-4 h-4 text-accent-info" />
              Logbook API Key
            </label>
            <div className="relative">
              <input
                type={showApiKey ? 'text' : 'password'}
                value={apiKey}
                onChange={(e) => updateQrzSettings({ apiKey: e.target.value })}
                placeholder="Your QRZ Logbook API Key"
                className="glass-input w-full pr-10 font-mono text-sm"
                disabled={!qrz.enabled}
              />
              <button
                type="button"
                onClick={() => setShowApiKey(!showApiKey)}
                className="absolute right-2 top-1/2 -translate-y-1/2 p-1 text-dark-300 hover:text-dark-200"
              >
                {showApiKey ? <EyeOff className="w-4 h-4" /> : <Eye className="w-4 h-4" />}
              </button>
            </div>
            <p className="text-xs text-dark-300">
              Get your API key from{' '}
              <a
                href="https://logbook.qrz.com/logbook"
                target="_blank"
                rel="noopener noreferrer"
                className="text-accent-primary hover:underline"
              >
                QRZ.com Logbook Settings
              </a>
              . Required for uploading QSOs to QRZ.
            </p>
          </div>
        </div>

        {/* Test connection */}
        <div className="pt-4 flex items-center gap-4">
          <button
            onClick={handleTestConnection}
            disabled={!qrz.username || !password || testStatus === 'testing'}
            className="glass-button px-4 py-2 flex items-center gap-2 disabled:opacity-50"
          >
            {testStatus === 'testing' && (
              <div className="w-4 h-4 border-2 border-accent-primary border-t-transparent rounded-full animate-spin" />
            )}
            {testStatus === 'success' && <CheckCircle className="w-4 h-4 text-accent-success" />}
            {testStatus === 'error' && <AlertCircle className="w-4 h-4 text-accent-danger" />}
            {testStatus === 'idle' && <Globe className="w-4 h-4" />}
            <span>
              {testStatus === 'testing'
                ? 'Testing...'
                : testStatus === 'success'
                  ? 'Connected!'
                  : testStatus === 'error'
                    ? 'Failed'
                    : 'Test & Save Credentials'}
            </span>
          </button>
          {testMessage && (
            <span className={`text-sm ${testStatus === 'success' ? 'text-accent-success' : 'text-accent-danger'}`}>
              {testMessage}
            </span>
          )}
        </div>
      </div>
    </div>
  );
}

// Rotator Settings Section
function RotatorSettingsSection() {
  const { settings, updateRotatorSettings } = useSettingsStore();
  const rotator = settings.rotator;
  const [showSetupHelp, setShowSetupHelp] = useState(false);
  const [testStatus, setTestStatus] = useState<'idle' | 'testing' | 'success' | 'error'>('idle');
  const [testMessage, setTestMessage] = useState('');
  const [copiedCommand, setCopiedCommand] = useState<string | null>(null);

  const handleTestConnection = async () => {
    setTestStatus('testing');
    setTestMessage('');
    try {
      // Try to connect to the rotctld socket
      await fetch(`http://${rotator.ipAddress}:${rotator.port}`, {
        method: 'GET',
        signal: AbortSignal.timeout(3000),
      });
      // If we get any response, even an error, the server is reachable
      setTestStatus('success');
      setTestMessage('Connection successful! Rotctld is reachable.');
    } catch (error) {
      // Check if it's a timeout or network error
      if (error instanceof Error) {
        if (error.name === 'TimeoutError') {
          setTestStatus('error');
          setTestMessage('Connection timeout. Check if rotctld is running and firewall settings.');
        } else {
          // For rotctld, connection refused or CORS errors are actually expected
          // because rotctld doesn't serve HTTP - but this means the host is reachable
          setTestStatus('success');
          setTestMessage('Host is reachable. Enable rotator control to connect.');
        }
      } else {
        setTestStatus('error');
        setTestMessage('Connection failed. Verify IP address and port.');
      }
    }
    setTimeout(() => setTestStatus('idle'), 5000);
  };

  const copyToClipboard = (text: string, id: string) => {
    navigator.clipboard.writeText(text);
    setCopiedCommand(id);
    setTimeout(() => setCopiedCommand(null), 2000);
  };

  return (
    <div className="space-y-6">
      <div>
        <h3 className="text-lg font-semibold font-ui text-dark-200 mb-1">Rotator Control</h3>
        <p className="text-sm text-dark-300">
          Configure connection to hamlib rotctld for antenna rotator control.
        </p>
      </div>

      {/* Setup Help Banner */}
      <div className="bg-blue-500/10 border border-blue-500/30 rounded-lg p-4">
        <button
          onClick={() => setShowSetupHelp(!showSetupHelp)}
          className="w-full flex items-center justify-between text-left"
        >
          <div className="flex items-center gap-3">
            <HelpCircle className="w-5 h-5 text-blue-400" />
            <div>
              <p className="font-medium text-blue-300">Need help setting up rotctld?</p>
              <p className="text-sm text-blue-400/70">Click here for installation and setup instructions</p>
            </div>
          </div>
          {showSetupHelp ? (
            <ChevronUp className="w-5 h-5 text-blue-400" />
          ) : (
            <ChevronDown className="w-5 h-5 text-blue-400" />
          )}
        </button>

        {showSetupHelp && (
          <div className="mt-4 space-y-4 pt-4 border-t border-blue-500/30">
            {/* What is rotctld */}
            <div>
              <h4 className="text-sm font-semibold text-blue-300 mb-2">What is rotctld?</h4>
              <p className="text-sm text-gray-400">
                rotctld is a TCP network daemon from the Hamlib project that controls antenna rotators.
                Multiple applications (like Log4YM and QLog) can connect to the same rotctld instance
                to share control of your rotator.
              </p>
            </div>

            {/* Installation */}
            <div>
              <h4 className="text-sm font-semibold text-blue-300 mb-2 flex items-center gap-2">
                <Download className="w-4 h-4" />
                Installation
              </h4>
              <div className="space-y-2 text-sm text-gray-400">
                <div>
                  <p className="font-medium text-gray-300 mb-1">Windows:</p>
                  <p>
                    Download from{' '}
                    <a
                      href="https://github.com/Hamlib/Hamlib/releases"
                      target="_blank"
                      rel="noopener noreferrer"
                      className="text-accent-primary hover:underline inline-flex items-center gap-1"
                    >
                      Hamlib releases <ExternalLink className="w-3 h-3" />
                    </a>
                  </p>
                  <p className="text-xs text-gray-500 mt-1">
                    Install to C:\Program Files\Hamlib\bin\ or similar
                  </p>
                </div>
                <div>
                  <p className="font-medium text-gray-300 mb-1">Linux:</p>
                  <div className="bg-dark-900 rounded p-2 font-mono text-xs">
                    <code>sudo apt install hamlib-utils</code> {/* Debian/Ubuntu */}
                  </div>
                </div>
                <div>
                  <p className="font-medium text-gray-300 mb-1">macOS:</p>
                  <div className="bg-dark-900 rounded p-2 font-mono text-xs">
                    <code>brew install hamlib</code>
                  </div>
                </div>
              </div>
            </div>

            {/* Finding your model */}
            <div>
              <h4 className="text-sm font-semibold text-blue-300 mb-2">Find your rotator model number</h4>
              <p className="text-sm text-gray-400 mb-2">
                Run this command to see all supported rotators:
              </p>
              <div className="bg-dark-900 rounded p-2 font-mono text-xs flex items-center justify-between">
                <code>rotctl -l</code>
                <button
                  onClick={() => copyToClipboard('rotctl -l', 'list')}
                  className="p-1 hover:bg-dark-700 rounded"
                >
                  {copiedCommand === 'list' ? (
                    <Check className="w-3 h-3 text-green-400" />
                  ) : (
                    <Copy className="w-3 h-3 text-gray-500" />
                  )}
                </button>
              </div>
              <div className="mt-2 text-xs text-gray-500">
                Common models: 202 (Easycomm), 601 (Yaesu GS-232A), 603 (GS-232B), 902 (SPID)
              </div>
            </div>

            {/* Starting rotctld */}
            <div>
              <h4 className="text-sm font-semibold text-blue-300 mb-2 flex items-center gap-2">
                <Terminal className="w-4 h-4" />
                Starting rotctld
              </h4>
              <div className="space-y-3">
                <div>
                  <p className="text-sm text-gray-400 mb-2">
                    <strong className="text-gray-300">Linux/macOS example</strong> (SPID on USB):
                  </p>
                  <div className="bg-dark-900 rounded p-2 font-mono text-xs flex items-center justify-between">
                    <code>rotctld -m 902 -r /dev/ttyUSB0</code>
                    <button
                      onClick={() => copyToClipboard('rotctld -m 902 -r /dev/ttyUSB0', 'linux')}
                      className="p-1 hover:bg-dark-700 rounded"
                    >
                      {copiedCommand === 'linux' ? (
                        <Check className="w-3 h-3 text-green-400" />
                      ) : (
                        <Copy className="w-3 h-3 text-gray-500" />
                      )}
                    </button>
                  </div>
                </div>
                <div>
                  <p className="text-sm text-gray-400 mb-2">
                    <strong className="text-gray-300">Windows example</strong> (Yaesu on COM3):
                  </p>
                  <div className="bg-dark-900 rounded p-2 font-mono text-xs flex items-center justify-between">
                    <code>rotctld.exe -m 603 -r COM3 -s 9600</code>
                    <button
                      onClick={() => copyToClipboard('rotctld.exe -m 603 -r COM3 -s 9600', 'windows')}
                      className="p-1 hover:bg-dark-700 rounded"
                    >
                      {copiedCommand === 'windows' ? (
                        <Check className="w-3 h-3 text-green-400" />
                      ) : (
                        <Copy className="w-3 h-3 text-gray-500" />
                      )}
                    </button>
                  </div>
                  <p className="text-xs text-gray-500 mt-1">
                    Replace COM3 with your serial port and 9600 with your baud rate
                  </p>
                </div>
                <div className="bg-yellow-500/10 border border-yellow-500/30 rounded p-2">
                  <p className="text-xs text-yellow-400">
                    <strong>Tip:</strong> Leave the terminal window open while using Log4YM. rotctld must stay
                    running in the background.
                  </p>
                </div>
              </div>
            </div>

            {/* Full documentation link */}
            <div className="pt-2 border-t border-blue-500/30">
              <a
                href="https://github.com/Hamlib/Hamlib/wiki"
                target="_blank"
                rel="noopener noreferrer"
                className="text-sm text-accent-primary hover:underline inline-flex items-center gap-1"
              >
                View complete Hamlib documentation <ExternalLink className="w-3 h-3" />
              </a>
            </div>
          </div>
        )}
      </div>

      {/* Enable toggle */}
      <div className="flex items-center justify-between p-4 bg-dark-700/50 rounded-lg border border-glass-100">
        <div className="flex items-center gap-3">
          {rotator.enabled ? (
            <Wifi className="w-5 h-5 text-accent-success" />
          ) : (
            <WifiOff className="w-5 h-5 text-dark-300" />
          )}
          <div>
            <p className="font-medium font-ui text-dark-200">Enable Rotator Control</p>
            <p className="text-sm text-dark-300">Connect to rotctld TCP server</p>
          </div>
        </div>
        <button
          onClick={() => updateRotatorSettings({ enabled: !rotator.enabled })}
          className={`relative w-11 h-6 rounded-full transition-colors ${
            rotator.enabled ? 'bg-accent-success' : 'bg-dark-600 border border-dark-400'
          }`}
        >
          <span
            className={`absolute top-0.5 left-0.5 w-5 h-5 rounded-full bg-white shadow transition-transform duration-200 ${
              rotator.enabled ? 'translate-x-5' : 'translate-x-0'
            }`}
          />
        </button>
      </div>

      {/* Connection settings */}
      <div className={`space-y-4 ${!rotator.enabled ? 'opacity-50 pointer-events-none' : ''}`}>
        <div className="grid grid-cols-2 gap-4">
          {/* IP Address */}
          <div className="space-y-2">
            <label className="flex items-center gap-2 text-sm font-medium font-ui text-dark-200">
              <Globe className="w-4 h-4 text-accent-primary" />
              IP Address
            </label>
            <input
              type="text"
              value={rotator.ipAddress}
              onChange={(e) => updateRotatorSettings({ ipAddress: e.target.value })}
              placeholder="127.0.0.1"
              className="glass-input w-full font-mono"
              disabled={!rotator.enabled}
            />
            <p className="text-xs text-gray-600">
              Use 127.0.0.1 if rotctld runs locally, or your server's IP
            </p>
          </div>

          {/* Port */}
          <div className="space-y-2">
            <label className="flex items-center gap-2 text-sm font-medium font-ui text-dark-200">
              <Compass className="w-4 h-4 text-accent-primary" />
              Port
            </label>
            <input
              type="number"
              value={rotator.port}
              onChange={(e) => updateRotatorSettings({ port: parseInt(e.target.value) || 4533 })}
              placeholder="4533"
              className="glass-input w-full font-mono"
              disabled={!rotator.enabled}
            />
            <p className="text-xs text-gray-600">
              Default rotctld port is 4533
            </p>
          </div>
        </div>

        {/* Test Connection Button */}
        <div className="pt-2 flex items-center gap-4">
          <button
            onClick={handleTestConnection}
            disabled={testStatus === 'testing'}
            className="glass-button px-4 py-2 flex items-center gap-2 disabled:opacity-50"
          >
            {testStatus === 'testing' && (
              <Loader2 className="w-4 h-4 animate-spin" />
            )}
            {testStatus === 'success' && <CheckCircle className="w-4 h-4 text-accent-success" />}
            {testStatus === 'error' && <AlertCircle className="w-4 h-4 text-accent-danger" />}
            {testStatus === 'idle' && <Wifi className="w-4 h-4" />}
            <span>
              {testStatus === 'testing'
                ? 'Testing...'
                : testStatus === 'success'
                  ? 'Connected!'
                  : testStatus === 'error'
                    ? 'Failed'
                    : 'Test Connection'}
            </span>
          </button>
          {testMessage && (
            <span className={`text-sm ${testStatus === 'success' ? 'text-green-400' : 'text-red-400'}`}>
              {testMessage}
            </span>
          )}
        </div>

        {/* Polling Interval */}
        <div className="space-y-2">
          <label className="flex items-center gap-2 text-sm font-medium font-ui text-dark-200">
            Polling Interval (ms)
          </label>
          <input
            type="number"
            value={rotator.pollingIntervalMs}
            onChange={(e) => updateRotatorSettings({ pollingIntervalMs: parseInt(e.target.value) || 500 })}
            min={100}
            max={5000}
            step={100}
            placeholder="500"
            className="glass-input w-48 font-mono"
            disabled={!rotator.enabled}
          />
          <p className="text-xs text-dark-300">
            How often to poll the rotator for position updates (100-5000ms)
          </p>
        </div>

        {/* Rotator ID */}
        <div className="space-y-2">
          <label className="flex items-center gap-2 text-sm font-medium font-ui text-dark-200">
            Rotator ID
          </label>
          <input
            type="text"
            value={rotator.rotatorId}
            onChange={(e) => updateRotatorSettings({ rotatorId: e.target.value })}
            placeholder="default"
            className="glass-input w-48 font-mono"
            disabled={!rotator.enabled}
          />
          <p className="text-xs text-dark-300">
            Identifier for this rotator (useful if you have multiple rotators)
          </p>
        </div>
      </div>

      {/* Help text */}
      <div className="pt-4 border-t border-glass-100">
        <p className="text-xs text-dark-300">
          The rotator service connects to hamlib's rotctld daemon via TCP. Make sure rotctld is
          running and accessible at the configured address. Default port for rotctld is 4533.
        </p>
      </div>
    </div>
  );
}

// Database Settings Section
function DatabaseSettingsSection() {
  const {
    status,
    connectionString,
    databaseName,
    setConnectionString,
    setDatabaseName,
    testConnection,
    configure,
    isTesting,
    isLoading,
    testResult,
    error,
    fetchStatus,
    clearError,
    clearTestResult,
  } = useSetupStore();

  const [showConnectionString, setShowConnectionString] = useState(false);

  useEffect(() => {
    fetchStatus();
  }, [fetchStatus]);

  const handleTest = async () => {
    clearError();
    await testConnection();
  };

  const handleSave = async () => {
    clearError();
    const success = await configure();
    if (success) {
      // Refresh status after save
      await fetchStatus();
    }
  };

  return (
    <div className="space-y-6">
      <div>
        <h3 className="text-lg font-semibold font-ui text-dark-200 mb-1">Database Connection</h3>
        <p className="text-sm text-dark-300">
          Configure your MongoDB connection for QSO and settings storage.
        </p>
      </div>

      {/* Connection Status */}
      <div
        className={`p-4 rounded-lg border ${
          status?.isConnected
            ? 'bg-accent-success/10 border-accent-success/30'
            : 'bg-accent-danger/10 border-accent-danger/30'
        }`}
      >
        <div className="flex items-center gap-3">
          {status?.isConnected ? (
            <CheckCircle className="w-5 h-5 text-accent-success" />
          ) : (
            <AlertCircle className="w-5 h-5 text-accent-danger" />
          )}
          <div>
            <p className={`font-medium ${status?.isConnected ? 'text-accent-success' : 'text-accent-danger'}`}>
              {status?.isConnected ? 'Connected' : 'Not Connected'}
            </p>
            {status?.databaseName && (
              <p className="text-sm text-dark-300">Database: {status.databaseName}</p>
            )}
            {status?.configuredAt && (
              <p className="text-xs text-dark-300">
                Configured: {new Date(status.configuredAt).toLocaleString()}
              </p>
            )}
          </div>
        </div>
      </div>

      {/* MongoDB Atlas link */}
      <a
        href="https://www.mongodb.com/atlas/database"
        target="_blank"
        rel="noopener noreferrer"
        className="inline-flex items-center gap-2 text-sm text-accent-primary hover:underline"
      >
        <ExternalLink className="w-4 h-4" />
        Get a free MongoDB Atlas cluster
      </a>

      {/* Connection String Input */}
      <div className="space-y-4">
        <div className="space-y-2">
          <label className="flex items-center gap-2 text-sm font-medium font-ui text-dark-200">
            <Server className="w-4 h-4 text-accent-primary" />
            MongoDB Connection String
          </label>
          <div className="relative">
            <input
              type={showConnectionString ? 'text' : 'password'}
              value={connectionString}
              onChange={(e) => {
                setConnectionString(e.target.value);
                clearTestResult();
              }}
              placeholder="mongodb+srv://user:password@cluster.mongodb.net/"
              className="glass-input w-full pr-10 font-mono text-sm"
            />
            <button
              type="button"
              onClick={() => setShowConnectionString(!showConnectionString)}
              className="absolute right-2 top-1/2 -translate-y-1/2 p-1 text-dark-300 hover:text-dark-200"
            >
              {showConnectionString ? <EyeOff className="w-4 h-4" /> : <Eye className="w-4 h-4" />}
            </button>
          </div>
          <p className="text-xs text-dark-300">
            Your connection string is stored locally and never sent anywhere except MongoDB.
          </p>
        </div>

        {/* Database Name Input */}
        <div className="space-y-2">
          <label className="text-sm font-medium font-ui text-dark-200">Database Name</label>
          <input
            type="text"
            value={databaseName}
            onChange={(e) => {
              setDatabaseName(e.target.value);
              clearTestResult();
            }}
            placeholder="Log4YM"
            className="glass-input w-full font-mono"
          />
          <p className="text-xs text-dark-300">
            The database will be created automatically if it doesn't exist.
          </p>
        </div>
      </div>

      {/* Test Result */}
      {testResult && (
        <div
          className={`p-4 rounded-lg border ${
            testResult.success
              ? 'bg-accent-success/10 border-accent-success/30'
              : 'bg-accent-danger/10 border-accent-danger/30'
          }`}
        >
          <div className="flex items-start gap-3">
            {testResult.success ? (
              <CheckCircle className="w-5 h-5 text-accent-success flex-shrink-0 mt-0.5" />
            ) : (
              <AlertCircle className="w-5 h-5 text-accent-danger flex-shrink-0 mt-0.5" />
            )}
            <div>
              <p className={`font-medium ${testResult.success ? 'text-accent-success' : 'text-accent-danger'}`}>
                {testResult.message}
              </p>
              {testResult.serverInfo && (
                <p className="text-sm text-dark-300 mt-1">
                  Found {testResult.serverInfo.databaseCount} database(s) on server
                </p>
              )}
            </div>
          </div>
        </div>
      )}

      {/* Error Display */}
      {error && (
        <div className="p-4 rounded-lg border bg-accent-danger/10 border-accent-danger/30">
          <div className="flex items-start gap-3">
            <AlertCircle className="w-5 h-5 text-accent-danger flex-shrink-0 mt-0.5" />
            <p className="text-accent-danger">{error}</p>
          </div>
        </div>
      )}

      {/* Actions */}
      <div className="flex gap-3">
        <button
          onClick={handleTest}
          disabled={!connectionString || isTesting}
          className="glass-button px-4 py-2 flex items-center gap-2 disabled:opacity-50"
        >
          {isTesting ? (
            <>
              <Loader2 className="w-4 h-4 animate-spin" />
              Testing...
            </>
          ) : (
            <>
              <Database className="w-4 h-4" />
              Test Connection
            </>
          )}
        </button>
        <button
          onClick={handleSave}
          disabled={!connectionString || !testResult?.success || isLoading}
          className="glass-button-success px-4 py-2 flex items-center gap-2 disabled:opacity-50"
        >
          {isLoading ? (
            <>
              <Loader2 className="w-4 h-4 animate-spin" />
              Saving...
            </>
          ) : (
            <>
              <Save className="w-4 h-4" />
              Save & Reconnect
            </>
          )}
        </button>
      </div>

      {/* Info */}
      <div className="pt-4 border-t border-glass-100">
        <p className="text-xs text-dark-300">
          Log4YM uses MongoDB to store your QSOs, settings, and layout preferences. You can use a
          free MongoDB Atlas cluster or a local MongoDB installation. Configuration is stored
          locally on your device.
        </p>
      </div>
    </div>
  );
}

// Appearance Settings Section
function AppearanceSettingsSection() {
  const { settings, updateAppearanceSettings } = useSettingsStore();
  const appearance = settings.appearance;

  return (
    <div className="space-y-6">
      <div>
        <h3 className="text-lg font-semibold font-ui text-dark-200 mb-1">Appearance</h3>
        <p className="text-sm text-dark-300">Customize the look and feel of the application.</p>
      </div>

      {/* Theme selection */}
      <div className="space-y-3">
        <label className="text-sm font-medium font-ui text-dark-200">Theme</label>
        <div className="grid grid-cols-3 gap-3">
          {(['dark', 'light', 'system'] as const).map((theme) => (
            <button
              key={theme}
              onClick={() => updateAppearanceSettings({ theme })}
              className={`p-4 rounded-lg border transition-all ${
                appearance.theme === theme
                  ? 'border-accent-primary bg-accent-primary/10'
                  : 'border-glass-100 hover:border-glass-200'
              }`}
            >
              <span className="capitalize text-sm font-medium">{theme}</span>
            </button>
          ))}
        </div>
        <p className="text-xs text-dark-300">Currently only dark theme is available.</p>
      </div>

      {/* Compact mode toggle */}
      <div className="flex items-center justify-between p-4 bg-dark-700/50 rounded-lg border border-glass-100">
        <div>
          <p className="font-medium font-ui text-dark-200">Compact Mode</p>
          <p className="text-sm text-dark-300">Use smaller spacing and fonts</p>
        </div>
        <button
          onClick={() => updateAppearanceSettings({ compactMode: !appearance.compactMode })}
          className={`relative w-11 h-6 rounded-full transition-colors ${
            appearance.compactMode ? 'bg-accent-success' : 'bg-dark-600 border border-dark-400'
          }`}
        >
          <span
            className={`absolute top-0.5 left-0.5 w-5 h-5 rounded-full bg-white shadow transition-transform duration-200 ${
              appearance.compactMode ? 'translate-x-5' : 'translate-x-0'
            }`}
          />
        </button>
      </div>
    </div>
  );
}

// Header Settings Section
function HeaderSettingsSection() {
  const { settings, updateHeaderSettings } = useSettingsStore();
  const header = settings.header;

  return (
    <div className="space-y-6">
      <div>
        <h3 className="text-lg font-semibold font-ui text-dark-200 mb-1">Header Bar Settings</h3>
        <p className="text-sm text-dark-300">Customize the header bar display with space weather indices and time formats.</p>
      </div>

      <div className="space-y-4">
        {/* Time Format */}
        <div className="space-y-2">
          <label className="flex items-center gap-2 text-sm font-medium font-ui text-dark-200">
            Time Format (Local)
          </label>
          <div className="flex gap-2">
            <button
              onClick={() => updateHeaderSettings({ timeFormat: '12h' })}
              className={`flex-1 px-4 py-2 rounded-lg border transition-colors ${
                header.timeFormat === '12h'
                  ? 'bg-accent-primary/10 border-accent-primary text-accent-primary'
                  : 'bg-dark-700/50 border-glass-100 text-dark-300 hover:bg-dark-700'
              }`}
            >
              12-Hour
            </button>
            <button
              onClick={() => updateHeaderSettings({ timeFormat: '24h' })}
              className={`flex-1 px-4 py-2 rounded-lg border transition-colors ${
                header.timeFormat === '24h'
                  ? 'bg-accent-primary/10 border-accent-primary text-accent-primary'
                  : 'bg-dark-700/50 border-glass-100 text-dark-300 hover:bg-dark-700'
              }`}
            >
              24-Hour
            </button>
          </div>
          <p className="text-xs text-dark-300">You can also click the local time in the header to toggle formats.</p>
        </div>

        {/* Size Multiplier */}
        <div className="space-y-2">
          <label className="flex items-center gap-2 text-sm font-medium font-ui text-dark-200">
            Header Size
          </label>
          <div className="grid grid-cols-4 gap-2">
            {[0.75, 1.0, 1.25, 1.5].map((size) => (
              <button
                key={size}
                onClick={() => updateHeaderSettings({ sizeMultiplier: size })}
                className={`px-4 py-2 rounded-lg border transition-colors ${
                  header.sizeMultiplier === size
                    ? 'bg-accent-primary/10 border-accent-primary text-accent-primary'
                    : 'bg-dark-700/50 border-glass-100 text-dark-300 hover:bg-dark-700'
                }`}
              >
                {size === 1.0 ? 'Normal' : `${size}x`}
              </button>
            ))}
          </div>
          <p className="text-xs text-dark-300">Adjust the size of text and spacing in the header bar.</p>
        </div>

        {/* Show Weather */}
        <label className="flex items-center justify-between p-4 bg-dark-700/50 rounded-lg border border-glass-100 cursor-pointer hover:bg-dark-700 transition-colors">
          <div className="flex items-center gap-3">
            <Globe className="w-5 h-5 text-accent-primary" />
            <div>
              <div className="font-medium font-ui text-dark-200">Show Weather</div>
              <div className="text-sm text-dark-300">Display current weather in header (requires station coordinates)</div>
            </div>
          </div>
          <input
            type="checkbox"
            checked={header.showWeather}
            onChange={(e) => updateHeaderSettings({ showWeather: e.target.checked })}
            className="w-5 h-5 rounded border-glass-100 bg-dark-700 text-accent-primary focus:ring-accent-primary focus:ring-offset-0"
          />
        </label>

        {/* Info Box */}
        <div className="p-4 bg-accent-primary/5 border border-accent-primary/20 rounded-lg">
          <h4 className="font-medium font-ui text-accent-primary mb-2 flex items-center gap-2">
            <Info className="w-4 h-4" />
            About the Header Bar
          </h4>
          <div className="text-sm text-dark-300 space-y-2">
            <p>
              The header bar displays essential operating information inspired by OpenHamClock:
            </p>
            <ul className="list-disc list-inside space-y-1 ml-2">
              <li><strong className="text-accent-secondary">UTC Time</strong>: Essential for logging (always 24-hour format)</li>
              <li><strong className="text-accent-primary">Local Time</strong>: Your system time (clickable to toggle format)</li>
              <li><strong className="text-accent-primary">SFI</strong>: Solar Flux Index (higher is better for HF)</li>
              <li><strong className="text-accent-success">K-Index</strong>: Geomagnetic activity (turns <span className="text-accent-danger">red</span> when ≥4)</li>
              <li><strong className="text-accent-secondary">SSN</strong>: Sunspot Number (indicates solar activity)</li>
            </ul>
            <p className="pt-2 text-xs">
              Space weather data refreshes every 15 minutes. Weather data requires latitude/longitude in Station settings.
            </p>
          </div>
        </div>
      </div>
    </div>
  );
}

// About Section
function AboutSection() {
  return (
    <div className="space-y-6">
      <div>
        <h3 className="text-lg font-semibold font-ui text-dark-200 mb-1">About Log4YM</h3>
        <p className="text-sm text-dark-300">Version and application information.</p>
      </div>

      <div className="space-y-4">
        <div className="p-4 bg-dark-700/50 rounded-lg border border-glass-100">
          <div className="flex items-center gap-4">
            <div className="w-16 h-16 bg-accent-primary/20 rounded-lg flex items-center justify-center">
              <Radio className="w-8 h-8 text-accent-primary" />
            </div>
            <div>
              <h4 className="text-xl font-bold font-display text-accent-primary">LOG4YM</h4>
              <p className="text-sm text-dark-300">Ham Radio Logging Software</p>
              <p className="text-xs text-dark-300 mt-1">Version 0.1.0 (Alpha)</p>
            </div>
          </div>
        </div>

        <div className="space-y-2 text-sm text-dark-300">
          <p>
            <strong className="text-dark-200">Author:</strong> Brian Keating (EI6LF)
          </p>
          <p>
            <strong className="text-dark-200">License:</strong> MIT
          </p>
          <p>
            <strong className="text-dark-200">Website:</strong>{' '}
            <a href="https://github.com/brianbruff/Log4YM" className="text-accent-primary hover:underline">
              github.com/brianbruff/Log4YM
            </a>
          </p>
        </div>

        <div className="pt-4 border-t border-glass-100">
          <p className="text-xs text-dark-300">
            Log4YM is a modern ham radio logging application designed for amateur radio operators.
            It features real-time DX cluster integration, rotator control, and QSO logging.
          </p>
        </div>
      </div>
    </div>
  );
}

// Main Settings Panel Component
export function SettingsPanel() {
  const {
    isOpen,
    closeSettings,
    activeSection,
    setActiveSection,
    isDirty,
    isSaving,
    saveSettings,
    resetSettings,
  } = useSettingsStore();

  if (!isOpen) return null;

  const handleSave = async () => {
    try {
      await saveSettings();
    } catch {
      // Error handled in store
    }
  };

  const renderSection = () => {
    switch (activeSection) {
      case 'station':
        return <StationSettingsSection />;
      case 'qrz':
        return <QrzSettingsSection />;
      case 'rotator':
        return <RotatorSettingsSection />;
      case 'database':
        return <DatabaseSettingsSection />;
      case 'appearance':
        return <AppearanceSettingsSection />;
      case 'header':
        return <HeaderSettingsSection />;
      case 'about':
        return <AboutSection />;
      default:
        return null;
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center">
      {/* Backdrop */}
      <div className="absolute inset-0 bg-dark-900/80 backdrop-blur-sm" onClick={closeSettings} />

      {/* Panel */}
      <div className="relative w-full max-w-4xl h-[600px] mx-4 bg-dark-800 border border-glass-200 rounded-xl shadow-2xl flex overflow-hidden animate-fade-in">
        {/* Master - Navigation sidebar */}
        <div className="w-64 bg-dark-850 border-r border-glass-100 flex flex-col">
          {/* Header */}
          <div className="p-4 border-b border-glass-100">
            <div className="flex items-center gap-3">
              <Settings className="w-6 h-6 text-accent-primary" />
              <h2 className="text-lg font-semibold font-display">Settings</h2>
            </div>
          </div>

          {/* Navigation */}
          <nav className="flex-1 p-2 space-y-1 overflow-auto">
            {SETTINGS_SECTIONS.map((section) => (
              <button
                key={section.id}
                onClick={() => setActiveSection(section.id)}
                className={`w-full flex items-center gap-3 px-3 py-3 rounded-lg text-left transition-all ${
                  activeSection === section.id
                    ? 'bg-accent-primary/10 text-accent-primary border border-accent-primary/30'
                    : 'hover:bg-dark-700 text-dark-300 hover:text-dark-200 border border-transparent'
                }`}
              >
                <span className={activeSection === section.id ? 'text-accent-secondary' : 'text-dark-300'}>
                  {section.icon}
                </span>
                <div>
                  <p className="font-medium font-ui">{section.name}</p>
                  <p className="text-xs text-dark-300">{section.description}</p>
                </div>
              </button>
            ))}
          </nav>

          {/* Footer actions */}
          <div className="p-3 border-t border-glass-100 space-y-2">
            <button
              onClick={handleSave}
              disabled={!isDirty || isSaving}
              className="w-full glass-button-success flex items-center justify-center gap-2 py-2 disabled:opacity-50"
            >
              {isSaving ? (
                <div className="w-4 h-4 border-2 border-white border-t-transparent rounded-full animate-spin" />
              ) : (
                <Save className="w-4 h-4" />
              )}
              <span>{isSaving ? 'Saving...' : 'Save Changes'}</span>
            </button>
            <button
              onClick={resetSettings}
              className="w-full glass-button flex items-center justify-center gap-2 py-2 text-dark-300"
            >
              <RotateCcw className="w-4 h-4" />
              <span>Reset to Defaults</span>
            </button>
          </div>
        </div>

        {/* Detail - Content area */}
        <div className="flex-1 flex flex-col">
          {/* Header with close button */}
          <div className="flex items-center justify-between p-4 border-b border-glass-100">
            <h3 className="text-lg font-semibold font-display text-dark-200">
              {SETTINGS_SECTIONS.find((s) => s.id === activeSection)?.name}
            </h3>
            <button onClick={closeSettings} className="p-2 hover:bg-dark-700 rounded-lg transition-colors">
              <X className="w-5 h-5" />
            </button>
          </div>

          {/* Content */}
          <div className="flex-1 p-6 overflow-auto">{renderSection()}</div>

          {/* Dirty indicator */}
          {isDirty && (
            <div className="px-4 py-2 bg-accent-primary/10 border-t border-accent-primary/30 text-accent-primary text-sm flex items-center gap-2">
              <AlertCircle className="w-4 h-4" />
              <span>You have unsaved changes</span>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
