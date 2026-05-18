import { z } from 'zod';

const envSchema = z.object({
  BETTER_AUTH_SECRET: z.string().min(1),
  BETTER_AUTH_URL: z.string().url(),
  MICROSOFT_TENANT_ID: z.string().min(1),
  MICROSOFT_CLIENT_ID: z.string().min(1),
  MICROSOFT_CLIENT_SECRET: z.string().min(1),
  API_CLIENT_ID: z.string().min(1),
  API_URL: z.string().url(),
});

const result = envSchema.safeParse(process.env);

if (!result.success) {
  const issues = result.error.issues.map((i) => `  ${i.path.join('.')}: ${i.message}`).join('\n');
  console.error(`\n[env] Invalid environment variables:\n${issues}\n`);
  process.exit(1);
}

export const env = result.data;
