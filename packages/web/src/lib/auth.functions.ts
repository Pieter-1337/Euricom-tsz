import { createServerFn } from '@tanstack/react-start';
import { getRequest } from '@tanstack/react-start/server';
import { auth } from './auth';

export type SessionUser = {
  id: string;
  name: string;
  email: string;
};

export type Session = {
  user: SessionUser;
} | null;

export const getSession = createServerFn({ method: 'GET' }).handler(async (): Promise<Session> => {
  const request = getRequest();
  const session = await auth.api.getSession({ headers: request.headers });
  if (!session) return null;
  return {
    user: {
      id: session.user.id,
      name: session.user.name,
      email: session.user.email,
    },
  };
});
