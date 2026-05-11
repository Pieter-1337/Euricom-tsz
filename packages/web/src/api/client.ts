export class ApiRequestError extends Error {
  constructor(public status: number) {
    super(`HTTP ${status}`);
  }
}
