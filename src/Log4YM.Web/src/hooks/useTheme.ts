import { useEffect } from 'react';
import { useSettingsStore } from '../store/settingsStore';

/**
 * Manages theme application based on user settings.
 * Applies 'dark' class to <html> for dark mode, removes it for light mode.
 */
export function useTheme() {
  const theme = useSettingsStore((s) => s.settings.appearance.theme);

  useEffect(() => {
    const html = document.documentElement;

    // Clean all theme classes
    html.classList.remove('dark');

    if (theme === 'dark') {
      html.classList.add('dark');
    }

    const meta = document.querySelector('meta[name="theme-color"]');
    if (meta) {
      const colors: Record<string, string> = {
        dark: '#1B1B1F',
        light: '#eceae6',
      };
      meta.setAttribute('content', colors[theme]);
    }
  }, [theme]);
}
