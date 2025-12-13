/** @type {import('tailwindcss').Config} */
export default {
  content: [
    "./index.html",
    "./src/**/*.{js,ts,jsx,tsx}",
  ],
  darkMode: 'class',
  theme: {
    extend: {
      colors: {
        // VS Code Dark Modern inspired palette
        glass: {
          50: 'rgba(255, 255, 255, 0.04)',
          100: 'rgba(255, 255, 255, 0.06)',
          200: 'rgba(255, 255, 255, 0.08)',
          300: 'rgba(255, 255, 255, 0.12)',
        },
        dark: {
          900: '#1e1e1e',        // VS Code editor background
          800: '#252526',        // VS Code sidebar background
          700: '#2d2d2d',        // VS Code hover/selection
          600: '#3c3c3c',        // VS Code border/divider
          500: '#4d4d4d',        // Lighter elements
          400: '#5a5a5a',        // Even lighter
          300: '#6e6e6e',        // Muted text
        },
        accent: {
          primary: '#0078d4',    // VS Code blue (focus/selection)
          secondary: '#3794ff',  // Lighter blue
          success: '#4ec9b0',    // VS Code teal/cyan
          warning: '#dcdcaa',    // VS Code yellow
          danger: '#f14c4c',     // VS Code red
          info: '#9cdcfe',       // VS Code light blue
        },
        ham: {
          cw: '#dcdcaa',         // Yellow for CW
          ssb: '#4ec9b0',        // Teal for SSB
          ft8: '#569cd6',        // Blue for FT8
          rtty: '#c586c0',       // Pink/magenta for RTTY
        }
      },
      backdropBlur: {
        xs: '2px',
      },
      boxShadow: {
        'glass': '0 8px 32px 0 rgba(0, 0, 0, 0.5)',
        'glass-sm': '0 4px 16px 0 rgba(0, 0, 0, 0.35)',
        'glow': '0 0 20px rgba(0, 120, 212, 0.3)',
        'glow-success': '0 0 20px rgba(78, 201, 176, 0.3)',
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
          '0%': { boxShadow: '0 0 5px rgba(0, 120, 212, 0.2)' },
          '100%': { boxShadow: '0 0 20px rgba(0, 120, 212, 0.4)' },
        }
      }
    },
  },
  plugins: [],
}
