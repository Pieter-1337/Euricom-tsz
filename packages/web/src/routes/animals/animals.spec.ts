import { describe, it, expect } from 'vitest';

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
