import { ApiRequestError, type ProblemDetails } from '#/api/client';

const PREFIX = '__api_error__:';

export function throwApiError(err: unknown): never {
  if (err instanceof ApiRequestError) {
    throw new Error(
      PREFIX + JSON.stringify({ status: err.status, problem: err.problem }),
    );
  }
  throw err;
}

export function parseServerError(err: unknown): ApiRequestError | null {
  if (!(err instanceof Error)) return null;
  if (!err.message.startsWith(PREFIX)) return null;
  try {
    const payload = JSON.parse(err.message.slice(PREFIX.length)) as {
      status: number;
      problem: ProblemDetails | null;
    };
    return new ApiRequestError(payload.status, payload.problem);
  } catch {
    return null;
  }
}
