import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { App } from './app';
import { routes } from './app.routes';

describe('App', () => {
  let http: HttpTestingController;
  let router: Router;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [App],
      providers: [provideRouter(routes), provideHttpClient(), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
  });

  afterEach(() => http.verify());

  async function render(url: string): Promise<HTMLElement> {
    const fixture = TestBed.createComponent(App);
    await router.navigateByUrl(url);
    fixture.detectChanges();
    await fixture.whenStable();
    return fixture.nativeElement as HTMLElement;
  }

  function answerBackgroundCalls(): void {
    http.match('/api/v1/health').forEach((r) => r.flush({ status: 'ok', service: 's', version: '1', timestampUtc: 't' }));
    http.match('/api/v1/appointment-types').forEach((r) => r.flush([]));
  }

  it('starts with a skip link that targets the main region', async () => {
    const root = await render('/');
    answerBackgroundCalls();

    const skip = root.querySelector('a.skip-link');
    expect(skip?.textContent).toContain('Skip to main content');
    expect(root.querySelector('main#main')).toBeTruthy();
  });

  it('shows the product name and the main navigation', async () => {
    const root = await render('/');
    answerBackgroundCalls();

    expect(root.querySelector('header')?.textContent).toContain('Capitec');
    expect(root.querySelector('nav[aria-label="Main"] a[href="/book"]')).toBeTruthy();
    expect(root.querySelector('nav[aria-label="Main"] a[href="/manage"]')).toBeTruthy();
  });

  it('renders the home page at the root', async () => {
    const root = await render('/');
    answerBackgroundCalls();

    expect(root.querySelector('main h1')?.textContent).toContain('Book a branch appointment');
  });

  it('renders the not-found page for an unknown path', async () => {
    const root = await render('/nowhere');
    answerBackgroundCalls();

    expect(root.querySelector('main h1')?.textContent).toContain('Page not found');
  });

  it('shows the booking service status in the footer', async () => {
    const root = await render('/');
    answerBackgroundCalls();
    await TestBed.inject(Router).navigateByUrl('/');

    expect(root.querySelector('footer')?.textContent).toContain('Booking service');
  });
});
