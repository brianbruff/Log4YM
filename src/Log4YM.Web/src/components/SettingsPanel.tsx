import { useEffect, useState, useCallback, useRef } from 'react';
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
  FileText,
  Upload,
  Download,
  XCircle,
  AlertTriangle,
  Loader2,
  FileDown,
  Database,
} from 'lucide-react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useSettingsStore, SettingsSection } from '../store/settingsStore';
import { useAppStore } from '../store/appStore';
import { api, AdifImportResponse } from '../api/client';

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
    id: 'logbook',
    name: 'Logbook',
    icon: <Database className="w-5 h-5" />,
    description: 'Import and export ADIF files',
  },
  {
    id: 'appearance',
    name: 'Appearance',
    icon: <Palette className="w-5 h-5" />,
    description: 'Theme and display options',
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
  const { setStationInfo } = useAppStore();
  const station = settings.station;

  // Sync station info to app store when callsign/grid changes
  useEffect(() => {
    if (station.callsign || station.gridSquare) {
      setStationInfo(station.callsign, station.gridSquare);
    }
  }, [station.callsign, station.gridSquare, setStationInfo]);

  return (
    <div className="space-y-6">
      <div>
        <h3 className="text-lg font-semibold text-gray-100 mb-1">Station Information</h3>
        <p className="text-sm text-gray-500">Configure your station callsign and location details.</p>
      </div>

      <div className="grid grid-cols-2 gap-4">
        {/* Callsign */}
        <div className="space-y-2">
          <label className="flex items-center gap-2 text-sm font-medium text-gray-300">
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
          <label className="flex items-center gap-2 text-sm font-medium text-gray-300">
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
          <label className="flex items-center gap-2 text-sm font-medium text-gray-300">
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
          <label className="text-sm font-medium text-gray-300">City</label>
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
          <label className="text-sm font-medium text-gray-300">Country</label>
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
        <h4 className="text-sm font-medium text-gray-300 mb-3">Coordinates (Optional)</h4>
        <div className="grid grid-cols-2 gap-4">
          <div className="space-y-2">
            <label className="text-sm text-gray-400">Latitude</label>
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
            <label className="text-sm text-gray-400">Longitude</label>
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
  const { settings, updateQrzSettings, setQrzPassword, getQrzPassword, setQrzApiKey, getQrzApiKey } = useSettingsStore();
  const [showPassword, setShowPassword] = useState(false);
  const [showApiKey, setShowApiKey] = useState(false);
  const [testStatus, setTestStatus] = useState<'idle' | 'testing' | 'success' | 'error'>('idle');
  const [testMessage, setTestMessage] = useState('');
  const [hasXmlSubscription, setHasXmlSubscription] = useState<boolean | null>(null);

  const qrz = settings.qrz;
  const password = getQrzPassword();
  const apiKey = getQrzApiKey();

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
        <h3 className="text-lg font-semibold text-gray-100 mb-1">QRZ.com Integration</h3>
        <p className="text-sm text-gray-500">
          Configure your QRZ.com credentials for callsign lookups and log uploads.
        </p>
      </div>

      {/* Enable toggle */}
      <div className="flex items-center justify-between p-4 bg-dark-700/50 rounded-lg border border-glass-100">
        <div>
          <p className="font-medium text-gray-200">Enable QRZ Lookups</p>
          <p className="text-sm text-gray-500">Use QRZ.com for callsign information</p>
        </div>
        <button
          onClick={() => updateQrzSettings({ enabled: !qrz.enabled })}
          className={`relative w-12 h-6 rounded-full transition-colors ${
            qrz.enabled ? 'bg-accent-success' : 'bg-dark-500'
          }`}
        >
          <span
            className={`absolute top-1 w-4 h-4 rounded-full bg-white transition-transform ${
              qrz.enabled ? 'translate-x-7' : 'translate-x-1'
            }`}
          />
        </button>
      </div>

      {/* Subscription Status */}
      {hasXmlSubscription !== null && (
        <div className={`flex items-center gap-2 p-3 rounded-lg border ${
          hasXmlSubscription
            ? 'bg-green-500/10 border-green-500/30 text-green-400'
            : 'bg-yellow-500/10 border-yellow-500/30 text-yellow-400'
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
          <label className="flex items-center gap-2 text-sm font-medium text-gray-300">
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
          <label className="flex items-center gap-2 text-sm font-medium text-gray-300">
            <Key className="w-4 h-4 text-accent-primary" />
            QRZ Password
          </label>
          <div className="relative">
            <input
              type={showPassword ? 'text' : 'password'}
              value={password}
              onChange={(e) => setQrzPassword(e.target.value)}
              placeholder="Your QRZ.com password"
              className="glass-input w-full pr-10"
              disabled={!qrz.enabled}
            />
            <button
              type="button"
              onClick={() => setShowPassword(!showPassword)}
              className="absolute right-2 top-1/2 -translate-y-1/2 p-1 text-gray-500 hover:text-gray-300"
            >
              {showPassword ? <EyeOff className="w-4 h-4" /> : <Eye className="w-4 h-4" />}
            </button>
          </div>
          <p className="text-xs text-gray-600">
            Required for callsign lookups (requires XML subscription on QRZ.com).
          </p>
        </div>

        {/* API Key for Logbook */}
        <div className="pt-4 border-t border-glass-100">
          <h4 className="text-sm font-medium text-gray-300 mb-3">QRZ Logbook Integration</h4>
          <div className="space-y-2">
            <label className="flex items-center gap-2 text-sm font-medium text-gray-300">
              <Key className="w-4 h-4 text-accent-info" />
              Logbook API Key
            </label>
            <div className="relative">
              <input
                type={showApiKey ? 'text' : 'password'}
                value={apiKey}
                onChange={(e) => setQrzApiKey(e.target.value)}
                placeholder="Your QRZ Logbook API Key"
                className="glass-input w-full pr-10 font-mono text-sm"
                disabled={!qrz.enabled}
              />
              <button
                type="button"
                onClick={() => setShowApiKey(!showApiKey)}
                className="absolute right-2 top-1/2 -translate-y-1/2 p-1 text-gray-500 hover:text-gray-300"
              >
                {showApiKey ? <EyeOff className="w-4 h-4" /> : <Eye className="w-4 h-4" />}
              </button>
            </div>
            <p className="text-xs text-gray-600">
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
            <span className={`text-sm ${testStatus === 'success' ? 'text-green-400' : 'text-red-400'}`}>
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

  return (
    <div className="space-y-6">
      <div>
        <h3 className="text-lg font-semibold text-gray-100 mb-1">Rotator Control</h3>
        <p className="text-sm text-gray-500">
          Configure connection to hamlib rotctld for antenna rotator control.
        </p>
      </div>

      {/* Enable toggle */}
      <div className="flex items-center justify-between p-4 bg-dark-700/50 rounded-lg border border-glass-100">
        <div className="flex items-center gap-3">
          {rotator.enabled ? (
            <Wifi className="w-5 h-5 text-accent-success" />
          ) : (
            <WifiOff className="w-5 h-5 text-gray-500" />
          )}
          <div>
            <p className="font-medium text-gray-200">Enable Rotator Control</p>
            <p className="text-sm text-gray-500">Connect to rotctld TCP server</p>
          </div>
        </div>
        <button
          onClick={() => updateRotatorSettings({ enabled: !rotator.enabled })}
          className={`relative w-12 h-6 rounded-full transition-colors ${
            rotator.enabled ? 'bg-accent-success' : 'bg-dark-500'
          }`}
        >
          <span
            className={`absolute top-1 w-4 h-4 rounded-full bg-white transition-transform ${
              rotator.enabled ? 'translate-x-7' : 'translate-x-1'
            }`}
          />
        </button>
      </div>

      {/* Connection settings */}
      <div className={`space-y-4 ${!rotator.enabled ? 'opacity-50 pointer-events-none' : ''}`}>
        <div className="grid grid-cols-2 gap-4">
          {/* IP Address */}
          <div className="space-y-2">
            <label className="flex items-center gap-2 text-sm font-medium text-gray-300">
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
          </div>

          {/* Port */}
          <div className="space-y-2">
            <label className="flex items-center gap-2 text-sm font-medium text-gray-300">
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
          </div>
        </div>

        {/* Polling Interval */}
        <div className="space-y-2">
          <label className="flex items-center gap-2 text-sm font-medium text-gray-300">
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
          <p className="text-xs text-gray-600">
            How often to poll the rotator for position updates (100-5000ms)
          </p>
        </div>

        {/* Rotator ID */}
        <div className="space-y-2">
          <label className="flex items-center gap-2 text-sm font-medium text-gray-300">
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
          <p className="text-xs text-gray-600">
            Identifier for this rotator (useful if you have multiple rotators)
          </p>
        </div>
      </div>

      {/* Help text */}
      <div className="pt-4 border-t border-glass-100">
        <p className="text-xs text-gray-600">
          The rotator service connects to hamlib's rotctld daemon via TCP. Make sure rotctld is
          running and accessible at the configured address. Default port for rotctld is 4533.
        </p>
      </div>
    </div>
  );
}

// Logbook Settings Section
function LogbookSettingsSection() {
  const queryClient = useQueryClient();
  const fileInputRef = useRef<HTMLInputElement>(null);
  const [importResult, setImportResult] = useState<AdifImportResponse | null>(null);
  const [skipDuplicates, setSkipDuplicates] = useState(true);

  // Import mutation
  const importMutation = useMutation({
    mutationFn: ({ file, skipDuplicates }: { file: File; skipDuplicates: boolean }) =>
      api.importAdif(file, skipDuplicates),
    onSuccess: (data) => {
      setImportResult(data);
      queryClient.invalidateQueries({ queryKey: ['qsos'] });
      queryClient.invalidateQueries({ queryKey: ['statistics'] });
    },
  });

  // Export mutation
  const exportMutation = useMutation({
    mutationFn: () => api.exportAdif(),
    onSuccess: (blob) => {
      downloadBlob(blob, `log4ym_export_${new Date().toISOString().slice(0, 10)}.adi`);
    },
  });

  const handleFileSelect = useCallback((e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (file) {
      importMutation.mutate({ file, skipDuplicates });
    }
    // Reset input so same file can be selected again
    if (fileInputRef.current) {
      fileInputRef.current.value = '';
    }
  }, [skipDuplicates, importMutation]);

  const handleDrop = useCallback((e: React.DragEvent) => {
    e.preventDefault();
    const file = e.dataTransfer.files?.[0];
    if (file && (file.name.endsWith('.adi') || file.name.endsWith('.adif') || file.name.endsWith('.xml'))) {
      importMutation.mutate({ file, skipDuplicates });
    }
  }, [skipDuplicates, importMutation]);

  const handleDragOver = useCallback((e: React.DragEvent) => {
    e.preventDefault();
  }, []);

  const handleExport = useCallback(() => {
    exportMutation.mutate();
  }, [exportMutation]);

  const downloadBlob = (blob: Blob, filename: string) => {
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = filename;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
  };

  return (
    <div className="space-y-6">
      <div>
        <h3 className="text-lg font-semibold text-gray-100 mb-1">Logbook Management</h3>
        <p className="text-sm text-gray-500">
          Import and export your logbook in ADIF format.
        </p>
      </div>

      {/* Import Section */}
      <div className="p-4 bg-dark-700/50 rounded-lg border border-glass-100">
        <h4 className="font-semibold text-gray-200 flex items-center gap-2 mb-4">
          <Upload className="w-4 h-4 text-accent-primary" />
          Import ADIF
        </h4>

        {/* Drop Zone */}
        <div
          onDrop={handleDrop}
          onDragOver={handleDragOver}
          onClick={() => fileInputRef.current?.click()}
          className={`
            border-2 border-dashed rounded-lg p-6 text-center cursor-pointer transition-colors
            ${importMutation.isPending
              ? 'border-accent-primary/50 bg-accent-primary/10'
              : 'border-glass-200 hover:border-accent-primary/50 hover:bg-dark-600/30'
            }
          `}
        >
          {importMutation.isPending ? (
            <div className="flex flex-col items-center gap-2">
              <Loader2 className="w-8 h-8 animate-spin text-accent-primary" />
              <p className="text-gray-400">Importing...</p>
            </div>
          ) : (
            <>
              <FileText className="w-10 h-10 mx-auto mb-2 text-gray-500" />
              <p className="text-gray-300 mb-1">Drop ADIF file here or click to browse</p>
              <p className="text-xs text-gray-500">Supports .adi, .adif, and .xml files</p>
            </>
          )}
        </div>

        <input
          ref={fileInputRef}
          type="file"
          accept=".adi,.adif,.xml"
          onChange={handleFileSelect}
          className="hidden"
        />

        {/* Options */}
        <div className="flex items-center gap-2 mt-4">
          <input
            type="checkbox"
            id="skipDuplicates"
            checked={skipDuplicates}
            onChange={e => setSkipDuplicates(e.target.checked)}
            className="w-4 h-4 rounded border-gray-600 text-accent-primary focus:ring-accent-primary"
          />
          <label htmlFor="skipDuplicates" className="text-sm text-gray-400">
            Skip duplicate QSOs (same callsign, date, time, band, mode)
          </label>
        </div>
      </div>

      {/* Import Results */}
      {importResult && (
        <div className="p-4 bg-dark-700/50 rounded-lg border border-glass-100">
          <h4 className="font-semibold text-gray-200 flex items-center gap-2 mb-3">
            {importResult.errorCount === 0 && importResult.importedCount > 0 ? (
              <CheckCircle className="w-4 h-4 text-green-400" />
            ) : importResult.errorCount > 0 ? (
              <AlertTriangle className="w-4 h-4 text-yellow-400" />
            ) : (
              <XCircle className="w-4 h-4 text-red-400" />
            )}
            Import Results
          </h4>

          <div className="grid grid-cols-4 gap-4 mb-4">
            <div className="text-center">
              <p className="text-2xl font-bold text-accent-primary">{importResult.totalRecords}</p>
              <p className="text-xs text-gray-500">Total Records</p>
            </div>
            <div className="text-center">
              <p className="text-2xl font-bold text-green-400">{importResult.importedCount}</p>
              <p className="text-xs text-gray-500">Imported</p>
            </div>
            <div className="text-center">
              <p className="text-2xl font-bold text-yellow-400">{importResult.skippedDuplicates}</p>
              <p className="text-xs text-gray-500">Duplicates</p>
            </div>
            <div className="text-center">
              <p className="text-2xl font-bold text-red-400">{importResult.errorCount}</p>
              <p className="text-xs text-gray-500">Errors</p>
            </div>
          </div>

          {importResult.errors.length > 0 && (
            <div className="max-h-24 overflow-auto bg-dark-800/50 rounded p-2 mb-3">
              {importResult.errors.map((error, i) => (
                <p key={i} className="text-sm text-red-400 flex items-start gap-2 py-1">
                  <XCircle className="w-4 h-4 flex-shrink-0 mt-0.5" />
                  {error}
                </p>
              ))}
            </div>
          )}

          <button
            onClick={() => setImportResult(null)}
            className="text-sm text-gray-400 hover:text-gray-300"
          >
            Dismiss
          </button>
        </div>
      )}

      {/* Export Section */}
      <div className="p-4 bg-dark-700/50 rounded-lg border border-glass-100">
        <h4 className="font-semibold text-gray-200 flex items-center gap-2 mb-4">
          <Download className="w-4 h-4 text-accent-success" />
          Export ADIF
        </h4>

        <p className="text-sm text-gray-400 mb-4">
          Export all QSOs from your logbook to an ADIF file. The file will be downloaded to your browser.
        </p>

        <button
          onClick={handleExport}
          disabled={exportMutation.isPending}
          className="w-full glass-button py-3 bg-accent-success/20 hover:bg-accent-success/30 disabled:opacity-50 flex items-center justify-center gap-2"
        >
          {exportMutation.isPending ? (
            <>
              <Loader2 className="w-5 h-5 animate-spin" />
              Exporting...
            </>
          ) : (
            <>
              <FileDown className="w-5 h-5" />
              Export All QSOs to ADIF
            </>
          )}
        </button>
      </div>

      {/* Info */}
      <div className="pt-4 border-t border-glass-100">
        <p className="text-xs text-gray-600">
          ADIF (Amateur Data Interchange Format) is the standard format for exchanging amateur radio log data.
          Duplicate detection uses callsign, date, time, band, and mode to identify matching QSOs.
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
        <h3 className="text-lg font-semibold text-gray-100 mb-1">Appearance</h3>
        <p className="text-sm text-gray-500">Customize the look and feel of the application.</p>
      </div>

      {/* Theme selection */}
      <div className="space-y-3">
        <label className="text-sm font-medium text-gray-300">Theme</label>
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
        <p className="text-xs text-gray-600">Currently only dark theme is available.</p>
      </div>

      {/* Compact mode toggle */}
      <div className="flex items-center justify-between p-4 bg-dark-700/50 rounded-lg border border-glass-100">
        <div>
          <p className="font-medium text-gray-200">Compact Mode</p>
          <p className="text-sm text-gray-500">Use smaller spacing and fonts</p>
        </div>
        <button
          onClick={() => updateAppearanceSettings({ compactMode: !appearance.compactMode })}
          className={`relative w-12 h-6 rounded-full transition-colors ${
            appearance.compactMode ? 'bg-accent-success' : 'bg-dark-500'
          }`}
        >
          <span
            className={`absolute top-1 w-4 h-4 rounded-full bg-white transition-transform ${
              appearance.compactMode ? 'translate-x-7' : 'translate-x-1'
            }`}
          />
        </button>
      </div>
    </div>
  );
}

// About Section
function AboutSection() {
  return (
    <div className="space-y-6">
      <div>
        <h3 className="text-lg font-semibold text-gray-100 mb-1">About Log4YM</h3>
        <p className="text-sm text-gray-500">Version and application information.</p>
      </div>

      <div className="space-y-4">
        <div className="p-4 bg-dark-700/50 rounded-lg border border-glass-100">
          <div className="flex items-center gap-4">
            <div className="w-16 h-16 bg-orange-500/20 rounded-lg flex items-center justify-center">
              <Radio className="w-8 h-8 text-orange-500" />
            </div>
            <div>
              <h4 className="text-xl font-bold text-orange-500">LOG4YM</h4>
              <p className="text-sm text-gray-400">Ham Radio Logging Software</p>
              <p className="text-xs text-gray-600 mt-1">Version 0.1.0 (Alpha)</p>
            </div>
          </div>
        </div>

        <div className="space-y-2 text-sm text-gray-400">
          <p>
            <strong className="text-gray-300">Author:</strong> Brian Keating (EI6LF)
          </p>
          <p>
            <strong className="text-gray-300">License:</strong> MIT
          </p>
          <p>
            <strong className="text-gray-300">Website:</strong>{' '}
            <a href="https://github.com/brianbruff/Log4YM" className="text-accent-primary hover:underline">
              github.com/brianbruff/Log4YM
            </a>
          </p>
        </div>

        <div className="pt-4 border-t border-glass-100">
          <p className="text-xs text-gray-600">
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
      case 'logbook':
        return <LogbookSettingsSection />;
      case 'appearance':
        return <AppearanceSettingsSection />;
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
              <h2 className="text-lg font-semibold">Settings</h2>
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
                    : 'hover:bg-dark-700 text-gray-400 hover:text-gray-200 border border-transparent'
                }`}
              >
                <span className={activeSection === section.id ? 'text-accent-primary' : 'text-gray-500'}>
                  {section.icon}
                </span>
                <div>
                  <p className="font-medium">{section.name}</p>
                  <p className="text-xs text-gray-600">{section.description}</p>
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
              className="w-full glass-button flex items-center justify-center gap-2 py-2 text-gray-400"
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
            <h3 className="text-lg font-semibold text-gray-100">
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
            <div className="px-4 py-2 bg-amber-500/10 border-t border-amber-500/30 text-amber-400 text-sm flex items-center gap-2">
              <AlertCircle className="w-4 h-4" />
              <span>You have unsaved changes</span>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
