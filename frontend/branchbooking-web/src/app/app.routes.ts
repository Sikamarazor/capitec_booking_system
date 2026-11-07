import { Routes } from '@angular/router';

// Routes are added by the work items that build the pages: /book (WI-502), /confirmation (WI-503), /manage (WI-504).
// Keep the wildcard last.
export const routes: Routes = [
  {
    path: '',
    title: 'Book a branch appointment',
    loadComponent: () => import('./features/home/home').then((m) => m.Home),
  },
  {
    path: '**',
    title: 'Page not found',
    loadComponent: () => import('./features/not-found/not-found').then((m) => m.NotFound),
  },
];
