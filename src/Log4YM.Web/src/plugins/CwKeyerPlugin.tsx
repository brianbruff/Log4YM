import { useState, useEffect, useCallback } from 'react';
import { Radio, Play, Square, Settings, Zap } from 'lucide-react';
import { useAppStore } from '../store/appStore';
import { useSignalR } from '../hooks/useSignalR';
import { GlassPanel } from '../components/GlassPanel';

// Default CW macros for common exchanges
const DEFAULT_MACROS = [
  { label: 'CQ', text: 'CQ CQ CQ DE {MYCALL} {MYCALL} K' },
  { label: '599', text: 'TU 599 {MYGRID} {MYGRID} K' },
  { label: 'QRZ', text: '{MYCALL} QRZ' },
  { label: '73', text: '73 GL {MYCALL} SK' },
  { label: 'AGN', text: 'AGN PSE' },
  { label: 'QSL', text: 'QSL TU 73' },
];

export function CwKeyerPlugin() {
  const { cwKeyerStatus, discoveredRadios, selectedRadioId, radioConnectionStates } = useAppStore();
  const { sendCwKey, stopCwKey, setCwSpeed } = useSignalR();

  // Local state
  const [messageText, setMessageText] = useState('');
  const [speedWpm, setSpeedWpm] = useState(20);
  const [transmitAsYouType, setTransmitAsYouType] = useState(false);
  const [typingTimeout, setTypingTimeout] = useState<NodeJS.Timeout | null>(null);
  const [showSettings, setShowSettings] = useState(false);
  const [customMacros, setCustomMacros] = useState(DEFAULT_MACROS);

  // Get the first connected radio or selected radio
  const activeRadioId = selectedRadioId || Array.from(discoveredRadios.keys()).find(id => {
    const state = radioConnectionStates.get(id);
    return state === 'Connected' || state === 'Monitoring';
  });

  // Get CW keyer status for active radio
  const keyerStatus = activeRadioId ? cwKeyerStatus.get(activeRadioId) : null;
  const isKeying = keyerStatus?.isKeying ?? false;

  // Update speed when keyer status changes
  useEffect(() => {
    if (keyerStatus?.speedWpm && keyerStatus.speedWpm !== speedWpm) {
      setSpeedWpm(keyerStatus.speedWpm);
    }
  }, [keyerStatus?.speedWpm]);

  // Handle "transmit as you type" functionality
  const handleMessageChange = useCallback((newText: string) => {
    setMessageText(newText);

    if (!transmitAsYouType || !activeRadioId) return;

    // Clear existing timeout
    if (typingTimeout) {
      clearTimeout(typingTimeout);
    }

    // Set new timeout to transmit after 500ms of no typing
    const timeout = setTimeout(() => {
      if (newText.trim()) {
        sendCwKey(activeRadioId, newText, speedWpm);
      }
    }, 500);

    setTypingTimeout(timeout);
  }, [transmitAsYouType, activeRadioId, speedWpm, sendCwKey, typingTimeout]);

  // Handle manual transmit button
  const handleTransmit = useCallback(async () => {
    if (!activeRadioId || !messageText.trim()) return;
    await sendCwKey(activeRadioId, messageText, speedWpm);
  }, [activeRadioId, messageText, speedWpm, sendCwKey]);

  // Handle stop transmission
  const handleStop = useCallback(async () => {
    if (!activeRadioId) return;
    await stopCwKey(activeRadioId);
  }, [activeRadioId, stopCwKey]);

  // Handle speed change
  const handleSpeedChange = useCallback(async (newSpeed: number) => {
    setSpeedWpm(newSpeed);
    if (activeRadioId) {
      await setCwSpeed(activeRadioId, newSpeed);
    }
  }, [activeRadioId, setCwSpeed]);

  // Handle macro click
  const handleMacroClick = useCallback(async (macroText: string) => {
    if (!activeRadioId) return;

    // Simple macro variable substitution
    // In a full implementation, these would come from settings/station info
    let processedText = macroText
      .replace('{MYCALL}', 'STATION')
      .replace('{MYGRID}', 'FN31');

    setMessageText(processedText);
    await sendCwKey(activeRadioId, processedText, speedWpm);
  }, [activeRadioId, speedWpm, sendCwKey]);

  // Cleanup timeout on unmount
  useEffect(() => {
    return () => {
      if (typingTimeout) {
        clearTimeout(typingTimeout);
      }
    };
  }, [typingTimeout]);

  const hasActiveRadio = !!activeRadioId;

  return (
    <GlassPanel
      title="CW Keyer"
      icon={<Radio className="w-5 h-5" />}
      actions={
        <div className="flex items-center gap-2">
          {isKeying && (
            <div className="flex items-center gap-1.5 text-xs font-medium text-accent-danger animate-pulse">
              <Zap className="w-3.5 h-3.5" />
              Keying
            </div>
          )}
          <span className="text-xs text-dark-300 font-mono">
            {speedWpm} WPM
          </span>
          <button
            onClick={() => setShowSettings(true)}
            className="glass-button p-1.5"
            title="Macro Settings"
          >
            <Settings className="w-4 h-4" />
          </button>
        </div>
      }
    >
      <div className="p-4 space-y-4">
        {/* No radio warning */}
        {!hasActiveRadio && (
          <div className="bg-accent-warning/10 border border-accent-warning/30 rounded-lg p-3 text-sm text-accent-warning">
            <p className="font-medium">No radio connected</p>
            <p className="text-xs mt-1">Connect a radio in the Rig panel to use CW keyer</p>
          </div>
        )}

        {/* Transmit Mode Toggle */}
        <div className="flex items-center justify-between">
          <label className="flex items-center gap-2 cursor-pointer">
            <input
              type="checkbox"
              checked={transmitAsYouType}
              onChange={(e) => setTransmitAsYouType(e.target.checked)}
              disabled={!hasActiveRadio}
              className="w-4 h-4 rounded bg-dark-800 border-glass-100 text-accent-primary focus:ring-accent-primary/50 disabled:opacity-50"
            />
            <span className="text-sm text-dark-200 font-ui">Transmit as you type</span>
          </label>
          <div className="text-xs text-dark-400">
            {transmitAsYouType ? 'Sends after 500ms pause' : 'Manual transmit'}
          </div>
        </div>

        {/* Message Input */}
        <div className="space-y-2">
          <label className="block text-xs text-dark-300 uppercase tracking-wider font-ui">
            Message
          </label>
          <textarea
            value={messageText}
            onChange={(e) => handleMessageChange(e.target.value)}
            disabled={!hasActiveRadio}
            placeholder="Type your CW message here..."
            className="w-full h-24 px-3 py-2 bg-dark-800 border border-glass-100 rounded-lg text-sm text-dark-200 font-mono focus:outline-none focus:border-accent-primary/50 resize-none disabled:opacity-50"
          />
          {keyerStatus?.currentMessage && (
            <div className="text-xs text-accent-primary font-mono">
              Sending: {keyerStatus.currentMessage}
            </div>
          )}
        </div>

        {/* Speed Control */}
        <div className="space-y-2">
          <label className="block text-xs text-dark-300 uppercase tracking-wider font-ui">
            Speed (WPM)
          </label>
          <div className="flex items-center gap-3">
            <input
              type="range"
              min="5"
              max="60"
              value={speedWpm}
              onChange={(e) => handleSpeedChange(parseInt(e.target.value))}
              disabled={!hasActiveRadio}
              className="flex-1 h-2 bg-dark-800 rounded-lg appearance-none cursor-pointer accent-accent-primary disabled:opacity-50"
            />
            <input
              type="number"
              min="5"
              max="60"
              value={speedWpm}
              onChange={(e) => handleSpeedChange(parseInt(e.target.value) || 20)}
              disabled={!hasActiveRadio}
              className="w-16 px-2 py-1 bg-dark-800 border border-glass-100 rounded text-sm text-dark-200 font-mono text-center focus:outline-none focus:border-accent-primary/50 disabled:opacity-50"
            />
          </div>
        </div>

        {/* Control Buttons */}
        <div className="flex gap-2">
          <button
            onClick={handleTransmit}
            disabled={!hasActiveRadio || !messageText.trim() || isKeying || transmitAsYouType}
            className="flex-1 px-4 py-2 text-sm font-medium font-ui flex items-center justify-center gap-2 bg-accent-success/20 text-accent-success rounded-lg hover:bg-accent-success/30 transition-all disabled:opacity-50 disabled:cursor-not-allowed"
          >
            <Play className="w-4 h-4" />
            Transmit
          </button>
          <button
            onClick={handleStop}
            disabled={!hasActiveRadio || !isKeying}
            className="flex-1 px-4 py-2 text-sm font-medium font-ui flex items-center justify-center gap-2 bg-accent-danger/20 text-accent-danger rounded-lg hover:bg-accent-danger/30 transition-all disabled:opacity-50 disabled:cursor-not-allowed"
          >
            <Square className="w-4 h-4" />
            Stop
          </button>
        </div>

        {/* Quick Macros */}
        <div className="space-y-2">
          <label className="block text-xs text-dark-300 uppercase tracking-wider font-ui">
            Quick Macros
          </label>
          <div className="grid grid-cols-3 gap-2">
            {customMacros.map((macro, index) => (
              <button
                key={index}
                onClick={() => handleMacroClick(macro.text)}
                disabled={!hasActiveRadio || isKeying}
                className="px-3 py-2 text-xs font-medium font-ui bg-dark-700 text-dark-200 rounded-lg hover:bg-dark-600 border border-glass-100 transition-all disabled:opacity-50 disabled:cursor-not-allowed"
                title={macro.text}
              >
                {macro.label}
              </button>
            ))}
          </div>
        </div>
      </div>

      {/* Settings Modal */}
      {showSettings && (
        <MacroSettingsModal
          macros={customMacros}
          onSave={(newMacros) => {
            setCustomMacros(newMacros);
            setShowSettings(false);
          }}
          onClose={() => setShowSettings(false)}
        />
      )}
    </GlassPanel>
  );
}

