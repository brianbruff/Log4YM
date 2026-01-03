import { useState, useEffect, useMemo } from "react";
import { Radio, Wifi, WifiOff, Power, PowerOff, Plus, Settings, ChevronDown, ChevronUp } from "lucide-react";
import { useAppStore } from "../store/appStore";
import { useSettingsStore } from "../store/settingsStore";
import { useSignalR } from "../hooks/useSignalR";
import { GlassPanel } from "../components/GlassPanel";
import { signalRService } from "../api/signalr";
import type {
  RadioConnectionState,
  HamlibRigModelInfo,
  HamlibRigCapabilities,
  HamlibRigConfigDto,
  HamlibDataBits,
  HamlibStopBits,
  HamlibFlowControl,
  HamlibParity,
  HamlibPttType,
} from "../api/signalr";
import { HAMLIB_BAUD_RATES } from "../api/signalr";

// Default Hamlib config
const defaultHamlibConfig: HamlibRigConfigDto = {
  modelId: 0,
  modelName: "",
  connectionType: "Serial",
  baudRate: 9600,
  dataBits: 8,
  stopBits: 1,
  flowControl: "None",
  parity: "None",
  networkPort: 4532,
  pttType: "Rig",
  getFrequency: true,
  getMode: true,
  getVfo: true,
  getPtt: true,
  getPower: false,
  getRit: false,
  getXit: false,
  getKeySpeed: false,
  pollIntervalMs: 250,
};

