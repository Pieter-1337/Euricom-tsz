import { createFileRoute } from '@tanstack/react-router';
import { createServerFn } from '@tanstack/react-start';
import { getAnimalById, type AnimalDTO } from '#/api/animals';

const fetchAnimalById = createServerFn({ method: 'GET' })
  .inputValidator((id: number) => id)
  .handler(async ({ data: id }) => {
    const { data } = await getAnimalById(id);
    return data as AnimalDTO;
  });

export const Route = createFileRoute('/animals/$id')({
  loader: ({ params }) => fetchAnimalById({ data: Number(params.id) }),
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
