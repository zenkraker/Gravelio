import { HttpClient } from '@angular/common/http';
import { Component, effect, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { problemMessage } from '../core/api';

type State = 'verifying' | 'done' | 'error';

@Component({
  selector: 'app-verify-email',
  imports: [RouterLink, MatButtonModule, MatCardModule],
  template: `
    <main>
      <mat-card>
        <mat-card-header>
          <h1 mat-card-title i18n>Confirm your email</h1>
        </mat-card-header>
        <mat-card-content>
          @if (state() === 'done') {
            <p role="status" i18n>Your email address is confirmed. You can sign in now.</p>
          } @else if (state() === 'error') {
            <p class="error" role="alert">{{ error() }}</p>
          } @else {
            <p role="status" i18n>Confirming your email address…</p>
          }
          <a mat-button routerLink="/login" i18n>Back to sign in</a>
        </mat-card-content>
      </mat-card>
    </main>
  `,
  styles: `
    main {
      display: grid;
      place-items: center;
      min-height: 100vh;
      padding: 16px;
    }
    mat-card {
      width: min(100%, 400px);
      padding: 8px;
    }
    .error {
      color: var(--mat-sys-error);
      margin: 0;
    }
  `,
})
export class VerifyEmail {
  /** From the emailed link: the account and the Identity token. */
  readonly user = input<string>('');
  readonly token = input<string>('');

  private readonly http = inject(HttpClient);
  private readonly attempted = signal(false);
  protected readonly state = signal<State>('verifying');
  protected readonly error = signal('');

  constructor() {
    effect(() => {
      const user = this.user();
      const token = this.token();
      if (!user || !token || this.attempted()) return;
      this.attempted.set(true);
      void this.verify(user, token);
    });
  }

  private async verify(user: string, token: string): Promise<void> {
    try {
      await firstValueFrom(
        this.http.post('/api/v1/auth/verify-email', { user_id: Number(user), token }),
      );
      this.state.set('done');
    } catch (err) {
      this.error.set(problemMessage(err));
      this.state.set('error');
    }
  }
}
