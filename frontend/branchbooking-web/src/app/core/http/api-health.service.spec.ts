import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ApiHealthService } from './api-health.service';

describe('ApiHealthService', () => {
  let service: ApiHealthService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(ApiHealthService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('requests the versioned health endpoint', () => {
    let result: unknown;
    service.getHealth().subscribe((r) => (result = r));

    const req = http.expectOne('/api/v1/health');
    expect(req.request.method).toBe('GET');
    req.flush({ status: 'ok', service: 'BranchBooking.Api', version: '1.0.0', timestampUtc: '2026-10-31T20:00:00Z' });

    expect(result).toEqual({
      status: 'ok',
      service: 'BranchBooking.Api',
      version: '1.0.0',
      timestampUtc: '2026-10-31T20:00:00Z',
    });
  });
});
