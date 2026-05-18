declare module 'bun:sqlite' {
  export class Database {
    constructor(filename?: string, options?: { readonly?: boolean; create?: boolean });
    exec(sql: string): void;
    prepare<T = unknown>(
      sql: string,
    ): {
      all(...params: unknown[]): T[];
      get(...params: unknown[]): T | null;
      run(...params: unknown[]): { changes: number; lastInsertRowid: number | bigint };
    };
    query<T = unknown>(
      sql: string,
    ): {
      all(...params: unknown[]): T[];
      get(...params: unknown[]): T | null;
      run(...params: unknown[]): { changes: number; lastInsertRowid: number | bigint };
    };
    close(): void;
    transaction<T extends (...args: unknown[]) => unknown>(fn: T): T;
  }
}
