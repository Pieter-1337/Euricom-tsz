import { defineConfig } from 'vite-plus';
import { devtools } from '@tanstack/devtools-vite';

import { tanstackStart } from '@tanstack/react-start/plugin/vite';

import viteReact from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';

const config = defineConfig({
  lint: {
    ignorePatterns: [],
  },
  fmt: {
    singleQuote: true,
    printWidth: 120,
  },
  resolve: {
    tsconfigPaths: true,
  },
  test: {
    setupFiles: ['./tests/setup.ts'],
  },
  plugins: [devtools(), tailwindcss(), tanstackStart(), viteReact()],
});

export default config;
