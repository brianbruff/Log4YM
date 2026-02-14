import { useState, useEffect } from 'react';
import { Loader2 } from 'lucide-react';
import { useAppStore } from '../store/appStore';
import { APP_VERSION } from '../version';

export function SplashScreen() {
  const [visible, setVisible] = useState(true);
  const [fadeOut, setFadeOut] = useState(false);
  const connectionState = useAppStore((s) => s.connectionState);

  useEffect(() => {
    if (connectionState === 'connected') {
      setFadeOut(true);
    }
  }, [connectionState]);

  // Fallback timeout - dismiss after 8 seconds regardless
  useEffect(() => {
    const timer = setTimeout(() => {
      setFadeOut(true);
    }, 8000);
    return () => clearTimeout(timer);
  }, []);

  // Remove from DOM after fade-out animation completes
  useEffect(() => {
    if (fadeOut) {
      const timer = setTimeout(() => setVisible(false), 500);
      return () => clearTimeout(timer);
    }
  }, [fadeOut]);

  if (!visible) return null;

  return (
    <div
      className={`fixed inset-0 z-50 flex flex-col items-center justify-center bg-dark-900 transition-opacity duration-500 ${
        fadeOut ? 'opacity-0' : 'opacity-100'
      }`}
    >
      <img
        src="./splash.webp"
        alt="Log4YM"
        className="max-w-[480px] w-full px-8"
      />

      <div className="mt-6 flex flex-col items-center gap-4">
        <p className="text-sm font-mono text-dark-300">
          v{APP_VERSION}
        </p>
        <Loader2 className="w-5 h-5 text-dark-300 animate-spin" />
      </div>
    </div>
  );
}
