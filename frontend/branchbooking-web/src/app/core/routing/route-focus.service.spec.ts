import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouteFocusService } from './route-focus.service';

describe('RouteFocusService', () => {
  let main: HTMLElement;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideRouter([])] });
    main = document.createElement('main');
    document.body.appendChild(main);
  });

  afterEach(() => main.remove());

  it('focuses the page heading and makes it focusable', () => {
    main.innerHTML = '<h1>Page heading</h1>';

    TestBed.inject(RouteFocusService).focusHeading();

    const heading = main.querySelector('h1');
    expect(heading?.getAttribute('tabindex')).toBe('-1');
    expect(document.activeElement).toBe(heading);
  });

  it('does nothing when the page has no heading', () => {
    main.innerHTML = '<p>No heading</p>';

    expect(() => TestBed.inject(RouteFocusService).focusHeading()).not.toThrow();
  });

  it('leaves an existing tabindex alone', () => {
    main.innerHTML = '<h1 tabindex="0">Page heading</h1>';

    TestBed.inject(RouteFocusService).focusHeading();

    expect(main.querySelector('h1')?.getAttribute('tabindex')).toBe('0');
  });
});
