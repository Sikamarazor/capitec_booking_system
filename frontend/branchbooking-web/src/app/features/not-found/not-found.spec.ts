import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { expectNoA11yViolations } from '../../testing/a11y';
import { NotFound } from './not-found';

describe('NotFound', () => {
  it('says the page does not exist and links home', () => {
    TestBed.configureTestingModule({ providers: [provideRouter([])] });
    const fixture = TestBed.createComponent(NotFound);
    fixture.detectChanges();
    const root = fixture.nativeElement as HTMLElement;

    expect(root.querySelector('h1')?.textContent).toContain('Page not found');
    expect(root.querySelector('a[href="/"]')).toBeTruthy();
  });

  it('has no accessibility violations', async () => {
    TestBed.configureTestingModule({ providers: [provideRouter([])] });
    const fixture = TestBed.createComponent(NotFound);
    fixture.detectChanges();

    await expectNoA11yViolations(fixture.nativeElement as HTMLElement);
  });
});
