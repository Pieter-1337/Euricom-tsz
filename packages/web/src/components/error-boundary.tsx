import { useEffect } from 'react';
import { useRouter, type ErrorComponentProps } from '@tanstack/react-router';
import { Button } from '#/components/ui/button';

function formatError(error: unknown): {
  name: string;
  message: string;
  stack?: string;
  raw: unknown;
} {
  if (error instanceof Error) {
    return { name: error.name, message: error.message, stack: error.stack, raw: error };
  }
  if (typeof error === 'string') {
    return { name: 'Error', message: error, raw: error };
  }
  try {
    return { name: 'Error', message: JSON.stringify(error), raw: error };
  } catch {
    return { name: 'Error', message: String(error), raw: error };
  }
}

export function ErrorBoundary({ error, reset }: ErrorComponentProps) {
  const router = useRouter();
  const info = formatError(error);

  useEffect(() => {
    console.group(`[ErrorBoundary] ${info.name}: ${info.message}`);
    console.error(info.raw);
    if (info.stack) console.error(info.stack);
    console.groupEnd();
  }, [info]);

  return (
    <main className="rounded-md border border-destructive/30 bg-destructive/5 p-6">
      <h1 className="text-2xl font-bold text-destructive">Something went wrong</h1>
      <p className="mt-2 text-sm text-muted-foreground">
        <span className="font-mono">{info.name}:</span> {info.message}
      </p>
      {import.meta.env.DEV && info.stack ? (
        <pre className="mt-4 max-h-64 overflow-auto rounded bg-muted p-3 text-xs">{info.stack}</pre>
      ) : null}
      <div className="mt-4 flex gap-2">
        <Button
          onClick={() => {
            reset();
            router.invalidate();
          }}
        >
          Try again
        </Button>
        <Button variant="outline" onClick={() => router.navigate({ to: '/' })}>
          Go home
        </Button>
      </div>
    </main>
  );
}
