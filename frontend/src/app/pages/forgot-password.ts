import { HttpClient } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { FormField, form, required, submit } from '@angular/forms/signals';
import { firstValueFrom } from 'rxjs';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { problemMessage } from '../core/api';
import { OUTLINE_FIELDS } from '../core/outline-fields';

@Component({
  selector: 'app-forgot-password',
  providers: [OUTLINE_FIELDS],
  imports: [
    RouterLink,
    FormField,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
  ],
  template: `
    <main>
      <mat-card>
        <mat-card-header>
          <h1 mat-card-title i18n>Reset your password</h1>
        </mat-card-header>
        <mat-card-content>
          @if (sent()) {
            <p role="status" i18n>
              If an account with that email exists, a reset link has been sent. Check your inbox.
            </p>
          } @else {
            <p i18n>Enter the email address of your account and we will send you a reset link.</p>
            <form novalidate (submit)="onSubmit($event)">
              <mat-form-field>
                <mat-label i18n>Email</mat-label>
                <input matInput type="email" autocomplete="email" [formField]="f.email" />
              </mat-form-field>
              @if (error(); as message) {
                <p class="error" role="alert">{{ message }}</p>
              }
              <button mat-flat-button type="submit" [disabled]="f().submitting()" i18n>
                Send reset link
              </button>
            </form>
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
    form {
      display: flex;
      flex-direction: column;
      gap: 8px;
      padding-top: 16px;
    }
    .error {
      color: var(--mat-sys-error);
      margin: 0;
    }
  `,
})
export class ForgotPassword {
  private readonly http = inject(HttpClient);

  protected readonly sent = signal(false);
  protected readonly error = signal('');
  private readonly model = signal({ email: '' });
  protected readonly f = form(this.model, (p) => {
    required(p.email, { message: $localize`Enter your email address` });
  });

  protected async onSubmit(event: Event): Promise<void> {
    event.preventDefault();
    this.error.set('');
    await submit(this.f, {
      action: async () => {
        try {
          // The API answers the same way for unknown addresses, so this page never reveals who has an account.
          await firstValueFrom(
            this.http.post('/api/v1/auth/forgot-password', { email: this.model().email }),
          );
          this.sent.set(true);
        } catch (err) {
          this.error.set(problemMessage(err));
        }
      },
      onInvalid: () => this.error.set($localize`Enter your email address`),
    });
  }
}
