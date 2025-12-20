import { useState } from "react";
import { Radio, Wifi, WifiOff, Search, Power, PowerOff, Plus, Terminal } from "lucide-react";
import { useAppStore } from "../store/appStore";
import { useSignalR } from "../hooks/useSignalR";
import { GlassPanel } from "../components/GlassPanel";
import type { RadioConnectionState } from "../api/signalr";

export function RadioPlugin() {
  const {
    discoveredRadios,
    radioConnectionStates,
    radioStates,
    selectedRadioId,
    setSelectedRadio,
  } = useAppStore();

  const {
    startRadioDiscovery,
    stopRadioDiscovery,
    connectRadio,
    disconnectRadio,
    selectRadioSlice,
    connectHamlib,
    disconnectHamlib,
    connectTci,
    disconnectTci,
  } = useSignalR();

  const [selectedType, setSelectedType] = useState<'FlexRadio' | 'Tci' | null>(null);
  const [isDiscovering, setIsDiscovering] = useState(false);
  const [showHamlibForm, setShowHamlibForm] = useState(false);
  const [hamlibHost, setHamlibHost] = useState("localhost");
  const [hamlibPort, setHamlibPort] = useState("4532");
  const [hamlibName, setHamlibName] = useState("");
  const [isConnectingHamlib, setIsConnectingHamlib] = useState(false);
  const [showTciForm, setShowTciForm] = useState(false);
  const [tciHost, setTciHost] = useState("localhost");
  const [tciPort, setTciPort] = useState("40001");
  const [tciName, setTciName] = useState("");
  const [isConnectingTci, setIsConnectingTci] = useState(false);

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

  const handleStartDiscovery = async (type: 'FlexRadio' | 'Tci') => {
    setSelectedType(type);
    setIsDiscovering(true);
    await startRadioDiscovery(type);
  };

  const handleStopDiscovery = async () => {
    if (selectedType) {
      await stopRadioDiscovery(selectedType);
    }
    setIsDiscovering(false);
  };

  const handleConnect = async (radioId: string) => {
    setSelectedRadio(radioId);
    await connectRadio(radioId);
  };

  const handleDisconnect = async () => {
    if (selectedRadioId) {
      const radio = discoveredRadios.get(selectedRadioId);
      if (radio?.type === "Hamlib") {
        await disconnectHamlib(selectedRadioId);
      } else if (radio?.type === "Tci" && selectedRadioId.startsWith("tci-")) {
        await disconnectTci(selectedRadioId);
      } else {
        await disconnectRadio(selectedRadioId);
      }
      setSelectedRadio(null);
    }
  };

  const handleConnectHamlib = async () => {
    const port = parseInt(hamlibPort, 10);
    if (!hamlibHost || isNaN(port)) return;

    setIsConnectingHamlib(true);
    try {
      await connectHamlib(hamlibHost, port, hamlibName || undefined);
      setShowHamlibForm(false);
      setHamlibHost("localhost");
      setHamlibPort("4532");
      setHamlibName("");
    } finally {
      setIsConnectingHamlib(false);
    }
  };

  const handleConnectTci = async () => {
    const port = parseInt(tciPort, 10);
    if (!tciHost || isNaN(port)) return;

    setIsConnectingTci(true);
    try {
      await connectTci(tciHost, port, tciName || undefined);
      setShowTciForm(false);
      setTciHost("localhost");
      setTciPort("40001");
      setTciName("");
    } finally {
      setIsConnectingTci(false);
    }
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

  // If we have an active radio connection, show the status display
  if (
    selectedRadio &&
    selectedConnectionState &&
    ["Connected", "Monitoring"].includes(selectedConnectionState)
  ) {
    return (
      <GlassPanel
        title="Radio"
        icon={<Radio className="w-5 h-5" />}
        actions={
          <div className="flex items-center gap-2">
            <span
              className={`flex items-center gap-1.5 text-xs ${getConnectionStateColor(
                selectedConnectionState
              )}`}
            >
              <Wifi className="w-3.5 h-3.5" />
              {getConnectionStateText(selectedConnectionState)}
            </span>
          </div>
        }
      >
        <div className="p-4 space-y-4">
          {/* Radio Info */}
          <div className="flex items-center justify-between">
            <div className="text-sm text-gray-400">
              <span className="font-medium text-gray-200">
                {selectedRadio.model}
              </span>
              <span className="mx-2">|</span>
              <span className="font-mono text-xs">
                {selectedRadio.ipAddress}
              </span>
            </div>
            <button
              onClick={handleDisconnect}
              className="px-3 py-1.5 text-xs font-medium flex items-center gap-1.5 bg-red-500/20 text-red-400 rounded-lg hover:bg-red-500/30 transition-all"
            >
              <PowerOff className="w-3.5 h-3.5" />
              Disconnect
            </button>
          </div>

          {/* Frequency Display */}
          <div className="bg-dark-700/50 rounded-lg p-4 border border-glass-100">
            <div className="text-xs text-gray-500 uppercase tracking-wider mb-1">
              Frequency
            </div>
            <div className="text-3xl font-bold text-accent-primary font-mono">
              {selectedRadioState
                ? formatFrequency(selectedRadioState.frequencyHz)
                : "---"}
              <span className="text-lg font-normal text-gray-500 ml-2">
                MHz
              </span>
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

          {/* Slice Selection for FlexRadio */}
          {selectedRadio.type === "FlexRadio" &&
            selectedRadio.slices &&
            selectedRadio.slices.length > 0 && (
              <div className="mt-4">
                <div className="text-xs text-gray-500 uppercase tracking-wider mb-2">
                  Select Slice
                </div>
                <div className="flex gap-2">
                  {selectedRadio.slices.map((slice) => (
                    <button
                      key={slice}
                      onClick={() => selectRadioSlice(selectedRadio.id, slice)}
                      className={`px-4 py-2 rounded-lg text-sm font-medium transition-all ${
                        selectedRadioState?.sliceOrInstance === slice
                          ? "bg-accent-primary/20 text-accent-primary border border-accent-primary/30"
                          : "bg-dark-700 text-gray-400 hover:bg-dark-600 border border-glass-100"
                      }`}
                    >
                      Slice {slice}
                    </button>
                  ))}
                </div>
              </div>
            )}
        </div>
      </GlassPanel>
    );
  }

  // Discovery / Connection UI
  return (
    <GlassPanel
      title="Radio"
      icon={<Radio className="w-5 h-5" />}
      actions={
        isDiscovering ? (
          <button
            onClick={handleStopDiscovery}
            className="flex items-center gap-1.5 px-2 py-1 text-xs text-amber-400 hover:bg-amber-500/10 rounded transition-all"
          >
            <Search className="w-3.5 h-3.5 animate-pulse" />
            Stop
          </button>
        ) : null
      }
    >
      <div className="p-4 space-y-4">
        {/* Radio Type Selection */}
        <div>
          <div className="text-xs text-gray-500 uppercase tracking-wider mb-2">
            Radio Type
          </div>
          <div className="grid grid-cols-3 gap-2">
            <button
              onClick={() => handleStartDiscovery("FlexRadio")}
              disabled={isDiscovering || showHamlibForm || showTciForm}
              className={`px-4 py-3 rounded-lg text-sm font-medium transition-all border ${
                selectedType === "FlexRadio" && isDiscovering
                  ? "bg-accent-primary/20 text-accent-primary border-accent-primary/30"
                  : "bg-dark-700 text-gray-300 hover:bg-dark-600 border-glass-100 disabled:opacity-50"
              }`}
            >
              <div className="flex flex-col items-center gap-1">
                <Radio className="w-5 h-5" />
                <span>FlexRadio</span>
              </div>
            </button>
            <button
              onClick={() => {
                setShowTciForm(!showTciForm);
                setShowHamlibForm(false);
              }}
              disabled={isDiscovering}
              className={`px-4 py-3 rounded-lg text-sm font-medium transition-all border ${
                showTciForm
                  ? "bg-purple-500/20 text-purple-400 border-purple-500/30"
                  : "bg-dark-700 text-gray-300 hover:bg-dark-600 border-glass-100 disabled:opacity-50"
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
              disabled={isDiscovering}
              className={`px-4 py-3 rounded-lg text-sm font-medium transition-all border ${
                showHamlibForm
                  ? "bg-orange-500/20 text-orange-400 border-orange-500/30"
                  : "bg-dark-700 text-gray-300 hover:bg-dark-600 border-glass-100 disabled:opacity-50"
              }`}
            >
              <div className="flex flex-col items-center gap-1">
                <Terminal className="w-5 h-5" />
                <span>rigctld</span>
              </div>
            </button>
          </div>
        </div>

        {/* rigctld Connection Form */}
        {showHamlibForm && (
          <div className="bg-dark-700/50 rounded-lg p-4 border border-orange-500/30 space-y-3">
            <div className="text-xs text-orange-400 uppercase tracking-wider mb-2">
              Connect to rigctld
            </div>
            <div className="grid grid-cols-2 gap-3">
              <div>
                <label className="block text-xs text-gray-500 mb-1">Host</label>
                <input
                  type="text"
                  value={hamlibHost}
                  onChange={(e) => setHamlibHost(e.target.value)}
                  placeholder="localhost"
                  className="w-full px-3 py-2 bg-dark-800 border border-glass-100 rounded-lg text-sm text-gray-200 focus:outline-none focus:border-orange-500/50"
                />
              </div>
              <div>
                <label className="block text-xs text-gray-500 mb-1">Port</label>
                <input
                  type="number"
                  value={hamlibPort}
                  onChange={(e) => setHamlibPort(e.target.value)}
                  placeholder="4532"
                  className="w-full px-3 py-2 bg-dark-800 border border-glass-100 rounded-lg text-sm text-gray-200 focus:outline-none focus:border-orange-500/50"
                />
              </div>
            </div>
            <div>
              <label className="block text-xs text-gray-500 mb-1">Name (optional)</label>
              <input
                type="text"
                value={hamlibName}
                onChange={(e) => setHamlibName(e.target.value)}
                placeholder="My Radio"
                className="w-full px-3 py-2 bg-dark-800 border border-glass-100 rounded-lg text-sm text-gray-200 focus:outline-none focus:border-orange-500/50"
              />
            </div>
            <div className="flex gap-2 pt-2">
              <button
                onClick={handleConnectHamlib}
                disabled={isConnectingHamlib || !hamlibHost}
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
                  value={tciHost}
                  onChange={(e) => setTciHost(e.target.value)}
                  placeholder="localhost"
                  className="w-full px-3 py-2 bg-dark-800 border border-glass-100 rounded-lg text-sm text-gray-200 focus:outline-none focus:border-purple-500/50"
                />
              </div>
              <div>
                <label className="block text-xs text-gray-500 mb-1">Port</label>
                <input
                  type="number"
                  value={tciPort}
                  onChange={(e) => setTciPort(e.target.value)}
                  placeholder="40001"
                  className="w-full px-3 py-2 bg-dark-800 border border-glass-100 rounded-lg text-sm text-gray-200 focus:outline-none focus:border-purple-500/50"
                />
              </div>
            </div>
            <div>
              <label className="block text-xs text-gray-500 mb-1">Name (optional)</label>
              <input
                type="text"
                value={tciName}
                onChange={(e) => setTciName(e.target.value)}
                placeholder="My TCI Radio"
                className="w-full px-3 py-2 bg-dark-800 border border-glass-100 rounded-lg text-sm text-gray-200 focus:outline-none focus:border-purple-500/50"
              />
            </div>
            <div className="flex gap-2 pt-2">
              <button
                onClick={handleConnectTci}
                disabled={isConnectingTci || !tciHost}
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
                          radio.type === "FlexRadio"
                            ? "bg-blue-500/20"
                            : radio.type === "Hamlib"
                            ? "bg-orange-500/20"
                            : "bg-purple-500/20"
                        }`}
                      >
                        {radio.type === "Hamlib" ? (
                          <Terminal className="w-4 h-4 text-orange-400" />
                        ) : (
                          <Radio
                            className={`w-4 h-4 ${
                              radio.type === "FlexRadio"
                                ? "text-blue-400"
                                : "text-purple-400"
                            }`}
                          />
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
        ) : isDiscovering ? (
          <div className="flex flex-col items-center justify-center py-8 text-gray-500">
            <Search className="w-8 h-8 mb-3 animate-pulse" />
            <p className="text-sm">Searching for {selectedType} radios...</p>
            <p className="text-xs text-gray-600 mt-1">
              {selectedType === "FlexRadio" ? "UDP port 4992" : "UDP port 1024"}
            </p>
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
