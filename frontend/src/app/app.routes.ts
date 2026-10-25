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
        path: 'people',
        title: 'People',
        loadComponent: () => import('./pages/people').then((m) => m.People),
      },
      {
        path: 'people/:id',
        title: 'Profile',
        loadComponent: () => import('./pages/person').then((m) => m.PersonPage),
      },
      {
        path: 'weight',
        title: 'Weight',
        loadComponent: () => import('./pages/weight').then((m) => m.Weight),
      },
      {
        path: 'steps',
        title: 'Steps',
        loadComponent: () => import('./pages/steps').then((m) => m.Steps),
      },
      {
        path: 'sleep',
        title: 'Sleep',
        loadComponent: () => import('./pages/sleep').then((m) => m.Sleep),
      },
      {
        path: 'water',
        title: 'Water',
        loadComponent: () => import('./pages/water').then((m) => m.Water),
      },
      {
        path: 'targets',
        title: 'Targets',
        loadComponent: () => import('./pages/targets').then((m) => m.Targets),
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
