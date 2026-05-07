import { createFileRoute, useRouter } from '@tanstack/react-router';
import { createServerFn } from '@tanstack/react-start';
import { useState, type FormEvent } from 'react';
import { z } from 'zod';
import { getAnimalById, updateAnimal, type AnimalDTO } from '#/api/animals';
import { Button } from '#/components/ui/button';
import { Input } from '#/components/ui/input';
import { Label } from '#/components/ui/label';

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
      <form onSubmit={handleSubmit} className="mt-4 grid max-w-md gap-4">
        <div className="grid gap-2">
          <Label htmlFor="id">ID</Label>
          <Input id="id" value={String(animal.id ?? '')} disabled />
        </div>

        <div className="grid gap-2">
          <Label htmlFor="name">Name</Label>
          <Input
            id="name"
            value={name}
            onChange={(e) => setName(e.target.value)}
            required
          />
        </div>

        <div className="grid gap-2">
          <Label htmlFor="species">Species</Label>
          <Input
            id="species"
            value={species}
            onChange={(e) => setSpecies(e.target.value)}
            required
          />
        </div>

        <div className="grid gap-2">
          <Label htmlFor="age">Age</Label>
          <Input
            id="age"
            type="number"
            value={age}
            onChange={(e) => setAge(e.target.value)}
          />
        </div>

        <div>
          <Button type="submit" disabled={saving}>
            {saving ? 'Saving…' : 'Save'}
          </Button>
        </div>
      </form>
    </main>
  );
}
