import { defineConfig } from 'vite-plus';

export default defineConfig({
  lint: {
    ignorePatterns: ['dist/**', 'node_modules/**', 'packages/api/**'],
  },
  fmt: {
    singleQuote: true,
    printWidth: 120,
  },
});