export function RigPlugin() {
  const {
    discoveredRadios,
    radioConnectionStates,
    radioStates,
    selectedRadioId,
    setSelectedRadio,
  } = useAppStore();

  const {
    connectRadio,
    disconnectRadio,
    getHamlibRigList,
    getHamlibRigCaps,
    getHamlibSerialPorts,
    getHamlibConfig,
    connectHamlibRig,
    disconnectHamlibRig,
    connectTci,
    disconnectTci,
  } = useSignalR();

  // TCI settings from store (persisted to database)
  const { settings, updateTciSettings, saveSettings } = useSettingsStore();
  const tciSettings = settings.radio.tci;

  // TCI form state
  const [showTciForm, setShowTciForm] = useState(false);
  const [isConnectingTci, setIsConnectingTci] = useState(false);

  // Hamlib form state
  const [showHamlibForm, setShowHamlibForm] = useState(false);
  const [isConnectingHamlib, setIsConnectingHamlib] = useState(false);
  const [hamlibRigs, setHamlibRigs] = useState<HamlibRigModelInfo[]>([]);
  const [hamlibCaps, setHamlibCaps] = useState<HamlibRigCapabilities | null>(null);
  const [serialPorts, setSerialPorts] = useState<string[]>([]);
  const [hamlibConfig, setHamlibConfig] = useState<HamlibRigConfigDto>(defaultHamlibConfig);
  const [rigSearch, setRigSearch] = useState("");
  const [showRigDropdown, setShowRigDropdown] = useState(false);
  const [showAdvanced, setShowAdvanced] = useState(false);

  // Set up Hamlib event handlers
  useEffect(() => {
    signalRService.setHandlers({
      onHamlibRigList: (evt) => {
        console.log('Hamlib rig list received:', evt.rigs.length, 'rigs');
        setHamlibRigs(evt.rigs);
      },
      onHamlibRigCaps: (evt) => {
        console.log('Hamlib rig caps received:', evt.modelId);
        setHamlibCaps(evt.capabilities);
      },
      onHamlibSerialPorts: (evt) => {
        console.log('Serial ports received:', evt.ports);
        setSerialPorts(evt.ports);
      },
      onHamlibConfigLoaded: (evt) => {
        console.log('Hamlib config loaded:', evt.config?.modelName);
        if (evt.config) {
          setHamlibConfig(evt.config);
          // Also load caps for this model
          getHamlibRigCaps(evt.config.modelId);
        }
      },
    });
  }, [getHamlibRigCaps]);

  // Load Hamlib data when form opens
  useEffect(() => {
    if (showHamlibForm && hamlibRigs.length === 0) {
      getHamlibRigList();
      getHamlibSerialPorts();
      getHamlibConfig();
    }
  }, [showHamlibForm, hamlibRigs.length, getHamlibRigList, getHamlibSerialPorts, getHamlibConfig]);

  // Load capabilities when rig model changes
  useEffect(() => {
    if (hamlibConfig.modelId > 0) {
      getHamlibRigCaps(hamlibConfig.modelId);
    }
  }, [hamlibConfig.modelId, getHamlibRigCaps]);

  // Auto-select connection type based on capabilities
  useEffect(() => {
    if (hamlibCaps) {
      // If current selection is not supported, switch to supported type
      if (hamlibConfig.connectionType === "Serial" && !hamlibCaps.supportsSerial && hamlibCaps.supportsNetwork) {
        updateHamlibConfig({ connectionType: "Network" });
      } else if (hamlibConfig.connectionType === "Network" && !hamlibCaps.supportsNetwork && hamlibCaps.supportsSerial) {
        updateHamlibConfig({ connectionType: "Serial" });
      }
    }
  }, [hamlibCaps, hamlibConfig.connectionType]);

  // Filter rigs by search term
  const filteredRigs = useMemo(() => {
    if (!rigSearch) return hamlibRigs.slice(0, 50); // Show first 50 by default
    const search = rigSearch.toLowerCase();
    return hamlibRigs.filter(rig =>
      rig.displayName.toLowerCase().includes(search) ||
      rig.manufacturer.toLowerCase().includes(search) ||
      rig.model.toLowerCase().includes(search)
    ).slice(0, 50);
  }, [hamlibRigs, rigSearch]);

  // Convert Map to array for rendering
  const radios = Array.from(discoveredRadios.values());
  const selectedRadio = selectedRadioId
    ? discoveredRadios.get(selectedRadioId)
    : null;
  const selectedConnectionState = selectedRadioId
    ? radioConnectionStates.get(selectedRadioId)
    : null;
  const selectedRadioState = selectedRadioId
    ? radioStates.get(selectedRadioId)
    : null;

  // Reset local connecting state when we receive connection state or radio data from SignalR
  useEffect(() => {
    if (selectedRadioState) {
      setIsConnectingTci(false);
      setIsConnectingHamlib(false);
    } else if (selectedConnectionState && selectedConnectionState !== "Connecting") {
      setIsConnectingTci(false);
      setIsConnectingHamlib(false);
    }
  }, [selectedConnectionState, selectedRadioState]);

  const handleConnect = async (radioId: string) => {
    setSelectedRadio(radioId);
    await connectRadio(radioId);
  };

  const handleDisconnect = async () => {
    if (selectedRadioId) {
      const radio = discoveredRadios.get(selectedRadioId);
      if (radio?.type === "Hamlib" || selectedRadioId.startsWith("hamlib-")) {
        await disconnectHamlibRig();
      } else if (radio?.type === "Tci" || selectedRadioId.startsWith("tci-")) {
        await disconnectTci(selectedRadioId);
      } else {
        await disconnectRadio(selectedRadioId);
      }
      setSelectedRadio(null);
    }
  };

  const handleConnectHamlib = async () => {
    if (!hamlibConfig.modelId) return;

    // Validate based on connection type
    if (hamlibConfig.connectionType === "Serial" && !hamlibConfig.serialPort) {
      return;
    }
    if (hamlibConfig.connectionType === "Network" && !hamlibConfig.hostname) {
      return;
    }

    const radioId = `hamlib-${hamlibConfig.modelId}`;
    setSelectedRadio(radioId);
    setIsConnectingHamlib(true);
    setShowHamlibForm(false);

    try {
      await connectHamlibRig(hamlibConfig);
    } catch (error) {
      console.error('Failed to connect Hamlib rig:', error);
      setSelectedRadio(null);
      setIsConnectingHamlib(false);
    }
  };

  const handleConnectTci = async () => {
    const port = tciSettings.port;
    const host = tciSettings.host;
    if (!host || !port) return;

    const radioId = `tci-${host}:${port}`;
    setSelectedRadio(radioId);
    setIsConnectingTci(true);
    setShowTciForm(false);

    try {
      // Save settings to database before connecting
      await saveSettings();
      await connectTci(host, port, tciSettings.name || undefined);
    } catch (error) {
      setSelectedRadio(null);
      setIsConnectingTci(false);
    }
  };

  const updateHamlibConfig = (updates: Partial<HamlibRigConfigDto>) => {
    setHamlibConfig(prev => ({ ...prev, ...updates }));
  };

  const handleRigSelect = (rig: HamlibRigModelInfo) => {
    updateHamlibConfig({
      modelId: rig.modelId,
      modelName: rig.displayName,
    });
    setRigSearch(rig.displayName);
    setShowRigDropdown(false);
  };

  const formatFrequency = (hz: number): string => {
    const mhz = hz / 1_000_000;
    return mhz.toFixed(6);
  };

  const getConnectionStateColor = (state?: RadioConnectionState): string => {
    switch (state) {
      case "Connected":
      case "Monitoring":
        return "text-green-400";
      case "Connecting":
      case "Discovering":
        return "text-yellow-400";
      case "Error":
        return "text-red-400";
      default:
        return "text-gray-500";
    }
  };

  const getConnectionStateText = (state?: RadioConnectionState): string => {
    return state || "Disconnected";
  };

  const isConnecting = selectedConnectionState === "Connecting";
  const isConnected = (selectedConnectionState && ["Connected", "Monitoring"].includes(selectedConnectionState))
    || !!selectedRadioState;
  const isLocallyConnecting = isConnectingTci || isConnectingHamlib;
  const effectiveConnectionState: RadioConnectionState | undefined =
    selectedConnectionState ?? (selectedRadioState ? "Connected" : undefined);

  const getRadioInfoFromId = (radioId: string) => {
    if (radioId.startsWith("tci-")) {
      const hostPort = radioId.substring(4);
      return { model: "TCI Radio", ipAddress: hostPort, type: "Tci" as const };
    }
    if (radioId.startsWith("hamlib-")) {
      const modelId = radioId.substring(7);
      const rig = hamlibRigs.find(r => r.modelId.toString() === modelId);
      return { model: rig?.displayName || "Hamlib Radio", ipAddress: hamlibConfig.connectionType === "Network" ? `${hamlibConfig.hostname}:${hamlibConfig.networkPort}` : hamlibConfig.serialPort || "", type: "Hamlib" as const };
    }
    return null;
  };

  const radioInfo = selectedRadio || (selectedRadioId ? getRadioInfoFromId(selectedRadioId) : null);

  // Show connected view
  if (selectedRadioId && radioInfo && (isConnecting || isConnected || isLocallyConnecting)) {
    return (
      <GlassPanel
        title="Rig"
        icon={<Radio className="w-5 h-5" />}
        actions={
          <div className="flex items-center gap-2">
            <span
              className={`flex items-center gap-1.5 text-xs ${getConnectionStateColor(
                effectiveConnectionState
              )}`}
            >
              <Wifi className="w-3.5 h-3.5" />
              {getConnectionStateText(effectiveConnectionState)}
            </span>
          </div>
        }
      >
        <div className="p-4 space-y-4">
          {/* Radio Info */}
          <div className="flex items-center justify-between">
            <div className="text-sm text-gray-400">
              <span className="font-medium text-gray-200">
                {radioInfo.model}
              </span>
              <span className="mx-2">|</span>
              <span className="font-mono text-xs">
                {radioInfo.ipAddress}
              </span>
            </div>
            {(isConnecting || isLocallyConnecting) && !isConnected ? (
              <div className="px-3 py-1.5 text-xs font-medium flex items-center gap-1.5 bg-yellow-500/20 text-yellow-400 rounded-lg">
                <Wifi className="w-3.5 h-3.5 animate-pulse" />
                Connecting...
              </div>
            ) : (
              <button
                onClick={handleDisconnect}
                className="px-3 py-1.5 text-xs font-medium flex items-center gap-1.5 bg-red-500/20 text-red-400 rounded-lg hover:bg-red-500/30 transition-all"
              >
                <PowerOff className="w-3.5 h-3.5" />
                Disconnect
              </button>
            )}
          </div>

          {/* Frequency Display */}
          <div className="bg-dark-700/50 rounded-lg p-4 border border-glass-100">
            <div className="text-xs text-gray-500 uppercase tracking-wider mb-1">
              Frequency
            </div>
            <div className="text-3xl font-bold text-accent-primary font-mono">
              {selectedRadioState
                ? formatFrequency(selectedRadioState.frequencyHz)
                : (isConnecting || isLocallyConnecting) && !isConnected
                ? <span className="text-gray-500 text-lg animate-pulse">Waiting for data...</span>
                : "---"}
              {selectedRadioState && (
                <span className="text-lg font-normal text-gray-500 ml-2">
                  MHz
                </span>
              )}
            </div>
          </div>

          {/* Mode and Band */}
          <div className="grid grid-cols-2 gap-3">
            <div className="bg-dark-700/50 rounded-lg p-3 border border-glass-100">
              <div className="text-xs text-gray-500 uppercase tracking-wider mb-1">
                Mode
              </div>
              <div className="text-xl font-bold text-accent-secondary">
                {selectedRadioState?.mode || "---"}
              </div>
            </div>
            <div className="bg-dark-700/50 rounded-lg p-3 border border-glass-100">
              <div className="text-xs text-gray-500 uppercase tracking-wider mb-1">
                Band
              </div>
              <div className="text-xl font-bold text-accent-primary">
                {selectedRadioState?.band || "---"}
              </div>
            </div>
          </div>

          {/* TX/RX Status */}
          <div className="flex items-center gap-3">
            <TxRxIndicator
              isTransmitting={selectedRadioState?.isTransmitting ?? false}
            />
            {selectedRadioState?.sliceOrInstance && (
              <span className="text-sm text-gray-400">
                Slice:{" "}
                <span className="text-gray-200">
                  {selectedRadioState.sliceOrInstance}
                </span>
              </span>
            )}
          </div>

        </div>
      </GlassPanel>
    );
  }

  // Discovery / Connection UI
  return (
    <GlassPanel
      title="Rig"
      icon={<Radio className="w-5 h-5" />}
    >
      <div className="p-4 space-y-4">
        {/* Radio Type Selection */}
        <div>
          <div className="text-xs text-gray-500 uppercase tracking-wider mb-2">
            Radio Type
          </div>
          <div className="grid grid-cols-2 gap-2">
            <button
              onClick={() => {
                setShowTciForm(!showTciForm);
                setShowHamlibForm(false);
              }}
              className={`px-4 py-3 rounded-lg text-sm font-medium transition-all border ${
                showTciForm
                  ? "bg-purple-500/20 text-purple-400 border-purple-500/30"
                  : "bg-dark-700 text-gray-300 hover:bg-dark-600 border-glass-100"
              }`}
            >
              <div className="flex flex-col items-center gap-1">
                <Radio className="w-5 h-5" />
                <span>TCI</span>
              </div>
            </button>
            <button
              onClick={() => {
                setShowHamlibForm(!showHamlibForm);
                setShowTciForm(false);
              }}
              className={`px-4 py-3 rounded-lg text-sm font-medium transition-all border ${
                showHamlibForm
                  ? "bg-orange-500/20 text-orange-400 border-orange-500/30"
                  : "bg-dark-700 text-gray-300 hover:bg-dark-600 border-glass-100"
              }`}
            >
              <div className="flex flex-col items-center gap-1">
                <Settings className="w-5 h-5" />
                <span>Hamlib</span>
              </div>
            </button>
          </div>
        </div>

        {/* Hamlib Configuration Form */}
        {showHamlibForm && (
          <div className="bg-dark-700/50 rounded-lg p-4 border border-orange-500/30 space-y-4">
            <div className="text-xs text-orange-400 uppercase tracking-wider">
              Hamlib Rig Configuration
            </div>

            {/* Rig Model Selector */}
            <div className="relative">
              <label className="block text-xs text-gray-500 mb-1">Rig Model</label>
              <input
                type="text"
                value={rigSearch}
                onChange={(e) => {
                  setRigSearch(e.target.value);
                  setShowRigDropdown(true);
                }}
                onFocus={() => setShowRigDropdown(true)}
                placeholder="Search for rig model..."
                className="w-full px-3 py-2 bg-dark-800 border border-glass-100 rounded-lg text-sm text-gray-200 focus:outline-none focus:border-orange-500/50"
              />
              {showRigDropdown && filteredRigs.length > 0 && (
                <div className="absolute z-50 w-full mt-1 max-h-48 overflow-y-auto bg-dark-800 border border-glass-100 rounded-lg shadow-lg">
                  {filteredRigs.map((rig) => (
                    <button
                      key={rig.modelId}
                      onClick={() => handleRigSelect(rig)}
                      className="w-full px-3 py-2 text-left text-sm text-gray-200 hover:bg-dark-700 transition-colors"
                    >
                      <div className="font-medium">{rig.displayName}</div>
                      <div className="text-xs text-gray-500">{rig.manufacturer} - {rig.model}</div>
                    </button>
                  ))}
                </div>
              )}
            </div>

            {/* Connection Type Toggle */}
            <div>
              <label className="block text-xs text-gray-500 mb-1">Connection Type</label>
              <div className="flex gap-2">
                <button
                  onClick={() => updateHamlibConfig({ connectionType: "Serial" })}
                  disabled={hamlibCaps ? !hamlibCaps.supportsSerial : false}
                  className={`flex-1 px-3 py-2 rounded-lg text-sm font-medium transition-all ${
                    hamlibConfig.connectionType === "Serial"
                      ? "bg-orange-500/20 text-orange-400 border border-orange-500/30"
                      : "bg-dark-800 text-gray-400 border border-glass-100 hover:bg-dark-700"
                  } disabled:opacity-50 disabled:cursor-not-allowed`}
                >
                  Serial
                </button>
                <button
                  onClick={() => updateHamlibConfig({ connectionType: "Network" })}
                  disabled={hamlibCaps ? !hamlibCaps.supportsNetwork : false}
                  className={`flex-1 px-3 py-2 rounded-lg text-sm font-medium transition-all ${
                    hamlibConfig.connectionType === "Network"
                      ? "bg-orange-500/20 text-orange-400 border border-orange-500/30"
                      : "bg-dark-800 text-gray-400 border border-glass-100 hover:bg-dark-700"
                  } disabled:opacity-50 disabled:cursor-not-allowed`}
                >
                  Network
                </button>
              </div>
            </div>

            {/* Serial Settings */}
            {hamlibConfig.connectionType === "Serial" && (
              <div className="space-y-3">
                <div className="grid grid-cols-2 gap-3">
                  <div>
                    <label className="block text-xs text-gray-500 mb-1">Serial Port</label>
                    <select
                      value={hamlibConfig.serialPort || ""}
                      onChange={(e) => updateHamlibConfig({ serialPort: e.target.value })}
                      className="w-full px-3 py-2 bg-dark-800 border border-glass-100 rounded-lg text-sm text-gray-200 focus:outline-none focus:border-orange-500/50"
                    >
                      <option value="">Select port...</option>
                      {serialPorts.map((port) => (
                        <option key={port} value={port}>{port}</option>
                      ))}
                    </select>
                  </div>
                  <div>
                    <label className="block text-xs text-gray-500 mb-1">Baud Rate</label>
                    <select
                      value={hamlibConfig.baudRate}
                      onChange={(e) => updateHamlibConfig({ baudRate: parseInt(e.target.value) })}
                      className="w-full px-3 py-2 bg-dark-800 border border-glass-100 rounded-lg text-sm text-gray-200 focus:outline-none focus:border-orange-500/50"
                    >
                      {HAMLIB_BAUD_RATES.map((rate) => (
                        <option key={rate} value={rate}>{rate}</option>
                      ))}
                    </select>
                  </div>
                </div>

                <div className="grid grid-cols-4 gap-2">
                  <div>
                    <label className="block text-xs text-gray-500 mb-1">Data Bits</label>
                    <select
                      value={hamlibConfig.dataBits}
                      onChange={(e) => updateHamlibConfig({ dataBits: parseInt(e.target.value) as HamlibDataBits })}
                      className="w-full px-2 py-2 bg-dark-800 border border-glass-100 rounded-lg text-sm text-gray-200 focus:outline-none focus:border-orange-500/50"
                    >
                      <option value={5}>5</option>
                      <option value={6}>6</option>
                      <option value={7}>7</option>
                      <option value={8}>8</option>
                    </select>
                  </div>
                  <div>
                    <label className="block text-xs text-gray-500 mb-1">Stop Bits</label>
                    <select
                      value={hamlibConfig.stopBits}
                      onChange={(e) => updateHamlibConfig({ stopBits: parseInt(e.target.value) as HamlibStopBits })}
                      className="w-full px-2 py-2 bg-dark-800 border border-glass-100 rounded-lg text-sm text-gray-200 focus:outline-none focus:border-orange-500/50"
                    >
                      <option value={1}>1</option>
                      <option value={2}>2</option>
                    </select>
                  </div>
                  <div>
                    <label className="block text-xs text-gray-500 mb-1">Flow</label>
                    <select
                      value={hamlibConfig.flowControl}
                      onChange={(e) => updateHamlibConfig({ flowControl: e.target.value as HamlibFlowControl })}
                      className="w-full px-2 py-2 bg-dark-800 border border-glass-100 rounded-lg text-sm text-gray-200 focus:outline-none focus:border-orange-500/50"
                    >
                      <option value="None">None</option>
                      <option value="Hardware">HW</option>
                      <option value="Software">SW</option>
                    </select>
                  </div>
                  <div>
                    <label className="block text-xs text-gray-500 mb-1">Parity</label>
                    <select
                      value={hamlibConfig.parity}
                      onChange={(e) => updateHamlibConfig({ parity: e.target.value as HamlibParity })}
                      className="w-full px-2 py-2 bg-dark-800 border border-glass-100 rounded-lg text-sm text-gray-200 focus:outline-none focus:border-orange-500/50"
                    >
                      <option value="None">None</option>
                      <option value="Even">Even</option>
                      <option value="Odd">Odd</option>
                      <option value="Mark">Mark</option>
                      <option value="Space">Space</option>
                    </select>
                  </div>
                </div>

                {/* PTT Configuration */}
                <div className="grid grid-cols-2 gap-3">
                  <div>
                    <label className="block text-xs text-gray-500 mb-1">PTT Type</label>
                    <select
                      value={hamlibConfig.pttType}
                      onChange={(e) => updateHamlibConfig({ pttType: e.target.value as HamlibPttType })}
                      className="w-full px-3 py-2 bg-dark-800 border border-glass-100 rounded-lg text-sm text-gray-200 focus:outline-none focus:border-orange-500/50"
                    >
                      <option value="None">None</option>
                      <option value="Rig">CAT (RIG)</option>
                      <option value="Dtr">DTR</option>
                      <option value="Rts">RTS</option>
                    </select>
                  </div>
                  {(hamlibConfig.pttType === "Dtr" || hamlibConfig.pttType === "Rts") && (
                    <div>
                      <label className="block text-xs text-gray-500 mb-1">PTT Port</label>
                      <select
                        value={hamlibConfig.pttPort || ""}
                        onChange={(e) => updateHamlibConfig({ pttPort: e.target.value })}
                        className="w-full px-3 py-2 bg-dark-800 border border-glass-100 rounded-lg text-sm text-gray-200 focus:outline-none focus:border-orange-500/50"
                      >
                        <option value="">Same as data</option>
                        {serialPorts.map((port) => (
                          <option key={port} value={port}>{port}</option>
                        ))}
                      </select>
                    </div>
                  )}
                </div>
              </div>
            )}

            {/* Network Settings */}
            {hamlibConfig.connectionType === "Network" && (
              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="block text-xs text-gray-500 mb-1">Hostname</label>
                  <input
                    type="text"
                    value={hamlibConfig.hostname || ""}
                    onChange={(e) => updateHamlibConfig({ hostname: e.target.value })}
                    placeholder="localhost"
                    className="w-full px-3 py-2 bg-dark-800 border border-glass-100 rounded-lg text-sm text-gray-200 focus:outline-none focus:border-orange-500/50"
                  />
                </div>
                <div>
                  <label className="block text-xs text-gray-500 mb-1">Port</label>
                  <input
                    type="number"
                    value={hamlibConfig.networkPort}
                    onChange={(e) => updateHamlibConfig({ networkPort: parseInt(e.target.value) || 4532 })}
                    placeholder="4532"
                    className="w-full px-3 py-2 bg-dark-800 border border-glass-100 rounded-lg text-sm text-gray-200 focus:outline-none focus:border-orange-500/50"
                  />
                </div>
              </div>
            )}

            {/* Advanced Options Toggle */}
            <button
              onClick={() => setShowAdvanced(!showAdvanced)}
              className="flex items-center gap-1 text-xs text-gray-500 hover:text-gray-400 transition-colors"
            >
              {showAdvanced ? <ChevronUp className="w-3 h-3" /> : <ChevronDown className="w-3 h-3" />}
              Advanced Options
            </button>

            {/* Advanced Options */}
            {showAdvanced && (
              <div className="space-y-3 pt-2 border-t border-glass-100">
                <div className="text-xs text-gray-500 mb-2">Feature Toggles</div>
                <div className="grid grid-cols-2 gap-2">
                  <FeatureToggle
                    label="Get Frequency"
                    checked={hamlibConfig.getFrequency}
                    onChange={(v) => updateHamlibConfig({ getFrequency: v })}
                    disabled={hamlibCaps ? !hamlibCaps.canGetFreq : false}
                  />
                  <FeatureToggle
                    label="Get Mode"
                    checked={hamlibConfig.getMode}
                    onChange={(v) => updateHamlibConfig({ getMode: v })}
                    disabled={hamlibCaps ? !hamlibCaps.canGetMode : false}
                  />
                  <FeatureToggle
                    label="Get VFO"
                    checked={hamlibConfig.getVfo}
                    onChange={(v) => updateHamlibConfig({ getVfo: v })}
                    disabled={hamlibCaps ? !hamlibCaps.canGetVfo : false}
                  />
                  <FeatureToggle
                    label="Get PTT"
                    checked={hamlibConfig.getPtt}
                    onChange={(v) => updateHamlibConfig({ getPtt: v })}
                    disabled={hamlibCaps ? !hamlibCaps.canGetPtt : false}
                  />
                  <FeatureToggle
                    label="Get Power"
                    checked={hamlibConfig.getPower}
                    onChange={(v) => updateHamlibConfig({ getPower: v })}
                    disabled={hamlibCaps ? !hamlibCaps.canGetPower : false}
                  />
                  <FeatureToggle
                    label="Get RIT"
                    checked={hamlibConfig.getRit}
                    onChange={(v) => updateHamlibConfig({ getRit: v })}
                    disabled={hamlibCaps ? !hamlibCaps.canGetRit : false}
                  />
                  <FeatureToggle
                    label="Get XIT"
                    checked={hamlibConfig.getXit}
                    onChange={(v) => updateHamlibConfig({ getXit: v })}
                    disabled={hamlibCaps ? !hamlibCaps.canGetXit : false}
                  />
                  <FeatureToggle
                    label="Get Key Speed"
                    checked={hamlibConfig.getKeySpeed}
                    onChange={(v) => updateHamlibConfig({ getKeySpeed: v })}
                    disabled={hamlibCaps ? !hamlibCaps.canGetKeySpeed : false}
                  />
                </div>

                <div>
                  <label className="block text-xs text-gray-500 mb-1">Poll Interval (ms)</label>
                  <input
                    type="number"
                    value={hamlibConfig.pollIntervalMs}
                    onChange={(e) => updateHamlibConfig({ pollIntervalMs: parseInt(e.target.value) || 250 })}
                    min={50}
                    max={5000}
                    className="w-32 px-3 py-2 bg-dark-800 border border-glass-100 rounded-lg text-sm text-gray-200 focus:outline-none focus:border-orange-500/50"
                  />
                </div>
              </div>
            )}

            {/* Action Buttons */}
            <div className="flex gap-2 pt-2">
              <button
                onClick={handleConnectHamlib}
                disabled={isConnectingHamlib || !hamlibConfig.modelId || (hamlibConfig.connectionType === "Serial" && !hamlibConfig.serialPort) || (hamlibConfig.connectionType === "Network" && !hamlibConfig.hostname)}
                className="flex-1 px-4 py-2 text-sm font-medium flex items-center justify-center gap-2 bg-orange-500/20 text-orange-400 rounded-lg hover:bg-orange-500/30 transition-all disabled:opacity-50"
              >
                {isConnectingHamlib ? (
                  <>
                    <Wifi className="w-4 h-4 animate-pulse" />
                    Connecting...
                  </>
                ) : (
                  <>
                    <Plus className="w-4 h-4" />
                    Connect
                  </>
                )}
              </button>
              <button
                onClick={() => setShowHamlibForm(false)}
                className="px-4 py-2 text-sm font-medium bg-dark-700 text-gray-400 rounded-lg hover:bg-dark-600 transition-all"
              >
                Cancel
              </button>
            </div>
          </div>
        )}

        {/* TCI Connection Form */}
        {showTciForm && (
          <div className="bg-dark-700/50 rounded-lg p-4 border border-purple-500/30 space-y-3">
            <div className="text-xs text-purple-400 uppercase tracking-wider mb-2">
              Connect to TCI Server
            </div>
            <div className="grid grid-cols-2 gap-3">
              <div>
                <label className="block text-xs text-gray-500 mb-1">Host</label>
                <input
                  type="text"
                  value={tciSettings.host}
                  onChange={(e) => updateTciSettings({ host: e.target.value })}
                  placeholder="localhost"
                  className="w-full px-3 py-2 bg-dark-800 border border-glass-100 rounded-lg text-sm text-gray-200 focus:outline-none focus:border-purple-500/50"
                />
              </div>
              <div>
                <label className="block text-xs text-gray-500 mb-1">Port</label>
                <input
                  type="number"
                  value={tciSettings.port}
                  onChange={(e) => updateTciSettings({ port: parseInt(e.target.value) || 50001 })}
                  placeholder="50001"
                  className="w-full px-3 py-2 bg-dark-800 border border-glass-100 rounded-lg text-sm text-gray-200 focus:outline-none focus:border-purple-500/50"
                />
              </div>
            </div>
            <div>
              <label className="block text-xs text-gray-500 mb-1">Name (optional)</label>
              <input
                type="text"
                value={tciSettings.name}
                onChange={(e) => updateTciSettings({ name: e.target.value })}
                placeholder="My TCI Radio"
                className="w-full px-3 py-2 bg-dark-800 border border-glass-100 rounded-lg text-sm text-gray-200 focus:outline-none focus:border-purple-500/50"
              />
            </div>
            <label className="flex items-center gap-2 text-sm text-gray-300 cursor-pointer">
              <input
                type="checkbox"
                checked={tciSettings.autoConnect}
                onChange={(e) => {
                  updateTciSettings({ autoConnect: e.target.checked });
                  saveSettings();
                }}
                className="w-4 h-4 rounded border-glass-100 bg-dark-800 text-purple-500 focus:ring-purple-500/50"
              />
              Auto-connect on startup
            </label>
            <div className="flex gap-2 pt-2">
              <button
                onClick={handleConnectTci}
                disabled={isConnectingTci || !tciSettings.host}
                className="flex-1 px-4 py-2 text-sm font-medium flex items-center justify-center gap-2 bg-purple-500/20 text-purple-400 rounded-lg hover:bg-purple-500/30 transition-all disabled:opacity-50"
              >
                {isConnectingTci ? (
                  <>
                    <Wifi className="w-4 h-4 animate-pulse" />
                    Connecting...
                  </>
                ) : (
                  <>
                    <Plus className="w-4 h-4" />
                    Connect
                  </>
                )}
              </button>
              <button
                onClick={() => setShowTciForm(false)}
                className="px-4 py-2 text-sm font-medium bg-dark-700 text-gray-400 rounded-lg hover:bg-dark-600 transition-all"
              >
                Cancel
              </button>
            </div>
          </div>
        )}

        {/* Discovered Radios */}
        {radios.length > 0 ? (
          <div>
            <div className="text-xs text-gray-500 uppercase tracking-wider mb-2">
              Discovered Radios ({radios.length})
            </div>
            <div className="space-y-2">
              {radios.map((radio) => {
                const connectionState = radioConnectionStates.get(radio.id);
                const isConnecting = connectionState === "Connecting";

                return (
                  <div
                    key={radio.id}
                    className="flex items-center justify-between p-3 bg-dark-700/50 rounded-lg border border-glass-100"
                  >
                    <div className="flex items-center gap-3">
                      <div
                        className={`p-2 rounded-lg ${
                          radio.type === "Hamlib"
                            ? "bg-orange-500/20"
                            : "bg-purple-500/20"
                        }`}
                      >
                        {radio.type === "Hamlib" ? (
                          <Settings className="w-4 h-4 text-orange-400" />
                        ) : (
                          <Radio className="w-4 h-4 text-purple-400" />
                        )}
                      </div>
                      <div>
                        <div className="text-sm font-medium text-gray-200">
                          {radio.nickname || radio.model}
                        </div>
                        <div className="text-xs text-gray-500 font-mono">
                          {radio.ipAddress}:{radio.port}
                        </div>
                      </div>
                    </div>
                    <button
                      onClick={() => handleConnect(radio.id)}
                      disabled={isConnecting}
                      className="px-3 py-1.5 text-xs font-medium flex items-center gap-1.5 bg-accent-primary/20 text-accent-primary rounded-lg hover:bg-accent-primary/30 transition-all disabled:opacity-50"
                    >
                      {isConnecting ? (
                        <>
                          <Wifi className="w-3.5 h-3.5 animate-pulse" />
                          Connecting...
                        </>
                      ) : (
                        <>
                          <Power className="w-3.5 h-3.5" />
                          Connect
                        </>
                      )}
                    </button>
                  </div>
                );
              })}
            </div>
          </div>
        ) : (
          <div className="flex flex-col items-center justify-center py-8 text-gray-500">
            <WifiOff className="w-8 h-8 mb-3 opacity-50" />
            <p className="text-sm text-center">No radios discovered</p>
            <p className="text-xs text-gray-600 mt-1 text-center">
              Select a radio type above to start discovery
            </p>
          </div>
        )}
      </div>
    </GlassPanel>
  );
}

