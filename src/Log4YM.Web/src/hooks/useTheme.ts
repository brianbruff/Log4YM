import { useEffect } from 'react';
import { useSettingsStore } from '../store/settingsStore';

/**
 * Manages theme application based on user settings.
 * Applies 'dark' class to <html> for dark mode, removes it for light mode.
 * Applies 'dark amber' classes for amber theme variant.
 */
export function useTheme() {
  const theme = useSettingsStore((s) => s.settings.appearance.theme);

  useEffect(() => {
    const html = document.documentElement;
    const meta = document.querySelector('meta[name="theme-color"]');

    // Clean all theme classes
    html.classList.remove('dark', 'amber');

    if (theme === 'dark' || theme === 'amber') {
      html.classList.add('dark');
    }
    if (theme === 'amber') {
      html.classList.add('amber');
    }

    if (meta) {
      const colors: Record<string, string> = {
        dark: '#0a0e14',
        light: '#faf8f3',
        amber: '#080604',
      };
      meta.setAttribute('content', colors[theme]);
    }
  }, [theme]);
}
