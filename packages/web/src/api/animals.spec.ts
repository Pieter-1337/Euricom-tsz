import { describe, it, expect, beforeEach, vi } from 'vite-plus/test';
import { ApiRequestError } from './client';
import { getAnimals, getAnimalById, createAnimal, updateAnimal, removeAnimal } from './animals';
import { emptyResponse, jsonResponse } from '@tests/fetch-util';

const mockFetch = vi.hoisted(() => {
  const mockFetch = vi.fn();
  globalThis.fetch = mockFetch as typeof fetch;
  return mockFetch;
});

const lastRequest = (): Request => mockFetch.mock.calls.at(-1)![0] as Request;

beforeEach(() => {
  mockFetch.mockReset();
});

describe('animals api', () => {
  it('getAnimals → GET /api/animals returns data', async () => {
    const animals = [{ id: 1, name: 'Rex', species: 'dog', age: 3 }];
    mockFetch.mockResolvedValue(jsonResponse(animals));

    await expect(getAnimals()).resolves.toEqual(animals);
    const req = lastRequest();
    expect(req.method).toBe('GET');
    expect(req.url).toBe(`${process.env.SERVER_URL}/api/animals`);
  });

  it('getAnimals → 404 throws ApiError', async () => {
    mockFetch.mockResolvedValue(emptyResponse(404));
    await expect(getAnimals()).rejects.toThrow(new ApiRequestError(404));
  });

  it('getAnimals → 500 throws ApiError', async () => {
    mockFetch.mockResolvedValue(emptyResponse(500));
    await expect(getAnimals()).rejects.toThrow(new ApiRequestError(500));
  });

  it('getAnimalById → GET /api/animals/{id} with path param', async () => {
    const id = '00000000-0000-0000-0000-000000000007';
    const animal = { id, name: 'Rex', species: 'dog', age: 3 };
    mockFetch.mockResolvedValue(jsonResponse(animal));

    await expect(getAnimalById(id)).resolves.toEqual(animal);
    expect(lastRequest().url).toBe(`${process.env.SERVER_URL}/api/animals/${id}`);
  });

  it('createAnimal → POST /api/animals with body', async () => {
    mockFetch.mockResolvedValue(emptyResponse(201));
    const body = { name: 'Rex', species: 'dog', age: 3 };

    await createAnimal(body);
    const req = lastRequest();
    expect(req.method).toBe('POST');
    expect(req.url).toBe(`${process.env.SERVER_URL}/api/animals`);
    await expect(req.json()).resolves.toEqual(body);
  });

  it('updateAnimal → PUT /api/animals/{id} with path + body', async () => {
    mockFetch.mockResolvedValue(emptyResponse(204));
    const id = '00000000-0000-0000-0000-000000000003';
    const body = { id, name: 'Rex', species: 'dog', age: 4 };

    await updateAnimal(id, body);
    const req = lastRequest();
    expect(req.method).toBe('PUT');
    expect(req.url).toBe(`${process.env.SERVER_URL}/api/animals/${id}`);
    await expect(req.json()).resolves.toEqual(body);
  });

  it('removeAnimal → DELETE /api/animals/{id}', async () => {
    mockFetch.mockResolvedValue(emptyResponse(204));
    const id = '00000000-0000-0000-0000-000000000009';

    await removeAnimal(id);
    const req = lastRequest();
    expect(req.method).toBe('DELETE');
    expect(req.url).toBe(`${process.env.SERVER_URL}/api/animals/${id}`);
  });
});

function filterAnimals(animals: Array<{ name: string; species: string }>, term: string) {
  const lower = term.toLowerCase();
  return animals.filter((a) => a.name.toLowerCase().includes(lower) || a.species.toLowerCase().includes(lower));
}

describe('filterAnimals', () => {
  const animals = [
    { name: 'Leo', species: 'Lion' },
    { name: 'Ellie', species: 'Elephant' },
    { name: 'Giraffy', species: 'Giraffe' },
  ];

  it('returns all animals when search is empty', () => {
    expect(filterAnimals(animals, '')).toHaveLength(3);
  });

  it('filters by name case-insensitively', () => {
    expect(filterAnimals(animals, 'leo')).toEqual([{ name: 'Leo', species: 'Lion' }]);
  });

  it('filters by species', () => {
    expect(filterAnimals(animals, 'eleph')).toEqual([{ name: 'Ellie', species: 'Elephant' }]);
  });
});
