import { HttpErrorResponse, HttpClient, HttpInterceptorFn } from '@angular/common/http';
import { Service, computed, inject, signal } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { firstValueFrom } from 'rxjs';
import { User } from './api';

/**
 * Auth is the API's httpOnly cookie, so the SPA never handles tokens. Angular's built-in XSRF support echoes the
 * XSRF-TOKEN cookie as X-XSRF-TOKEN; the API hands out a token on every GET and binds it to the signed-in user.
 */
@Service()
export class AuthService {
  private http = inject(HttpClient);
  private router = inject(Router);

  readonly user = signal<User | null>(null);
  readonly isAdmin = computed(() => this.user()?.access_type === 'admin');

  async login(username: string, password: string): Promise<void> {
    await firstValueFrom(this.http.post('/api/v1/auth/login', { username, password }));
    await this.loadUser(); // also fetches the XSRF token for the new identity
  }

  /** Called once at startup; a 401 just means nobody is signed in. */
  async restore(): Promise<void> {
    await this.loadUser().catch(() => undefined);
  }

  async loadUser(): Promise<void> {
    this.user.set(await firstValueFrom(this.http.get<User>('/api/v1/users/me')));
  }

  async logout(): Promise<void> {
    await firstValueFrom(this.http.post('/api/v1/auth/logout', null)).catch(() => undefined);
    this.user.set(null);
    await this.restore(); // picks up an anonymous XSRF token for the next login
    await this.router.navigateByUrl('/login');
  }
}

/** A 401 while signed in means the session ended: drop the user and go to the login page. */
export const unauthorizedInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  return next(req).pipe(
    catchError((err: HttpErrorResponse) => {
      if (err.status === 401 && auth.user()) {
        auth.user.set(null);
        void router.navigateByUrl('/login');
      }
      return throwError(() => err);
    }),
  );
};

export const authGuard: CanActivateFn = () =>
  inject(AuthService).user() ? true : inject(Router).parseUrl('/login');
export const adminGuard: CanActivateFn = () =>
  inject(AuthService).isAdmin() ? true : inject(Router).parseUrl('/');
