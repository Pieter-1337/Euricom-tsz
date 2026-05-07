import { describe, it, expect } from 'vitest';

describe('sample', () => {
  it('should add two numbers', () => {
    expect(1 + 1).toBe(2);
  });

  it('should concatenate strings', () => {
    expect('hello' + ' ' + 'world').toBe('hello world');
  });
});
