import { Routes } from '@angular/router';
import { adminGuard, authGuard } from './core/auth';

export const routes: Routes = [
  {
    path: 'login',
    title: 'Sign in',
    loadComponent: () => import('./pages/login').then((m) => m.Login),
  },
  {
    path: 'forgot-password',
    title: 'Reset password',
    loadComponent: () => import('./pages/forgot-password').then((m) => m.ForgotPassword),
  },
  {
    path: 'reset-password',
    title: 'Choose a new password',
    loadComponent: () => import('./pages/reset-password').then((m) => m.ResetPassword),
  },
  {
    path: 'signup',
    title: 'Create an account',
    loadComponent: () => import('./pages/signup').then((m) => m.Signup),
  },
  {
    path: 'verify-email',
    title: 'Confirm your email',
    loadComponent: () => import('./pages/verify-email').then((m) => m.VerifyEmail),
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
        path: 'stats',
        title: 'Statistics',
        loadComponent: () => import('./pages/stats').then((m) => m.Stats),
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
        path: 'fasting',
        title: 'Fasting',
        loadComponent: () => import('./pages/fasting').then((m) => m.Fasting),
      },
      {
        path: 'poop',
        title: 'Bowel movements',
        loadComponent: () => import('./pages/poop').then((m) => m.Poop),
      },
      {
        path: 'notifications',
        title: 'Notifications',
        loadComponent: () => import('./pages/notifications').then((m) => m.NotificationsPage),
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
      {
        path: 'server-settings',
        title: 'Server settings',
        canActivate: [adminGuard],
        loadComponent: () => import('./pages/server-settings').then((m) => m.ServerSettingsPage),
      },
    ],
  },
  { path: '**', redirectTo: '' },
];
