import { type components } from './schema';
import { client } from './client';

export type AnimalDTO = components['schemas']['Animal'];
export type CreateAnimalRequestDTO = components['schemas']['CreateAnimalRequest'];
export type UpdateAnimalRequestDTO = components['schemas']['UpdateAnimalRequest'];
export type Animal = AnimalDTO;

/*
// Example how to map a DTO to a domain object

export type Product = Omit<ProductDTO, 'createdAt'> & {
  createdAt: Date;
};

const toProduct = (dto: ProductDTO): Product => {
  return {
    ...animal,
    createdAt: new Date(dto.createdAt),
  };
};

export const getProducts = async (): Promise<Product[]> => {
  const resp = await client.GET('/api/products');
  return resp.data.map(toProduct);
};

export const getProductById = async (id: number): Promise<Product[]> => {
  const resp = await client.GET('/api/products/{id}', { params: { path: { id } } });
  return toProduct(resp.data;
};
*/

export const getAnimals = async (): Promise<Animal[]> => {
  const resp = await client.GET('/api/animals');
  return resp.data!;
};

export const getAnimalById = async (id: number): Promise<Animal> => {
  const resp = await client.GET('/api/animals/{id}', { params: { path: { id } } });
  return resp.data!;
};

export const createAnimal = async (animal: CreateAnimalRequestDTO): Promise<void> => {
  const resp = await client.POST('/api/animals', { body: animal });
  return resp.data!;
};

export const updateAnimal = async (id: number, animal: UpdateAnimalRequestDTO): Promise<void> => {
  const resp = await client.PUT('/api/animals/{id}', { params: { path: { id } }, body: animal });
  return resp.data!;
};

export const removeAnimal = async (id: number): Promise<void> => {
  const resp = await client.DELETE('/api/animals/{id}', { params: { path: { id } } });
  return resp.data!;
};
