import { Settings, Layout, Plus } from 'lucide-react';

interface HeaderProps {
  onAddPanel?: () => void;
}

export function Header({ onAddPanel }: HeaderProps) {
  return (
    <header className="h-14 bg-dark-800/90 backdrop-blur-xl border-b border-glass-100 flex items-center justify-between px-4">
      {/* Logo */}
      <div className="flex items-center gap-3">
        <div className="relative w-10 h-10">
          {/* Circle with notch and radio wave lines */}
          <svg viewBox="0 0 40 40" className="w-full h-full">
            {/* Main circle with left notch cutout */}
            <path
              d="M20 2 A18 18 0 1 1 20 38 A18 18 0 0 1 20 2 M8 14 L8 26 A12 12 0 0 0 8 14"
              fill="#f97316"
            />
            {/* Semi-circle cutout on left */}
            <circle cx="8" cy="20" r="8" fill="#1a1b23" />
            {/* Radio wave lines extending outside circle */}
            <rect x="16" y="9" width="20" height="3.5" rx="1.75" fill="#1a1b23" />
            <rect x="16" y="18.25" width="20" height="3.5" rx="1.75" fill="#1a1b23" />
            <rect x="16" y="27.5" width="20" height="3.5" rx="1.75" fill="#1a1b23" />
          </svg>
        </div>
        <div>
          <h1 className="text-xl font-bold tracking-wide">
            <span className="text-orange-500">LOG</span>
            <span className="text-orange-500">4</span>
            <span className="text-orange-500">YM</span>
          </h1>
          <p className="text-[10px] text-orange-500/80 tracking-widest uppercase -mt-0.5">Hamradio Logging</p>
        </div>
      </div>

      {/* Actions */}
      <div className="flex items-center gap-2">
        <button
          onClick={onAddPanel}
          className="glass-button flex items-center gap-2 text-sm"
        >
          <Plus className="w-4 h-4" />
          Add Panel
        </button>

        <button className="glass-button p-2" title="Layout">
          <Layout className="w-4 h-4" />
        </button>

        <button className="glass-button p-2" title="Settings">
          <Settings className="w-4 h-4" />
        </button>
      </div>
    </header>
  );
}
