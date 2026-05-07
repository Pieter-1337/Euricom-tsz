import { type components, type paths } from './schema';
import createClient from 'openapi-fetch';

export type AnimalDTO = components['schemas']['Animal'];
export type CreateAnimalRequestDTO = components['schemas']['CreateAnimalRequest'];
export type UpdateAnimalRequestDTO = components['schemas']['UpdateAnimalRequest'];

const client = createClient<paths>({ baseUrl: 'http://localhost:5204' });

export const getAnimals = async () => {
  return client.GET('/api/animals');
};

export const getAnimalById = async (id: number) => {
  return client.GET(`/api/animals/{id}`, {
    params: {
      path: {
        id,
      },
    },
  });
};

export const createAnimal = async (animal: CreateAnimalRequestDTO) => {
  return client.POST('/api/animals', {
    body: animal,
  });
};

export const updateAnimal = async (id: number, animal: UpdateAnimalRequestDTO) => {
  return client.PUT('/api/animals/{id}', {
    params: {
      path: { id },
    },
    body: animal,
  });
};

export const removeAnimal = async (id: number) => {
  return client.DELETE('/api/animals/{id}', {
    params: {
      path: { id },
    },
  });
};
