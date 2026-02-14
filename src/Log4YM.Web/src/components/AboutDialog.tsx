import { useEffect, useRef } from 'react';
import { X, Github, Bug } from 'lucide-react';
import { APP_VERSION } from '../version';

interface AboutDialogProps {
  isOpen: boolean;
  onClose: () => void;
}

export function AboutDialog({ isOpen, onClose }: AboutDialogProps) {
  const dialogRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!isOpen) return;
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') onClose();
    };
    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [isOpen, onClose]);

  if (!isOpen) return null;

  const openLink = (url: string) => {
    if (window.electronAPI && 'openExternal' in window.electronAPI) {
      (window.electronAPI as any).openExternal(url);
    } else {
      window.open(url, '_blank', 'noopener,noreferrer');
    }
  };

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 backdrop-blur-sm animate-fade-in"
      onClick={(e) => {
        if (e.target === e.currentTarget) onClose();
      }}
    >
      <div
        ref={dialogRef}
        className="relative glass-panel border border-glass-200 rounded-xl shadow-2xl p-8 max-w-sm w-full mx-4 animate-scale-in text-center"
      >
        <button
          onClick={onClose}
          className="absolute top-3 right-3 p-1 rounded-lg hover:bg-dark-600 transition-colors text-dark-300 hover:text-white"
          title="Close"
        >
          <X className="w-4 h-4" />
        </button>

        <img
          src="./logo.webp"
          alt="Log4YM Logo"
          className="w-[120px] h-auto mx-auto mb-4"
        />

        <h1 className="text-2xl font-bold text-white font-display tracking-wider">
          Log4YM
        </h1>
        <p className="text-dark-300 text-sm mt-1">
          Amateur Radio Logging Software
        </p>
        <p className="text-dark-400 text-xs font-mono mt-2">
          Version {APP_VERSION}
        </p>

        <div className="border-t border-glass-100 my-5" />

        <div className="flex items-center justify-center gap-4">
          <button
            onClick={() => openLink('https://github.com/brianbruff/Log4YM')}
            className="flex items-center gap-2 px-4 py-2 rounded-lg hover:bg-dark-600 transition-colors text-dark-200 hover:text-accent-primary text-sm"
          >
            <Github className="w-4 h-4" />
            GitHub
          </button>
          <button
            onClick={() => openLink('https://github.com/brianbruff/Log4YM/issues')}
            className="flex items-center gap-2 px-4 py-2 rounded-lg hover:bg-dark-600 transition-colors text-dark-200 hover:text-accent-primary text-sm"
          >
            <Bug className="w-4 h-4" />
            Report Issue
          </button>
        </div>

        <button
          onClick={onClose}
          className="mt-5 px-6 py-2 rounded-lg bg-dark-600 hover:bg-dark-500 text-white text-sm font-medium transition-colors"
        >
          Close
        </button>
      </div>
    </div>
  );
}