// Macro Settings Modal Component
interface MacroSettingsModalProps {
  macros: Array<{ label: string; text: string }>;
  onSave: (macros: Array<{ label: string; text: string }>) => void;
  onClose: () => void;
}

function MacroSettingsModal({ macros, onSave, onClose }: MacroSettingsModalProps) {
  const [editedMacros, setEditedMacros] = useState(macros);

  const handleMacroChange = (index: number, field: 'label' | 'text', value: string) => {
    const newMacros = [...editedMacros];
    newMacros[index] = { ...newMacros[index], [field]: value };
    setEditedMacros(newMacros);
  };

  const handleReset = () => {
    setEditedMacros(DEFAULT_MACROS);
  };

  return (
    <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50">
      <div className="bg-dark-800 rounded-xl border border-glass-100 shadow-2xl w-full max-w-2xl mx-4 max-h-[80vh] overflow-hidden flex flex-col">
        <div className="flex items-center justify-between px-4 py-3 border-b border-glass-100">
          <h3 className="text-lg font-ui font-semibold text-dark-200">CW Macro Settings</h3>
          <button
            onClick={onClose}
            className="text-dark-300 hover:text-dark-200 transition-colors"
          >
            <span className="text-xl">&times;</span>
          </button>
        </div>

        <div className="p-4 space-y-4 overflow-y-auto">
          <p className="text-sm font-ui text-dark-300">
            Configure quick-access CW macros. Use <span className="font-mono text-accent-primary">{'{MYCALL}'}</span> and <span className="font-mono text-accent-primary">{'{MYGRID}'}</span> for automatic substitution.
          </p>

          <div className="space-y-3">
            {editedMacros.map((macro, index) => (
              <div key={index} className="flex items-start gap-3 bg-dark-700/50 rounded-lg p-3 border border-glass-100">
                <span className="text-sm text-dark-300 font-mono w-6 mt-1">{index + 1}.</span>
                <div className="flex-1 space-y-2">
                  <input
                    type="text"
                    value={macro.label}
                    onChange={(e) => handleMacroChange(index, 'label', e.target.value)}
                    placeholder="Label"
                    maxLength={12}
                    className="w-full px-3 py-1.5 bg-dark-800 border border-glass-100 rounded text-sm text-dark-200 font-ui focus:outline-none focus:border-accent-primary/50"
                  />
                  <input
                    type="text"
                    value={macro.text}
                    onChange={(e) => handleMacroChange(index, 'text', e.target.value)}
                    placeholder="CW Text"
                    className="w-full px-3 py-1.5 bg-dark-800 border border-glass-100 rounded text-sm text-dark-200 font-mono focus:outline-none focus:border-accent-primary/50"
                  />
                </div>
              </div>
            ))}
          </div>

          <div className="text-xs text-dark-300 pt-2 border-t border-glass-100">
            <p className="font-ui font-medium mb-1">Available variables:</p>
            <ul className="space-y-0.5 list-disc list-inside font-mono">
              <li><span className="text-accent-primary">{'{MYCALL}'}</span> - Your callsign</li>
              <li><span className="text-accent-primary">{'{MYGRID}'}</span> - Your grid square</li>
            </ul>
          </div>
        </div>

        <div className="flex justify-between gap-2 px-4 py-3 border-t border-glass-100">
          <button
            onClick={handleReset}
            className="px-3 py-2 text-sm font-ui font-medium text-dark-300 hover:text-dark-200 transition-colors"
          >
            Reset to Defaults
          </button>
          <div className="flex gap-2">
            <button
              onClick={onClose}
              className="px-4 py-2 text-sm font-ui font-medium bg-dark-700 text-dark-200 rounded-lg hover:bg-dark-600 transition-all border border-glass-100"
            >
              Cancel
            </button>
            <button
              onClick={() => onSave(editedMacros)}
              className="px-4 py-2 text-sm font-ui font-medium bg-accent-primary text-white rounded-lg hover:bg-accent-primary/80 transition-all"
            >
              Save
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}
