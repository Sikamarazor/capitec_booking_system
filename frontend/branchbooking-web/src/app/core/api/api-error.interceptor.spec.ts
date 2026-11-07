import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ApiError } from './api-error';
import { apiErrorInterceptor } from './api-error.interceptor';

describe('apiErrorInterceptor', () => {
  let client: HttpClient;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(withInterceptors([apiErrorInterceptor])), provideHttpClientTesting()],
    });
    client = TestBed.inject(HttpClient);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('passes a successful response through untouched', () => {
    let result: unknown;
    client.get('/x').subscribe((r) => (result = r));

    http.expectOne('/x').flush({ ok: true });

    expect(result).toEqual({ ok: true });
  });

  it('turns an error response into an ApiError', () => {
    let caught: unknown;
    client.get('/x').subscribe({ error: (e: unknown) => (caught = e) });

    http.expectOne('/x').flush({ code: 'booking_not_found' }, { status: 404, statusText: 'Not Found' });

    expect(caught instanceof ApiError).toBe(true);
    expect((caught as ApiError).code).toBe('booking_not_found');
    expect((caught as ApiError).status).toBe(404);
  });

  it('turns a network failure into a network_error', () => {
    let caught: unknown;
    client.get('/x').subscribe({ error: (e: unknown) => (caught = e) });

    http.expectOne('/x').error(new ProgressEvent('error'));

    expect((caught as ApiError).code).toBe('network_error');
  });
});
