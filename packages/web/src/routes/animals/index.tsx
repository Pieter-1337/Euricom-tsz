import { createFileRoute, Link } from '@tanstack/react-router';
import { createServerFn } from '@tanstack/react-start';
import { useState } from 'react';
import { getAnimals, type AnimalDTO } from '#/api/animals';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '#/components/ui/table';
import { Input } from '#/components/ui/input';

const fetchAnimals = createServerFn({ method: 'GET' }).handler(async () => {
  const animals = await getAnimals();
  return animals ?? [];
});

export const Route = createFileRoute('/animals/')({
  loader: () => fetchAnimals(),
  component: Animals,
});

function Animals() {
  const animals = Route.useLoaderData();
  const [searchTerm, setSearchTerm] = useState('');

  const filtered = animals.filter((animal: AnimalDTO) => {
    const term = searchTerm.toLowerCase();
    return (animal.name ?? '').toLowerCase().includes(term) || (animal.species ?? '').toLowerCase().includes(term);
  });

  return (
    <main>
      <h1 className="text-2xl font-bold">Animals</h1>
      <Input
        className="mt-4 max-w-sm"
        placeholder="Search by name or species..."
        value={searchTerm}
        onChange={(e) => setSearchTerm(e.target.value)}
      />
      <Table className="mt-4">
        <TableHeader>
          <TableRow>
            <TableHead>Name</TableHead>
            <TableHead>Species</TableHead>
            <TableHead>Age</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {filtered.map((animal: AnimalDTO) => (
            <TableRow key={animal.id}>
              <TableCell>
                <Link to="/animals/$id" params={{ id: String(animal.id) }} className="hover:underline">
                  {animal.name}
                </Link>
              </TableCell>
              <TableCell>{animal.species}</TableCell>
              <TableCell>{animal.age}</TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </main>
  );
}
