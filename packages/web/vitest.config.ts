import { defineConfig } from 'vitest/config';
import viteReact from '@vitejs/plugin-react';
import tsconfigPaths from 'vite-tsconfig-paths';

export default defineConfig({
  plugins: [viteReact(), tsconfigPaths()],
  test: {
    environment: 'node',
  },
});
