import { HttpErrorResponse, HttpHeaders } from '@angular/common/http';
import { ApiError, toApiError } from './api-error';

function response(status: number, error: unknown, headers: Record<string, string> = {}): HttpErrorResponse {
  return new HttpErrorResponse({ status, error, headers: new HttpHeaders(headers) });
}

describe('toApiError', () => {
  it('reads the code and correlation id from a problem response', () => {
    const error = toApiError(response(409, { code: 'slot_not_available', correlationId: 'c-1' }));

    expect(error instanceof ApiError).toBe(true);
    expect(error.status).toBe(409);
    expect(error.code).toBe('slot_not_available');
    expect(error.correlationId).toBe('c-1');
  });

  it('falls back to the correlation id header', () => {
    const error = toApiError(response(500, { code: 'internal_error' }, { 'X-Correlation-Id': 'h-1' }));

    expect(error.correlationId).toBe('h-1');
  });

  it('treats status 0 as a network failure', () => {
    const error = toApiError(response(0, new ProgressEvent('error')));

    expect(error.code).toBe('network_error');
    expect(error.status).toBe(0);
  });

  it('uses an unexpected-error code when the body is not a problem response', () => {
    const error = toApiError(response(502, '<html>Bad gateway</html>'));

    expect(error.code).toBe('unexpected_error');
    expect(error.status).toBe(502);
  });

  it('knows a 429 without a body is a rate limit and reads Retry-After', () => {
    const error = toApiError(response(429, null, { 'Retry-After': '12' }));

    expect(error.code).toBe('rate_limited');
    expect(error.retryAfterSeconds).toBe(12);
  });

  it('ignores a Retry-After that is not a number of seconds', () => {
    const error = toApiError(response(429, { code: 'rate_limited' }, { 'Retry-After': 'soon' }));

    expect(error.retryAfterSeconds).toBeNull();
  });

  it('keeps field errors, lower-casing the first letter and dropping malformed entries', () => {
    const error = toApiError(
      response(400, {
        code: 'validation_failed',
        errors: { FullName: ['Too short'], email: ['Bad'], Phone: 'not an array', Other: [1, 2] },
      }),
    );

    expect(error.fieldErrors).toEqual({ fullName: ['Too short'], email: ['Bad'] });
  });

  it('never puts the server wording in the message', () => {
    const error = toApiError(response(400, { code: 'invalid_customer_details', detail: 'thandi@example.com is bad' }));

    expect(error.message).toBe('invalid_customer_details');
  });
});
