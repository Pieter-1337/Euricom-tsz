import { describe, it, expect, beforeEach, vi } from 'vite-plus/test';
import { ApiRequestError } from './client';
import { getAnimals, getAnimalById, createAnimal, updateAnimal, removeAnimal } from './animals';
import { emptyResponse, jsonResponse } from 'tests/fetch-util';

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
    const animal = { id: 7, name: 'Rex', species: 'dog', age: 3 };
    mockFetch.mockResolvedValue(jsonResponse(animal));

    await expect(getAnimalById(7)).resolves.toEqual(animal);
    expect(lastRequest().url).toBe(`${process.env.SERVER_URL}/api/animals/7`);
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
    const body = { name: 'Rex', species: 'dog', age: 4 };

    await updateAnimal(3, body);
    const req = lastRequest();
    expect(req.method).toBe('PUT');
    expect(req.url).toBe(`${process.env.SERVER_URL}/api/animals/3`);
    await expect(req.json()).resolves.toEqual(body);
  });

  it('removeAnimal → DELETE /api/animals/{id}', async () => {
    mockFetch.mockResolvedValue(emptyResponse(204));

    await removeAnimal(9);
    const req = lastRequest();
    expect(req.method).toBe('DELETE');
    expect(req.url).toBe(`${process.env.SERVER_URL}/api/animals/9`);
  });
});
