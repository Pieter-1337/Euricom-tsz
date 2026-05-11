import { describe, it, expect } from 'vite-plus/test';

describe('sample', () => {
  it('should add two numbers', () => {
    expect(1 + 1).toBe(2);
  });

  it('should concatenate strings', () => {
    expect('hello' + ' ' + 'world').toBe('hello world');
  });
});
