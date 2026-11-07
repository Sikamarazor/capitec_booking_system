import { TestBed } from '@angular/core/testing';
import { Title } from '@angular/platform-browser';
import { RouterStateSnapshot } from '@angular/router';
import { AppTitleStrategy } from './app-title.strategy';

describe('AppTitleStrategy', () => {
  it('appends the product name to the page title', () => {
    const strategy = TestBed.inject(AppTitleStrategy);
    strategy.buildTitle = () => 'Book an appointment';

    strategy.updateTitle({} as RouterStateSnapshot);

    expect(TestBed.inject(Title).getTitle()).toBe('Book an appointment | Capitec Branch Booking');
  });

  it('uses the product name alone when a page has no title', () => {
    const strategy = TestBed.inject(AppTitleStrategy);
    strategy.buildTitle = () => undefined;

    strategy.updateTitle({} as RouterStateSnapshot);

    expect(TestBed.inject(Title).getTitle()).toBe('Capitec Branch Booking');
  });
});
