import { useState } from 'react';
import { Radio, Plus, Pencil, Trash2, Wifi, WifiOff, Power, PowerOff, X } from 'lucide-react';
import { useAppStore } from '../store/appStore';
import { useSignalR } from '../hooks/useSignalR';
import { GlassPanel } from '../components/GlassPanel';
import { FLEX_RADIO_MODELS, type SmartUnlinkRadioDto } from '../api/signalr';

interface RadioFormData {
  id?: string;
  name: string;
  ipAddress: string;
  model: string;
  callsign: string;
  enabled: boolean;
  version: string;
}

const initialFormData: RadioFormData = {
  name: '',
  ipAddress: '',
  model: 'FLEX-6600',
  callsign: '',
  enabled: false,
  version: '4.1.3.39644',
};

export function SmartUnlinkPlugin() {
  const { smartUnlinkRadios } = useAppStore();
  const {
    addSmartUnlinkRadio,
    updateSmartUnlinkRadio,
    removeSmartUnlinkRadio,
    setSmartUnlinkRadioEnabled,
  } = useSignalR();

  const [showModal, setShowModal] = useState(false);
  const [formData, setFormData] = useState<RadioFormData>(initialFormData);
  const [isEditing, setIsEditing] = useState(false);

  const radios = Array.from(smartUnlinkRadios.values());

  const handleAddClick = () => {
    setFormData(initialFormData);
    setIsEditing(false);
    setShowModal(true);
  };

  const handleEditClick = (radio: { id: string; name: string; ipAddress: string; model: string; serialNumber: string; callsign?: string; enabled: boolean; version?: string }) => {
    setFormData({
      id: radio.id,
      name: radio.name,
      ipAddress: radio.ipAddress,
      model: radio.model,
      callsign: radio.callsign || '',
      enabled: radio.enabled,
      version: radio.version || '4.1.3.39644',
    });
    setIsEditing(true);
    setShowModal(true);
  };

  const handleDeleteClick = async (id: string) => {
    if (window.confirm('Are you sure you want to remove this radio?')) {
      await removeSmartUnlinkRadio(id);
    }
  };

  const handleToggleEnabled = async (id: string, currentEnabled: boolean) => {
    await setSmartUnlinkRadioEnabled(id, !currentEnabled);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();

    const dto: SmartUnlinkRadioDto = {
      id: formData.id,
      name: formData.name,
      ipAddress: formData.ipAddress,
      model: formData.model,
      serialNumber: '0000-0000-0000-0000',
      callsign: formData.callsign || undefined,
      enabled: formData.enabled,
      version: formData.version,
    };

    if (isEditing) {
      await updateSmartUnlinkRadio(dto);
    } else {
      await addSmartUnlinkRadio(dto);
    }

    setShowModal(false);
    setFormData(initialFormData);
  };

  const validateIp = (ip: string): boolean => {
    const ipRegex = /^(\d{1,3}\.){3}\d{1,3}$/;
    if (!ipRegex.test(ip)) return false;
    const parts = ip.split('.').map(Number);
    return parts.every(p => p >= 0 && p <= 255);
  };

  const isFormValid = formData.name.trim() !== '' &&
    validateIp(formData.ipAddress) &&
    formData.model !== '';

  return (
    <GlassPanel
      title="SmartUnlink"
      icon={<Radio className="w-5 h-5" />}
      actions={
        <button
          onClick={handleAddClick}
          className="flex items-center gap-1.5 px-2 py-1 text-xs bg-accent-primary/20 text-accent-primary rounded hover:bg-accent-primary/30 transition-colors"
        >
          <Plus className="w-3.5 h-3.5" />
          Add Radio
        </button>
      }
    >
      <div className="p-4 space-y-3">
        {radios.length === 0 ? (
          <div className="flex flex-col items-center justify-center py-8 text-dark-300">
            <WifiOff className="w-12 h-12 mb-4 opacity-50" />
            <p className="font-ui text-center">No radios configured</p>
            <p className="text-sm text-dark-300 mt-2 text-center">
              Add a FlexRadio to broadcast discovery packets
            </p>
          </div>
        ) : (
          radios.map((radio) => (
            <RadioCard
              key={radio.id}
              radio={radio}
              onEdit={() => handleEditClick(radio)}
              onDelete={() => handleDeleteClick(radio.id)}
              onToggleEnabled={() => handleToggleEnabled(radio.id, radio.enabled)}
            />
          ))
        )}
      </div>

      {/* Add/Edit Modal */}
      {showModal && (
        <div className="fixed inset-0 bg-dark-900/80 backdrop-blur-sm flex items-center justify-center z-50">
          <div className="glass-panel w-[400px] animate-fade-in">
            <div className="flex items-center justify-between px-4 py-3 border-b border-glass-100">
              <div className="flex items-center gap-2">
                <Radio className="w-5 h-5 text-accent-primary" />
                <h3 className="font-ui font-semibold">
                  {isEditing ? 'Edit FlexRadio' : 'Add FlexRadio'}
                </h3>
              </div>
              <button
                onClick={() => setShowModal(false)}
                className="p-1 hover:bg-dark-600 rounded transition-colors"
              >
                <X className="w-4 h-4" />
              </button>
            </div>

            <form onSubmit={handleSubmit} className="p-4 space-y-4">
              {/* Radio Name */}
              <div>
                <label className="block text-xs font-ui text-dark-300 mb-1">
                  Radio Name <span className="text-accent-danger">*</span>
                </label>
                <input
                  type="text"
                  value={formData.name}
                  onChange={(e) => setFormData({ ...formData, name: e.target.value })}
                  className="w-full bg-dark-700 border border-glass-100 rounded px-3 py-2 text-sm font-mono focus:outline-none focus:border-accent-primary"
                  placeholder="My Flex 6600"
                />
              </div>

              {/* IP Address */}
              <div>
                <label className="block text-xs font-ui text-dark-300 mb-1">
                  IP Address <span className="text-accent-danger">*</span>
                </label>
                <input
                  type="text"
                  value={formData.ipAddress}
                  onChange={(e) => setFormData({ ...formData, ipAddress: e.target.value })}
                  className={`w-full bg-dark-700 border rounded px-3 py-2 text-sm font-mono focus:outline-none ${
                    formData.ipAddress && !validateIp(formData.ipAddress)
                      ? 'border-accent-danger focus:border-accent-danger'
                      : 'border-glass-100 focus:border-accent-primary'
                  }`}
                  placeholder="192.168.1.100"
                />
                {formData.ipAddress && !validateIp(formData.ipAddress) && (
                  <p className="text-xs text-accent-danger mt-1">Invalid IP address format</p>
                )}
              </div>

              {/* Model */}
              <div>
                <label className="block text-xs font-ui text-dark-300 mb-1">
                  Model <span className="text-accent-danger">*</span>
                </label>
                <select
                  value={formData.model}
                  onChange={(e) => setFormData({ ...formData, model: e.target.value })}
                  className="w-full bg-dark-700 border border-glass-100 rounded px-3 py-2 text-sm font-mono focus:outline-none focus:border-accent-primary"
                >
                  {FLEX_RADIO_MODELS.map((model) => (
                    <option key={model} value={model}>
                      {model}
                    </option>
                  ))}
                </select>
              </div>

              {/* Callsign */}
              <div>
                <label className="block text-xs font-ui text-dark-300 mb-1">
                  Callsign <span className="text-dark-300">(optional)</span>
                </label>
                <input
                  type="text"
                  value={formData.callsign}
                  onChange={(e) => setFormData({ ...formData, callsign: e.target.value.toUpperCase() })}
                  className="w-full bg-dark-700 border border-glass-100 rounded px-3 py-2 text-sm font-mono focus:outline-none focus:border-accent-primary uppercase"
                  placeholder="W1ABC"
                />
              </div>

              {/* Version */}
              <div>
                <label className="block text-xs font-ui text-dark-300 mb-1">
                  Firmware Version <span className="text-dark-300">(optional)</span>
                </label>
                <input
                  type="text"
                  value={formData.version}
                  onChange={(e) => setFormData({ ...formData, version: e.target.value })}
                  className="w-full bg-dark-700 border border-glass-100 rounded px-3 py-2 text-sm font-mono focus:outline-none focus:border-accent-primary"
                  placeholder="4.1.3.39644"
                />
                <p className="text-xs text-dark-300 mt-1">
                  SmartSDR version to advertise (must match your SmartSDR app version)
                </p>
              </div>

              {/* Buttons */}
              <div className="flex justify-end gap-2 pt-2">
                <button
                  type="button"
                  onClick={() => setShowModal(false)}
                  className="px-4 py-2 text-sm text-dark-300 hover:text-white transition-colors"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={!isFormValid}
                  className="px-4 py-2 text-sm bg-accent-primary text-white rounded hover:bg-accent-primary/80 transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
                >
                  {isEditing ? 'Save Changes' : 'Add Radio'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </GlassPanel>
  );
}

interface RadioCardProps {
  radio: {
    id: string;
    name: string;
    ipAddress: string;
    model: string;
    serialNumber: string;
    callsign?: string;
    enabled: boolean;
    version?: string;
  };
  onEdit: () => void;
  onDelete: () => void;
  onToggleEnabled: () => void;
}

function RadioCard({ radio, onEdit, onDelete, onToggleEnabled }: RadioCardProps) {
  return (
    <div className={`
      bg-dark-700/50 rounded-lg p-3 border transition-colors
      ${radio.enabled ? 'border-accent-success/50' : 'border-glass-100'}
    `}>
      {/* Header */}
      <div className="flex items-start justify-between mb-2">
        <div>
          <div className="font-mono font-medium text-dark-200">{radio.name}</div>
          <div className="text-xs font-mono text-dark-300">{radio.model}</div>
        </div>
        <div className="flex items-center gap-1">
          <button
            onClick={onEdit}
            className="p-1.5 hover:bg-dark-600 rounded transition-colors text-dark-300 hover:text-white"
            title="Edit"
          >
            <Pencil className="w-3.5 h-3.5" />
          </button>
          <button
            onClick={onDelete}
            className="p-1.5 hover:bg-dark-600 rounded transition-colors text-dark-300 hover:text-accent-danger"
            title="Delete"
          >
            <Trash2 className="w-3.5 h-3.5" />
          </button>
        </div>
      </div>

      {/* Info */}
      <div className="text-sm text-dark-300 space-y-1 mb-3">
        <div className="flex items-center gap-2">
          <Wifi className="w-3.5 h-3.5" />
          <span className="font-mono">{radio.ipAddress}</span>
          {radio.callsign && (
            <>
              <span className="text-dark-300">|</span>
              <span className="font-mono">{radio.callsign}</span>
            </>
          )}
        </div>
      </div>

      {/* Footer */}
      <div className="flex items-center justify-between pt-2 border-t border-glass-100">
        <div className="flex items-center gap-2">
          {radio.enabled ? (
            <span className="flex items-center gap-1.5 text-xs font-mono text-accent-success">
              <span className="w-2 h-2 rounded-full bg-accent-success animate-pulse" />
              Broadcasting
            </span>
          ) : (
            <span className="flex items-center gap-1.5 text-xs font-mono text-dark-300">
              <span className="w-2 h-2 rounded-full bg-dark-600" />
              Idle
            </span>
          )}
        </div>

        <button
          onClick={onToggleEnabled}
          className={`
            flex items-center gap-1.5 px-2 py-1 text-xs font-mono rounded transition-all
            ${radio.enabled
              ? 'bg-accent-success/20 text-accent-success hover:bg-accent-success/30'
              : 'bg-dark-600 text-dark-300 hover:bg-dark-500'
            }
          `}
        >
          {radio.enabled ? (
            <>
              <Power className="w-3.5 h-3.5" />
              ON
            </>
          ) : (
            <>
              <PowerOff className="w-3.5 h-3.5" />
              OFF
            </>
          )}
        </button>
      </div>
    </div>
  );
}
