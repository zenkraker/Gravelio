import { HttpClient } from '@angular/common/http';
import { Component, inject, input, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { FormField, form, required, submit } from '@angular/forms/signals';
import { firstValueFrom } from 'rxjs';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSnackBar } from '@angular/material/snack-bar';
import { problemMessage } from '../core/api';
import { OUTLINE_FIELDS } from '../core/outline-fields';

@Component({
  selector: 'app-reset-password',
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
          <h1 mat-card-title i18n>Choose a new password</h1>
        </mat-card-header>
        <mat-card-content>
          @if (token()) {
            <form novalidate (submit)="onSubmit($event)">
              <mat-form-field>
                <mat-label i18n>New password</mat-label>
                <input
                  matInput
                  type="password"
                  autocomplete="new-password"
                  [formField]="f.password"
                />
              </mat-form-field>
              <mat-form-field>
                <mat-label i18n>Repeat new password</mat-label>
                <input
                  matInput
                  type="password"
                  autocomplete="new-password"
                  [formField]="f.confirm"
                />
              </mat-form-field>
              @if (error(); as message) {
                <p class="error" role="alert">{{ message }}</p>
              }
              <button mat-flat-button type="submit" [disabled]="f().submitting()" i18n>
                Save password
              </button>
            </form>
          } @else {
            <p class="error" role="alert" i18n>
              This password reset link is invalid or has expired.
            </p>
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
export class ResetPassword {
  /** The token from the emailed link, bound from the query string. */
  readonly token = input<string>('');

  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly snack = inject(MatSnackBar);

  protected readonly error = signal('');
  private readonly model = signal({ password: '', confirm: '' });
  protected readonly f = form(this.model, (p) => {
    required(p.password, { message: $localize`Enter a new password` });
    required(p.confirm, { message: $localize`Repeat the new password` });
  });

  protected async onSubmit(event: Event): Promise<void> {
    event.preventDefault();
    this.error.set('');
    await submit(this.f, {
      action: async () => {
        const { password, confirm } = this.model();
        if (password !== confirm) {
          this.error.set($localize`The passwords do not match`);
          return;
        }
        try {
          await firstValueFrom(
            this.http.post('/api/v1/auth/reset-password', {
              token: this.token(),
              new_password: password,
            }),
          );
          this.snack.open($localize`Password changed. Sign in with the new one.`, undefined, {
            duration: 6000,
          });
          await this.router.navigateByUrl('/login');
        } catch (err) {
          this.error.set(problemMessage(err));
        }
      },
      onInvalid: () => this.error.set($localize`Fill in both password fields`),
    });
  }
}
