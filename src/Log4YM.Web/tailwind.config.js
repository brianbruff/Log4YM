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
        // Surface/background colors — themed via CSS custom properties
        dark: {
          900: 'rgb(var(--surface-900) / <alpha-value>)',
          850: 'rgb(var(--surface-850) / <alpha-value>)',
          800: 'rgb(var(--surface-800) / <alpha-value>)',
          700: 'rgb(var(--surface-700) / <alpha-value>)',
          600: 'rgb(var(--surface-600) / <alpha-value>)',
          500: 'rgb(var(--surface-500) / <alpha-value>)',
          400: 'rgb(var(--surface-400) / <alpha-value>)',
          300: 'rgb(var(--surface-300) / <alpha-value>)',
          200: 'rgb(var(--surface-200) / <alpha-value>)',
        },
        // Glass/overlay tints — derived from accent-primary
        glass: {
          50: 'rgb(var(--accent-primary) / 0.04)',
          100: 'rgb(var(--accent-primary) / 0.08)',
          200: 'rgb(var(--accent-primary) / 0.12)',
          300: 'rgb(var(--accent-primary) / 0.18)',
        },
        // Accent colors — themed
        accent: {
          primary: 'rgb(var(--accent-primary) / <alpha-value>)',
          secondary: 'rgb(var(--accent-secondary) / <alpha-value>)',
          success: 'rgb(var(--accent-success) / <alpha-value>)',
          warning: 'rgb(var(--accent-warning) / <alpha-value>)',
          danger: 'rgb(var(--accent-danger) / <alpha-value>)',
          info: 'rgb(var(--accent-info) / <alpha-value>)',
        },
        // Ham radio mode colors — themed
        ham: {
          cw: 'rgb(var(--ham-cw) / <alpha-value>)',
          ssb: 'rgb(var(--ham-ssb) / <alpha-value>)',
          ft8: 'rgb(var(--ham-ft8) / <alpha-value>)',
          rtty: 'rgb(var(--ham-rtty) / <alpha-value>)',
        },
        // Gray scale — semantically inverted for light/dark themes
        gray: {
          50: 'rgb(var(--gray-50) / <alpha-value>)',
          100: 'rgb(var(--gray-100) / <alpha-value>)',
          200: 'rgb(var(--gray-200) / <alpha-value>)',
          300: 'rgb(var(--gray-300) / <alpha-value>)',
          400: 'rgb(var(--gray-400) / <alpha-value>)',
          500: 'rgb(var(--gray-500) / <alpha-value>)',
          600: 'rgb(var(--gray-600) / <alpha-value>)',
          700: 'rgb(var(--gray-700) / <alpha-value>)',
          800: 'rgb(var(--gray-800) / <alpha-value>)',
          900: 'rgb(var(--gray-900) / <alpha-value>)',
        },
      },
      backdropBlur: {
        xs: '2px',
      },
      boxShadow: {
        'glass': 'var(--shadow-glass)',
        'glass-sm': 'var(--shadow-glass-sm)',
        'glow': 'var(--shadow-glow)',
        'glow-success': 'var(--shadow-glow-success)',
        'glow-cyan': 'var(--shadow-glow-cyan)',
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
          '0%': { boxShadow: '0 0 5px rgb(var(--accent-primary) / 0.15)' },
          '100%': { boxShadow: '0 0 20px rgb(var(--accent-primary) / 0.35)' },
        }
      }
    },
  },
  plugins: [],
}
