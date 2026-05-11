import { defineConfig } from 'vite-plus';
import { fileURLToPath, URL } from 'node:url';

export default defineConfig({
  lint: {
    ignorePatterns: ['dist/**', 'node_modules/**', 'packages/api/**'],
  },
  fmt: {
    singleQuote: true,
    printWidth: 120,
  },
  resolve: {
    alias: {
      '@tests': fileURLToPath(new URL('./packages/web/tests', import.meta.url)),
    },
  },
  test: {
    environment: 'node',
    setupFiles: ['./packages/web/tests/setup.ts'],
  },
});
