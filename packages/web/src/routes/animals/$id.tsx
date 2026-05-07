import { createFileRoute } from '@tanstack/react-router';
import { getAnimalById, type AnimalDTO } from '#/api/animals';

export const Route = createFileRoute('/animals/$id')({
  loader: async ({ params }) => {
    const { data } = await getAnimalById(Number(params.id));
    return data as AnimalDTO;
  },
  component: AnimalDetail,
});

function AnimalDetail() {
  const animal = Route.useLoaderData();

  return (
    <main>
      <h1 className="text-2xl font-bold">{animal.name}</h1>
      <dl className="mt-4 grid grid-cols-[auto_1fr] gap-x-6 gap-y-2 text-sm">
        <dt className="font-semibold">ID</dt>
        <dd>{animal.id}</dd>
        <dt className="font-semibold">Species</dt>
        <dd>{animal.species}</dd>
        <dt className="font-semibold">Age</dt>
        <dd>{animal.age}</dd>
      </dl>
    </main>
  );
}
