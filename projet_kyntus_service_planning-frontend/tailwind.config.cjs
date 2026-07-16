/** @type {import('tailwindcss').Config} */
module.exports = {
  darkMode: 'class',
  content: ['./src/**/*.{html,ts}'],
  theme: {
    extend: {
      colors: {
        navy: {
          950: 'rgb(var(--navy-950-rgb) / <alpha-value>)',
          900: 'rgb(var(--navy-900-rgb) / <alpha-value>)',
          800: 'rgb(var(--navy-800-rgb) / <alpha-value>)',
          700: 'rgb(var(--navy-700-rgb) / <alpha-value>)',
        },
        'electric-blue': 'rgb(var(--electric-blue-rgb) / <alpha-value>)',
        'soft-blue': 'rgb(var(--soft-blue-rgb) / <alpha-value>)',
        /* Tokens sémantiques (suivent theme-light / theme-dark via CSS vars) */
        surface: {
          1: 'var(--surface-1)',
          2: 'var(--surface-2)',
          3: 'var(--surface-3)',
        },
        ink: {
          1: 'var(--ink-1)',
          2: 'var(--ink-2)',
          3: 'var(--ink-3)',
        },
        line: {
          DEFAULT: 'var(--border-1)',
          soft: 'var(--border-2)',
        },
        accent: {
          DEFAULT: 'var(--accent)',
          hover: 'var(--accent-hover)',
        },
        success: 'var(--success)',
        warning: 'var(--warning)',
        danger: 'var(--danger)',
        info: 'var(--info)',
      },
      fontFamily: {
        sans: ['Inter', 'ui-sans-serif', 'system-ui', 'sans-serif'],
        display: ['Space Grotesk', 'Inter', 'ui-sans-serif', 'system-ui', 'sans-serif'],
      },
      transitionTimingFunction: {
        'out-expo': 'var(--ease-out)',
      },
      boxShadow: {
        'elevation-1': 'var(--shadow-1)',
        'elevation-2': 'var(--shadow-2)',
        'elevation-3': 'var(--shadow-3)',
      },
    },
  },
  plugins: [],
};
