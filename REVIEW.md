# REVIEW.md

## 8 may

- Je CLAUDE.md staat in je .claude folder, waardoor deze niet meegenomen wordt door Claude Code. Plaats deze in de root.
- Maak naast de root CLAUDE.md meer gespecializeerde CLAUDE.md in je packages folders.
- Hier eerste kleine verbetering:

```
# Important rules to follow

- **Always use Bun for Node tooling.** Never use npm or pnpm unless strictly necessary and explicitly requested.
- **Use Bash for scripts and shell commands.** This repo is developed on WSL; never use PowerShell or assume Windows-style paths.
```

- voor het age (number) field kan je beter volgende aanpassing doen.
  instruct claude om dit volgende keer beter te doen.

```js
defaultValues: {
    name: String(animal?.name ?? ''),
    species: String(animal?.species ?? ''),
    age: Number(animal?.age ?? 0),     // <-------
},
validators: {
    onChange: z.object({
    name: z.string().min(1, 'Name is required'),
    species: z.string().min(1, 'Species is required'),
    age: z.number().int().nonnegative(),    // <-------
    }),
}
```

en 

```js
onSubmit: async ({ value }) => {
    await saveAnimal({
        data: {
            id: Number(animal?.id),
            animal: {
            name: value.name,
            species: value.species,
            age: value.age,                     // <-------
            },
        },
    });
    await router.invalidate();
},
```

en 

```tsx
<Input
    id={field.name}
    name={field.name}
    type="number"
    value={field.state.value}
    onBlur={field.handleBlur}
    onChange={(e) => field.handleChange(e.target.value === '' ? 0 : e.target.valueAsNumber)}  // <-------
/>
```

- Enable more strict openapi specs, better for the TS types generation.
  and make sure all field are not optional (default for C#)

```
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.NumberHandling = JsonNumberHandling.Strict;
});

builder.Services.AddOpenApi(options =>
{
    options.AddSchemaTransformer((schema, _, _) =>
    {
        if (schema.Properties is { Count: > 0 })
        {
            schema.Required ??= new HashSet<string>();
            foreach (var (name, property) in schema.Properties)
            {
                var isNullable = property.Type is { } t && (t & JsonSchemaType.Null) != 0;
                if (!isNullable)
                {
                    schema.Required.Add(name);
                }
            }
        }
        return Task.CompletedTask;
    });
});
```