/** @type {import('tailwindcss').Config} */
export default {
  content: [
    "./index.html",
    "./src/**/*.{js,ts,jsx,tsx}",
  ],
  darkMode: 'class',
  theme: {
    extend: {
      fontFamily: {
        display: ['Orbitron', 'system-ui', 'sans-serif'],
        mono: ['JetBrains Mono', 'Consolas', 'monospace'],
        ui: ['Space Grotesk', 'system-ui', 'sans-serif'],
      },
      colors: {
        // OpenHamClock instrument panel palette
        glass: {
          50: 'rgba(255, 180, 50, 0.04)',
          100: 'rgba(255, 180, 50, 0.08)',
          200: 'rgba(255, 180, 50, 0.12)',
          300: 'rgba(255, 180, 50, 0.18)',
        },
        dark: {
          900: '#0a0e14',        // Deepest background
          850: '#0e1319',        // Between 900 and 800
          800: '#111820',        // Panel/sidebar background
          700: '#1a2332',        // Elevated surface
          600: '#243044',        // Borders, dividers
          500: '#2e3d55',        // Lighter elements
          400: '#3d5070',        // Muted interactive
          300: '#5a7090',        // Muted text
          200: '#8899aa',        // Secondary text
        },
        accent: {
          primary: '#ffb432',    // Amber — signature accent
          secondary: '#00ddff',  // Cyan — secondary accent
          success: '#00ff88',    // Green — section headers, good status
          warning: '#ffb432',    // Amber — warnings
          danger: '#ff4466',     // Red — alerts, errors
          info: '#00ddff',       // Cyan — informational
        },
        ham: {
          cw: '#ffb432',         // Amber for CW
          ssb: '#00ff88',        // Green for SSB
          ft8: '#00ddff',        // Cyan for FT8
          rtty: '#aa66ff',       // Purple for RTTY
        }
      },
      backdropBlur: {
        xs: '2px',
      },
      boxShadow: {
        'glass': '0 8px 32px 0 rgba(0, 0, 0, 0.6), inset 0 1px 0 rgba(255, 180, 50, 0.05)',
        'glass-sm': '0 4px 16px 0 rgba(0, 0, 0, 0.4)',
        'glow': '0 0 20px rgba(255, 180, 50, 0.25)',
        'glow-success': '0 0 20px rgba(0, 255, 136, 0.25)',
        'glow-cyan': '0 0 20px rgba(0, 221, 255, 0.25)',
      },
      backgroundImage: {
        'gradient-radial': 'radial-gradient(var(--tw-gradient-stops))',
        'mesh': 'none',
      },
      animation: {
        'pulse-slow': 'pulse 3s cubic-bezier(0.4, 0, 0.6, 1) infinite',
        'glow': 'glow 2s ease-in-out infinite alternate',
      },
      keyframes: {
        glow: {
          '0%': { boxShadow: '0 0 5px rgba(255, 180, 50, 0.15)' },
          '100%': { boxShadow: '0 0 20px rgba(255, 180, 50, 0.35)' },
        }
      }
    },
  },
  plugins: [],
}
