import { createFileRoute } from '@tanstack/react-router';

export const Route = createFileRoute('/')({ component: Home });

function Home() {
  return (
    <main>
      <h1 className="text-2xl font-bold">Welcome</h1>
      <p className="mt-2 text-gray-600">A simple TanStack Start app.</p>
    </main>
  );
}
