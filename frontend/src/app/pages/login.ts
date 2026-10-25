import { Component, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { FormField, form, required, submit } from '@angular/forms/signals';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { OUTLINE_FIELDS } from '../core/outline-fields';
import { MatInputModule } from '@angular/material/input';
import { problemMessage } from '../core/api';
import { AuthService } from '../core/auth';

@Component({
  selector: 'app-login',
  providers: [OUTLINE_FIELDS],
  imports: [FormField, MatCardModule, MatFormFieldModule, MatInputModule, MatButtonModule],
  template: `
    <main>
      <mat-card>
        <mat-card-header>
          <h1 mat-card-title i18n>Sign in to Gravelio</h1>
        </mat-card-header>
        <mat-card-content>
          <form novalidate (submit)="onSubmit($event)">
            <mat-form-field>
              <mat-label i18n>Username</mat-label>
              <input matInput autocomplete="username" [formField]="f.username" />
            </mat-form-field>
            <mat-form-field>
              <mat-label i18n>Password</mat-label>
              <input
                matInput
                type="password"
                autocomplete="current-password"
                [formField]="f.password"
              />
            </mat-form-field>
            @if (error(); as message) {
              <p class="error" role="alert">{{ message }}</p>
            }
            <button mat-flat-button type="submit" [disabled]="f().submitting()" i18n>
              Sign in
            </button>
          </form>
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
export class Login {
  private auth = inject(AuthService);
  private router = inject(Router);

  protected readonly error = signal('');
  private readonly model = signal({ username: '', password: '' });
  protected readonly f = form(this.model, (p) => {
    required(p.username, { message: $localize`Enter your username` });
    required(p.password, { message: $localize`Enter your password` });
  });

  protected async onSubmit(event: Event): Promise<void> {
    event.preventDefault();
    this.error.set('');
    await submit(this.f, {
      action: async () => {
        try {
          const { username, password } = this.model();
          await this.auth.login(username, password);
          await this.router.navigateByUrl('/');
        } catch (err) {
          this.error.set(problemMessage(err));
        }
      },
      onInvalid: () => this.error.set($localize`Enter your username and password`),
    });
  }
}
