import { ApiError } from './api-error';
import { API_ERROR_CODES, CLIENT_ERROR_CODES } from './api-error-codes';
import { ERROR_MESSAGES, describeError } from './error-messages';

describe('error messages', () => {
  it('has copy for every documented and client code', () => {
    for (const code of [...API_ERROR_CODES, ...CLIENT_ERROR_CODES]) {
      const entry = ERROR_MESSAGES[code];

      expect(entry.title.length).toBeGreaterThan(0);
      expect(entry.message.length).toBeGreaterThan(0);
    }
  });

  it('describes a known code with its own copy', () => {
    const description = describeError(new ApiError(409, 'slot_not_available'));

    expect(description.title).toBe('That time was just taken');
    expect(description.retryable).toBe(false);
  });

  it('falls back to a generic message for an unknown code', () => {
    const description = describeError(new ApiError(418, 'made_up_code'));

    expect(description.title).toBe('Something went wrong');
  });

  it('falls back to a generic message for anything that is not an ApiError', () => {
    const description = describeError(new Error('boom with thandi@example.com'));

    expect(description.title).toBe('Something went wrong');
    expect(description.message).not.toContain('thandi');
    expect(description.supportCode).toBeNull();
  });

  it('shows the wait for a rate limit when the server gave one', () => {
    expect(describeError(new ApiError(429, 'rate_limited', null, 30)).message).toContain('30 seconds');
    expect(describeError(new ApiError(429, 'rate_limited', null, 1)).message).toContain('1 second ');
    expect(describeError(new ApiError(429, 'rate_limited')).message).toBe('Please wait a moment and try again.');
  });

  it('passes the correlation id through as a support code', () => {
    expect(describeError(new ApiError(500, 'internal_error', 'abc123')).supportCode).toBe('abc123');
  });
});