interface FeatureToggleProps {
  label: string;
  checked: boolean;
  onChange: (value: boolean) => void;
  disabled?: boolean;
}

function FeatureToggle({ label, checked, onChange, disabled }: FeatureToggleProps) {
  return (
    <label className={`flex items-center gap-2 text-sm ${disabled ? 'opacity-50 cursor-not-allowed' : 'cursor-pointer'}`}>
      <input
        type="checkbox"
        checked={checked}
        onChange={(e) => onChange(e.target.checked)}
        disabled={disabled}
        className="w-4 h-4 rounded bg-dark-800 border-glass-100 text-orange-500 focus:ring-orange-500/50"
      />
      <span className="text-gray-300">{label}</span>
    </label>
  );
}

interface TxRxIndicatorProps {
  isTransmitting: boolean;
}

function TxRxIndicator({ isTransmitting }: TxRxIndicatorProps) {
  return (
    <div className="flex rounded-lg overflow-hidden border border-glass-100">
      <div
        className={`px-3 py-1.5 text-xs font-bold transition-all ${
          !isTransmitting
            ? "bg-green-500/30 text-green-400 border-r border-green-500/30"
            : "bg-dark-700 text-gray-600 border-r border-glass-100"
        }`}
      >
        RX
      </div>
      <div
        className={`px-3 py-1.5 text-xs font-bold transition-all ${
          isTransmitting
            ? "bg-red-500/30 text-red-400 animate-pulse"
            : "bg-dark-700 text-gray-600"
        }`}
      >
        TX
      </div>
    </div>
  );
}
