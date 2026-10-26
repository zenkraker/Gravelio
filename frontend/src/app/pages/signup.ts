import { HttpClient, httpResource } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { FormField, form, required, submit } from '@angular/forms/signals';
import { firstValueFrom } from 'rxjs';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { problemMessage, PublicSettings, SignupResult } from '../core/api';
import { OUTLINE_FIELDS } from '../core/outline-fields';

@Component({
  selector: 'app-signup',
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
          <h1 mat-card-title i18n>Create an account</h1>
        </mat-card-header>
        <mat-card-content>
          @if (result(); as r) {
            <p role="status">
              @if (r.pending_email_verification) {
                <span i18n>Account created. Check your email to confirm your address.</span>
              } @else if (r.pending_approval) {
                <span i18n
                  >Account created. An administrator must approve it before you can sign in.</span
                >
              } @else {
                <span i18n>Account created. You can sign in now.</span>
              }
            </p>
          } @else if (settings.value(); as s) {
            @if (s.signup_enabled) {
              <form novalidate (submit)="onSubmit($event)">
                <mat-form-field>
                  <mat-label i18n>Name</mat-label>
                  <input matInput autocomplete="name" [formField]="f.name" />
                </mat-form-field>
                <mat-form-field>
                  <mat-label i18n>Username</mat-label>
                  <input matInput autocomplete="username" [formField]="f.username" />
                </mat-form-field>
                <mat-form-field>
                  <mat-label i18n>Email</mat-label>
                  <input matInput type="email" autocomplete="email" [formField]="f.email" />
                </mat-form-field>
                <mat-form-field>
                  <mat-label i18n>Password</mat-label>
                  <input
                    matInput
                    type="password"
                    autocomplete="new-password"
                    [formField]="f.password"
                  />
                </mat-form-field>
                <mat-form-field>
                  <mat-label i18n>Repeat password</mat-label>
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
                  Create account
                </button>
              </form>
            } @else {
              <p i18n>Sign-up is disabled on this server.</p>
            }
          } @else if (settings.error()) {
            <p class="error" role="alert" i18n>Could not load the server settings.</p>
          } @else {
            <p i18n>Loading…</p>
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
      width: min(100%, 440px);
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
export class Signup {
  private readonly http = inject(HttpClient);

  protected readonly settings = httpResource<PublicSettings>(
    () => '/api/v1/server-settings/public',
  );
  protected readonly result = signal<SignupResult | null>(null);
  protected readonly error = signal('');
  private readonly model = signal({ name: '', username: '', email: '', password: '', confirm: '' });
  protected readonly f = form(this.model, (p) => {
    required(p.name, { message: $localize`Enter your name` });
    required(p.username, { message: $localize`Enter a username` });
    required(p.email, { message: $localize`Enter your email` });
    required(p.password, { message: $localize`Enter a password` });
    required(p.confirm, { message: $localize`Repeat the password` });
  });

  protected async onSubmit(event: Event): Promise<void> {
    event.preventDefault();
    this.error.set('');
    await submit(this.f, {
      action: async () => {
        const v = this.model();
        if (v.password !== v.confirm) {
          this.error.set($localize`The passwords do not match`);
          return;
        }
        try {
          this.result.set(
            await firstValueFrom(
              this.http.post<SignupResult>('/api/v1/auth/signup', {
                name: v.name,
                username: v.username,
                email: v.email,
                password: v.password,
              }),
            ),
          );
        } catch (err) {
          this.error.set(problemMessage(err));
        }
      },
      onInvalid: () => this.error.set($localize`Fill in every field`),
    });
  }
}
