import {
  HttpClient,
  HttpErrorResponse,
  HttpInterceptorFn,
  HttpRequest,
} from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { Observable, catchError, firstValueFrom, from, switchMap, throwError } from 'rxjs';
import { TokenResponse, User } from './api';

const clientHeader = { 'X-Client-Type': 'web' };

/** Access token lives in memory only; the refresh token is an httpOnly cookie the browser sends to /api/v1/auth. */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private http = inject(HttpClient);
  private router = inject(Router);
  private refreshing: Promise<boolean> | null = null;

  readonly accessToken = signal<string | null>(null);
  readonly user = signal<User | null>(null);
  readonly isAdmin = computed(() => this.user()?.access_type === 'admin');

  async login(username: string, password: string): Promise<void> {
    const res = await firstValueFrom(
      this.http.post<TokenResponse>('/api/v1/auth/login', { username, password }),
    );
    this.accessToken.set(res.access_token);
    await this.loadUser();
  }

  /** Swap the refresh cookie for a new access token; concurrent callers share one request. */
  refresh(): Promise<boolean> {
    this.refreshing ??= firstValueFrom(
      this.http.post<TokenResponse>('/api/v1/auth/refresh', null, { headers: clientHeader }),
    )
      .then((res) => {
        this.accessToken.set(res.access_token);
        return true;
      })
      .catch(() => {
        this.clear();
        return false;
      })
      .finally(() => (this.refreshing = null));
    return this.refreshing;
  }

  /** Called once at startup: resumes a session from the refresh cookie if there is one. */
  async restore(): Promise<void> {
    if (await this.refresh()) await this.loadUser().catch(() => this.clear());
  }

  async loadUser(): Promise<void> {
    this.user.set(await firstValueFrom(this.http.get<User>('/api/v1/users/me')));
  }

  async logout(): Promise<void> {
    await firstValueFrom(
      this.http.post('/api/v1/auth/logout', null, { headers: clientHeader }),
    ).catch(() => undefined);
    this.clear();
    await this.router.navigateByUrl('/login');
  }

  clear(): void {
    this.accessToken.set(null);
    this.user.set(null);
  }
}

const withToken = (req: HttpRequest<unknown>, token: string | null) =>
  token ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : req;

/** Adds the bearer token and, on a 401, refreshes once and retries before sending the user to login. */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  if (req.url.startsWith('/api/v1/auth/')) return next(req);
  return next(withToken(req, auth.accessToken())).pipe(
    catchError((err: HttpErrorResponse): Observable<never> => {
      if (err.status !== 401) return throwError(() => err);
      return from(auth.refresh()).pipe(
        switchMap((ok) => {
          if (!ok) {
            router.navigateByUrl('/login');
            return throwError(() => err);
          }
          return next(withToken(req, auth.accessToken())) as Observable<never>;
        }),
      );
    }),
  );
};

export const authGuard: CanActivateFn = () =>
  inject(AuthService).user() ? true : inject(Router).parseUrl('/login');
export const adminGuard: CanActivateFn = () =>
  inject(AuthService).isAdmin() ? true : inject(Router).parseUrl('/');
