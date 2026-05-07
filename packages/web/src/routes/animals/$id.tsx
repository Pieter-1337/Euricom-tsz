import { createFileRoute, useRouter } from '@tanstack/react-router';
import { createServerFn } from '@tanstack/react-start';
import { useState, type FormEvent } from 'react';
import { z } from 'zod';
import { getAnimalById, updateAnimal, type AnimalDTO } from '#/api/animals';

const animalIdSchema = z.number().int().positive();

const updateAnimalSchema = z.object({
  name: z.string().min(1, 'Name is required'),
  species: z.string().min(1, 'Species is required'),
  age: z.number().int().nonnegative().optional(),
});

const saveAnimalInputSchema = z.object({
  id: animalIdSchema,
  animal: updateAnimalSchema,
});

const fetchAnimalById = createServerFn({ method: 'GET' })
  .inputValidator(animalIdSchema)
  .handler(async ({ data: id }) => {
    const { data } = await getAnimalById(id);
    return data as AnimalDTO;
  });

const saveAnimal = createServerFn({ method: 'POST' })
  .inputValidator(saveAnimalInputSchema)
  .handler(async ({ data }) => {
    await updateAnimal(data.id, data.animal);
  });

export const Route = createFileRoute('/animals/$id')({
  loader: ({ params }) => fetchAnimalById({ data: Number(params.id) }),
  // loader: async ({ params }) => {
  //   const { data } = await getAnimalById(Number(params.id));
  //   return data as AnimalDTO;
  // },
  component: AnimalDetail,
});

function AnimalDetail() {
  const animal = Route.useLoaderData();
  const router = useRouter();

  const [name, setName] = useState(String(animal.name ?? ''));
  const [species, setSpecies] = useState(String(animal.species ?? ''));
  const [age, setAge] = useState(String(animal.age ?? ''));
  const [saving, setSaving] = useState(false);

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setSaving(true);
    await saveAnimal({
      data: {
        id: Number(animal.id),
        animal: {
          name,
          species,
          age: age === '' ? undefined : Number(age),
        },
      },
    });
    await router.invalidate();
    setSaving(false);
  };

  return (
    <main>
      <h1 className="text-2xl font-bold">{animal.name}</h1>
      <form
        onSubmit={handleSubmit}
        className="mt-4 grid grid-cols-[auto_1fr] gap-x-6 gap-y-2 text-sm"
      >
        <label className="font-semibold self-center" htmlFor="id">
          ID
        </label>
        <input
          id="id"
          value={String(animal.id ?? '')}
          disabled
          className="rounded border px-2 py-1 bg-gray-100"
        />

        <label className="font-semibold self-center" htmlFor="name">
          Name
        </label>
        <input
          id="name"
          value={name}
          onChange={(e) => setName(e.target.value)}
          required
          className="rounded border px-2 py-1"
        />

        <label className="font-semibold self-center" htmlFor="species">
          Species
        </label>
        <input
          id="species"
          value={species}
          onChange={(e) => setSpecies(e.target.value)}
          required
          className="rounded border px-2 py-1"
        />

        <label className="font-semibold self-center" htmlFor="age">
          Age
        </label>
        <input
          id="age"
          type="number"
          value={age}
          onChange={(e) => setAge(e.target.value)}
          className="rounded border px-2 py-1"
        />

        <div className="col-span-2 mt-2">
          <button
            type="submit"
            disabled={saving}
            className="rounded bg-black px-4 py-2 text-white disabled:opacity-50"
          >
            {saving ? 'Saving…' : 'Save'}
          </button>
        </div>
      </form>
    </main>
  );
}
