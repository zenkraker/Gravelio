import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { User } from './api';
import { AuthService, unauthorizedInterceptor } from './auth';

const admin = {
  id: 1,
  name: 'Admin',
  username: 'admin',
  access_type: 'admin',
  units: 'metric',
} as User;

describe('AuthService', () => {
  let auth: AuthService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideHttpClient(withInterceptors([unauthorizedInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    auth = TestBed.inject(AuthService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('logs in, then loads the user so the XSRF token matches the new identity', async () => {
    const login = auth.login('admin', 'pw');
    http
      .expectOne({ method: 'POST', url: '/api/v1/auth/login' })
      .flush(null, { status: 204, statusText: 'No Content' });
    await vi.waitFor(() => http.expectOne({ method: 'GET', url: '/api/v1/users/me' }).flush(admin));
    await login;
    expect(auth.user()).toEqual(admin);
    expect(auth.isAdmin()).toBe(true);
  });

  it('treats a 401 at startup as "not signed in" without redirecting', async () => {
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigateByUrl');
    const restore = auth.restore();
    http.expectOne('/api/v1/users/me').flush(null, { status: 401, statusText: 'Unauthorized' });
    await restore;
    expect(auth.user()).toBeNull();
    expect(navigate).not.toHaveBeenCalled();
  });

  it('sends a signed-in user to the login page when the session ends', async () => {
    auth.user.set(admin);
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
    TestBed.inject(HttpClient)
      .get('/api/v1/activities')
      .subscribe({ error: () => undefined });
    http.expectOne('/api/v1/activities').flush(null, { status: 401, statusText: 'Unauthorized' });
    expect(auth.user()).toBeNull();
    expect(navigate).toHaveBeenCalledWith('/login');
  });
});
