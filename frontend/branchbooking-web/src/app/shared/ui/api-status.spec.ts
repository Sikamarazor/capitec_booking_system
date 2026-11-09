import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ApiStatus } from './api-status';

describe('ApiStatus', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('says it is checking until the health call answers', () => {
    const fixture = TestBed.createComponent(ApiStatus);
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Checking');
    http.expectOne('/api/v1/health');
  });

  it('shows online when the health call succeeds', async () => {
    const fixture = TestBed.createComponent(ApiStatus);
    fixture.detectChanges();
    http.expectOne('/api/v1/health').flush({ status: 'ok', service: 's', version: '1', timestampUtc: 't' });
    await fixture.whenStable();

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Online');
  });

  it('shows unavailable when the health call fails', async () => {
    const fixture = TestBed.createComponent(ApiStatus);
    fixture.detectChanges();
    http.expectOne('/api/v1/health').flush('boom', { status: 503, statusText: 'Service Unavailable' });
    await fixture.whenStable();

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Unavailable');
  });
});
