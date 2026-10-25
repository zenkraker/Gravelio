import { Routes } from '@angular/router';
import { adminGuard, authGuard } from './core/auth';

export const routes: Routes = [
  {
    path: 'login',
    title: 'Sign in',
    loadComponent: () => import('./pages/login').then((m) => m.Login),
  },
  {
    path: '',
    canActivate: [authGuard],
    loadComponent: () => import('./shell').then((m) => m.Shell),
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'activities' },
      {
        path: 'activities',
        title: 'Activities',
        loadComponent: () => import('./pages/activities').then((m) => m.Activities),
      },
      {
        path: 'activities/:id',
        title: 'Activity',
        loadComponent: () => import('./pages/activity-detail').then((m) => m.ActivityDetail),
      },
      {
        path: 'gear',
        title: 'Gear',
        loadComponent: () => import('./pages/gears').then((m) => m.Gears),
      },
      {
        path: 'gear/:id',
        title: 'Gear',
        loadComponent: () => import('./pages/gear-detail').then((m) => m.GearDetail),
      },
      {
        path: 'weight',
        title: 'Weight',
        loadComponent: () => import('./pages/weight').then((m) => m.Weight),
      },
      {
        path: 'profile',
        title: 'Profile',
        loadComponent: () => import('./pages/profile').then((m) => m.Profile),
      },
      {
        path: 'users',
        title: 'Users',
        canActivate: [adminGuard],
        loadComponent: () => import('./pages/users').then((m) => m.Users),
      },
    ],
  },
  { path: '**', redirectTo: '' },
];
