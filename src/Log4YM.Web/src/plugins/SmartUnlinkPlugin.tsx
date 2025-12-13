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
  serialNumber: string;
  callsign: string;
  enabled: boolean;
}

const initialFormData: RadioFormData = {
  name: '',
  ipAddress: '',
  model: 'FLEX-6600',
  serialNumber: '',
  callsign: '',
  enabled: false,
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

  const handleEditClick = (radio: RadioFormData) => {
    setFormData({
      id: radio.id,
      name: radio.name,
      ipAddress: radio.ipAddress,
      model: radio.model,
      serialNumber: radio.serialNumber,
      callsign: radio.callsign || '',
      enabled: radio.enabled,
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
      serialNumber: formData.serialNumber,
      callsign: formData.callsign || undefined,
      enabled: formData.enabled,
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
    formData.serialNumber.trim() !== '' &&
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
          <div className="flex flex-col items-center justify-center py-8 text-gray-500">
            <WifiOff className="w-12 h-12 mb-4 opacity-50" />
            <p className="text-center">No radios configured</p>
            <p className="text-sm text-gray-600 mt-2 text-center">
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
                <h3 className="font-semibold">
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
                <label className="block text-xs text-gray-400 mb-1">
                  Radio Name <span className="text-red-400">*</span>
                </label>
                <input
                  type="text"
                  value={formData.name}
                  onChange={(e) => setFormData({ ...formData, name: e.target.value })}
                  className="w-full bg-dark-700 border border-glass-100 rounded px-3 py-2 text-sm focus:outline-none focus:border-accent-primary"
                  placeholder="My Flex 6600"
                />
              </div>

              {/* IP Address */}
              <div>
                <label className="block text-xs text-gray-400 mb-1">
                  IP Address <span className="text-red-400">*</span>
                </label>
                <input
                  type="text"
                  value={formData.ipAddress}
                  onChange={(e) => setFormData({ ...formData, ipAddress: e.target.value })}
                  className={`w-full bg-dark-700 border rounded px-3 py-2 text-sm focus:outline-none ${
                    formData.ipAddress && !validateIp(formData.ipAddress)
                      ? 'border-red-500 focus:border-red-500'
                      : 'border-glass-100 focus:border-accent-primary'
                  }`}
                  placeholder="192.168.1.100"
                />
                {formData.ipAddress && !validateIp(formData.ipAddress) && (
                  <p className="text-xs text-red-400 mt-1">Invalid IP address format</p>
                )}
              </div>

              {/* Model */}
              <div>
                <label className="block text-xs text-gray-400 mb-1">
                  Model <span className="text-red-400">*</span>
                </label>
                <select
                  value={formData.model}
                  onChange={(e) => setFormData({ ...formData, model: e.target.value })}
                  className="w-full bg-dark-700 border border-glass-100 rounded px-3 py-2 text-sm focus:outline-none focus:border-accent-primary"
                >
                  {FLEX_RADIO_MODELS.map((model) => (
                    <option key={model} value={model}>
                      {model}
                    </option>
                  ))}
                </select>
              </div>

              {/* Serial Number */}
              <div>
                <label className="block text-xs text-gray-400 mb-1">
                  Serial Number <span className="text-red-400">*</span>
                </label>
                <input
                  type="text"
                  value={formData.serialNumber}
                  onChange={(e) => setFormData({ ...formData, serialNumber: e.target.value })}
                  className="w-full bg-dark-700 border border-glass-100 rounded px-3 py-2 text-sm focus:outline-none focus:border-accent-primary"
                  placeholder="1234-5678-9ABC-DEF0"
                />
                <p className="text-xs text-gray-500 mt-1">
                  Find this in SmartSDR under Radio &gt; Info
                </p>
              </div>

              {/* Callsign */}
              <div>
                <label className="block text-xs text-gray-400 mb-1">
                  Callsign <span className="text-gray-500">(optional)</span>
                </label>
                <input
                  type="text"
                  value={formData.callsign}
                  onChange={(e) => setFormData({ ...formData, callsign: e.target.value.toUpperCase() })}
                  className="w-full bg-dark-700 border border-glass-100 rounded px-3 py-2 text-sm focus:outline-none focus:border-accent-primary uppercase"
                  placeholder="W1ABC"
                />
              </div>

              {/* Buttons */}
              <div className="flex justify-end gap-2 pt-2">
                <button
                  type="button"
                  onClick={() => setShowModal(false)}
                  className="px-4 py-2 text-sm text-gray-400 hover:text-white transition-colors"
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
          <div className="font-medium text-gray-200">{radio.name}</div>
          <div className="text-xs text-gray-500">{radio.model}</div>
        </div>
        <div className="flex items-center gap-1">
          <button
            onClick={onEdit}
            className="p-1.5 hover:bg-dark-600 rounded transition-colors text-gray-400 hover:text-white"
            title="Edit"
          >
            <Pencil className="w-3.5 h-3.5" />
          </button>
          <button
            onClick={onDelete}
            className="p-1.5 hover:bg-dark-600 rounded transition-colors text-gray-400 hover:text-red-400"
            title="Delete"
          >
            <Trash2 className="w-3.5 h-3.5" />
          </button>
        </div>
      </div>

      {/* Info */}
      <div className="text-sm text-gray-400 space-y-1 mb-3">
        <div className="flex items-center gap-2">
          <Wifi className="w-3.5 h-3.5" />
          <span className="font-mono">{radio.ipAddress}</span>
          {radio.callsign && (
            <>
              <span className="text-gray-600">|</span>
              <span>{radio.callsign}</span>
            </>
          )}
        </div>
        <div className="text-xs text-gray-500 font-mono">
          SN: {radio.serialNumber}
        </div>
      </div>

      {/* Footer */}
      <div className="flex items-center justify-between pt-2 border-t border-glass-100">
        <div className="flex items-center gap-2">
          {radio.enabled ? (
            <span className="flex items-center gap-1.5 text-xs text-accent-success">
              <span className="w-2 h-2 rounded-full bg-accent-success animate-pulse" />
              Broadcasting
            </span>
          ) : (
            <span className="flex items-center gap-1.5 text-xs text-gray-500">
              <span className="w-2 h-2 rounded-full bg-gray-600" />
              Idle
            </span>
          )}
        </div>

        <button
          onClick={onToggleEnabled}
          className={`
            flex items-center gap-1.5 px-2 py-1 text-xs rounded transition-all
            ${radio.enabled
              ? 'bg-green-500/20 text-green-400 hover:bg-green-500/30'
              : 'bg-dark-600 text-gray-400 hover:bg-dark-500'
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
