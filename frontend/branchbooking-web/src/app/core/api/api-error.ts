import { HttpErrorResponse } from '@angular/common/http';

/** Validation messages by field name, with the first letter lower-cased (`FullName` becomes `fullName`). */
export type FieldErrors = Readonly<Record<string, readonly string[]>>;

/**
 * What every failed API call becomes. The message is the code, never the server's wording, so nothing the server
 * says (which could echo personal data) can reach the screen or the console by accident.
 */
export class ApiError extends Error {
  constructor(
    readonly status: number,
    readonly code: string,
    readonly correlationId: string | null = null,
    readonly retryAfterSeconds: number | null = null,
    readonly fieldErrors: FieldErrors = {},
  ) {
    super(code);
    this.name = 'ApiError';
  }
}

export function toApiError(response: HttpErrorResponse): ApiError {
  const body = isRecord(response.error) ? response.error : null;
  const correlationId = stringOrNull(body?.['correlationId']) ?? response.headers?.get('X-Correlation-Id') ?? null;

  if (response.status === 0) {
    return new ApiError(0, 'network_error', correlationId);
  }

  const retryAfter = parseRetryAfter(response.headers?.get('Retry-After') ?? null);
  const code = stringOrNull(body?.['code']) ?? (response.status === 429 ? 'rate_limited' : 'unexpected_error');

  return new ApiError(response.status, code, correlationId, retryAfter, readFieldErrors(body?.['errors']));
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

function stringOrNull(value: unknown): string | null {
  return typeof value === 'string' && value.length > 0 ? value : null;
}

function parseRetryAfter(value: string | null): number | null {
  if (value === null || !/^\d{1,5}$/.test(value.trim())) {
    return null;
  }

  return Number(value.trim());
}

function readFieldErrors(value: unknown): FieldErrors {
  if (!isRecord(value)) {
    return {};
  }

  const result: Record<string, string[]> = {};
  for (const [key, messages] of Object.entries(value)) {
    if (Array.isArray(messages) && messages.every((m) => typeof m === 'string')) {
      result[key.charAt(0).toLowerCase() + key.slice(1)] = messages as string[];
    }
  }

  return result;
}
