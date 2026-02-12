import { useEffect } from 'react';
import { useSettingsStore } from '../store/settingsStore';

/**
 * Manages theme application based on user settings and system preference.
 * Applies 'dark' class to <html> for dark mode, removes it for light mode.
 * Listens to prefers-color-scheme media query when theme is set to 'system'.
 */
export function useTheme() {
  const theme = useSettingsStore((s) => s.settings.appearance.theme);

  useEffect(() => {
    const html = document.documentElement;
    const meta = document.querySelector('meta[name="theme-color"]');

    const applyTheme = (resolved: 'dark' | 'light') => {
      if (resolved === 'dark') {
        html.classList.add('dark');
      } else {
        html.classList.remove('dark');
      }
      if (meta) {
        meta.setAttribute('content', resolved === 'dark' ? '#0a0e14' : '#faf8f3');
      }
    };

    if (theme === 'system') {
      const mq = window.matchMedia('(prefers-color-scheme: dark)');
      const handler = (e: MediaQueryListEvent) => applyTheme(e.matches ? 'dark' : 'light');
      applyTheme(mq.matches ? 'dark' : 'light');
      mq.addEventListener('change', handler);
      return () => mq.removeEventListener('change', handler);
    }

    applyTheme(theme);
  }, [theme]);
}
