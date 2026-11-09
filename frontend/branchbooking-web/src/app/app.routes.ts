import { Routes } from '@angular/router';

export const routes: Routes = [
    {
        path: '',
        title: 'Book a branch appointment',
        loadComponent: () => import('./features/home/home').then((m) => m.Home),
    },
    {
        path: 'book',
        title: 'Book an appointment',
        loadComponent: () => import('./features/booking/booking-wizard').then((m) => m.BookingWizard),
    },
    {
        path: 'confirmation',
        title: 'Booking confirmation',
        loadComponent: () => import('./features/confirmation/confirmation').then((m) => m.Confirmation),
    },
    {
        path: 'manage',
        title: 'Manage your booking',
        loadComponent: () => import('./features/manage/manage-booking').then((m) => m.ManageBooking),
    },
    {
        path: '**',
        title: 'Page not found',
        loadComponent: () => import('./features/not-found/not-found').then((m) => m.NotFound),
    },
];
