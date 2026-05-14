export type ProblemDetailFieldError = { code: string; message: string };
export type ProblemDetails = {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  code?: string;
  errors?: Record<string, ProblemDetailFieldError[]>;
  [key: string]: unknown;
};

export class ApiRequestError extends Error {
  constructor(
    public status: number,
    public problem: ProblemDetails | null,
  ) {
    super(problem?.title ?? `HTTP ${status}`);
  }

  get isExpected() {
    return this.status >= 400 && this.status < 500;
  }

  get userMessage() {
    return this.isExpected
      ? (this.problem?.detail ?? this.problem?.title ?? 'Something went wrong.')
      : 'Something went wrong.';
  }

  get fieldErrors() {
    return this.problem?.errors;
  }
}
