import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { App } from './app';

describe('App', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [App],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('shows the page heading', async () => {
    const fixture = TestBed.createComponent(App);
    http.expectOne('/api/v1/health').flush({ status: 'ok', service: 's', version: '1', timestampUtc: 't' });
    await fixture.whenStable();

    expect((fixture.nativeElement as HTMLElement).querySelector('h1')?.textContent).toContain(
      'Book a branch appointment',
    );
  });

  it('shows API online when the health call succeeds', async () => {
    const fixture = TestBed.createComponent(App);
    http.expectOne('/api/v1/health').flush({ status: 'ok', service: 's', version: '1', timestampUtc: 't' });
    await fixture.whenStable();

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('API online');
  });

  it('shows API unavailable when the health call fails', async () => {
    const fixture = TestBed.createComponent(App);
    http.expectOne('/api/v1/health').flush('boom', { status: 503, statusText: 'Service Unavailable' });
    await fixture.whenStable();

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('API unavailable');
  });
});
