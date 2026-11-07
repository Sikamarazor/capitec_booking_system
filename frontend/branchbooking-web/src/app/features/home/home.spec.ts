import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { expectNoA11yViolations } from '../../testing/a11y';
import { aType } from '../../testing/api-fixtures';
import { Home } from './home';

describe('Home', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('has one heading and links to booking and managing', () => {
    const fixture = TestBed.createComponent(Home);
    fixture.detectChanges();
    http.expectOne('/api/v1/appointment-types').flush([]);
    const root = fixture.nativeElement as HTMLElement;

    expect(root.querySelectorAll('h1').length).toBe(1);
    expect(root.querySelector('h1')?.textContent).toContain('Book a branch appointment');
    expect(root.querySelector('a[href="/book"]')).toBeTruthy();
    expect(root.querySelector('a[href="/manage"]')).toBeTruthy();
  });

  it('lists the appointment types the API returns', async () => {
    const fixture = TestBed.createComponent(Home);
    fixture.detectChanges();
    http.expectOne('/api/v1/appointment-types').flush([aType]);
    await fixture.whenStable();

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Open an account');
  });

  it('leaves the list out, and still works, when the API fails', async () => {
    const fixture = TestBed.createComponent(Home);
    fixture.detectChanges();
    http.expectOne('/api/v1/appointment-types').flush('boom', { status: 503, statusText: 'Service Unavailable' });
    await fixture.whenStable();
    const root = fixture.nativeElement as HTMLElement;

    expect(root.textContent).not.toContain('What you can book');
    expect(root.querySelector('a[href="/book"]')).toBeTruthy();
  });

  it('has no accessibility violations', async () => {
    const fixture = TestBed.createComponent(Home);
    fixture.detectChanges();
    http.expectOne('/api/v1/appointment-types').flush([aType]);
    await fixture.whenStable();

    await expectNoA11yViolations(fixture.nativeElement as HTMLElement);
  });
});
