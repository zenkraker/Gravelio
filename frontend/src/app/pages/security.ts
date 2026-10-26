import { HttpClient, httpResource } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { FormField, form, required, submit } from '@angular/forms/signals';
import { firstValueFrom } from 'rxjs';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MfaSetup, MfaStatus, RecoveryCodes, problemMessage } from '../core/api';
import { AuthService } from '../core/auth';
import { ApiKeys } from '../components/api-keys';
import { Sessions } from '../components/sessions';
import { OUTLINE_FIELDS } from '../core/outline-fields';

@Component({
  selector: 'app-security',
  providers: [OUTLINE_FIELDS],
  imports: [
    ApiKeys,
    Sessions,
    RouterLink,
    FormField,
    MatButtonModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
  ],
  template: `
    <header>
      <h1 i18n>Security</h1>
      <a mat-button routerLink="/profile" i18n>Back to profile</a>
    </header>

    <h2 i18n>Two-factor authentication</h2>
    <mat-card appearance="outlined">
      <mat-card-content>
        @if (recoveryCodes(); as codes) {
          <p i18n>
            Keep these recovery codes somewhere safe. Each one works once, and this is the only time
            they are shown.
          </p>
          <ul class="codes" aria-label="Recovery codes" i18n-aria-label>
            @for (code of codes; track code) {
              <li>
                <code>{{ code }}</code>
              </li>
            }
          </ul>
          <button mat-flat-button type="button" (click)="recoveryCodes.set(null)" i18n>
            I have saved my codes
          </button>
        } @else if (status.value(); as current) {
          @if (current.enabled) {
            <p i18n>
              Two-factor authentication is on. Recovery codes left:
              {{ current.recovery_codes_left }}.
            </p>
            <form novalidate (submit)="regenerate($event)">
              <mat-form-field
                ><mat-label i18n>Current password</mat-label
                ><input
                  matInput
                  type="password"
                  autocomplete="current-password"
                  [formField]="mf.password"
              /></mat-form-field>
              <mat-form-field
                ><mat-label i18n>Authenticator or recovery code</mat-label
                ><input matInput autocomplete="one-time-code" [formField]="mf.code"
              /></mat-form-field>
              @if (error()) {
                <p class="error" role="alert">{{ error() }}</p>
              }
              <div class="actions">
                <button mat-stroked-button type="submit" [disabled]="mf().submitting()" i18n>
                  New recovery codes
                </button>
                <button
                  mat-flat-button
                  type="button"
                  [disabled]="mf().submitting()"
                  (click)="disable()"
                  i18n
                >
                  Turn off
                </button>
              </div>
            </form>
          } @else if (setupInfo(); as info) {
            <p i18n>
              Add this key to your authenticator app, then enter the code the app shows. The key
              stays valid until you finish or start over.
            </p>
            <p>
              <code class="key">{{ spaced(info.secret) }}</code>
            </p>
            <form novalidate (submit)="enable($event)">
              <mat-form-field
                ><mat-label i18n>Current password</mat-label
                ><input
                  matInput
                  type="password"
                  autocomplete="current-password"
                  [formField]="ef.password"
              /></mat-form-field>
              <mat-form-field
                ><mat-label i18n>Code from the app</mat-label
                ><input matInput autocomplete="one-time-code" [formField]="ef.code"
              /></mat-form-field>
              @if (error()) {
                <p class="error" role="alert">{{ error() }}</p>
              }
              <div class="actions">
                <button mat-flat-button type="submit" [disabled]="ef().submitting()" i18n>
                  Turn on
                </button>
                <button mat-button type="button" (click)="setupInfo.set(null)" i18n>Cancel</button>
              </div>
            </form>
          } @else {
            <p i18n>
              Two-factor authentication is off. Turn it on to ask for a code from an authenticator
              app each time you sign in.
            </p>
            @if (error()) {
              <p class="error" role="alert">{{ error() }}</p>
            }
            <button mat-flat-button type="button" (click)="start()" i18n>Set up</button>
          }
        } @else if (status.error()) {
          <p class="error" role="alert" i18n>Could not load your security settings.</p>
        }
      </mat-card-content>
    </mat-card>

    <app-sessions />
    <app-api-keys />
  `,
  styles: `
    header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 16px;
    }
    form {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(260px, 1fr));
      gap: 8px 16px;
      padding-top: 16px;
    }
    .actions,
    .error {
      grid-column: 1 / -1;
    }
    .actions {
      display: flex;
      flex-wrap: wrap;
      gap: 8px;
    }
    .error {
      color: var(--mat-sys-error);
      margin: 0;
    }
    .codes {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(180px, 1fr));
      gap: 8px;
      list-style: none;
      padding: 0;
    }
    code {
      font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
      background: var(--mat-sys-surface-container-high);
      border-radius: var(--mat-sys-corner-small);
      padding: 4px 8px;
    }
    .key {
      font: var(--mat-sys-title-medium);
      word-break: break-all;
    }
  `,
})
export class Security {
  private http = inject(HttpClient);
  private auth = inject(AuthService);
  private snack = inject(MatSnackBar);

  protected readonly status = httpResource<MfaStatus>(() => '/api/v1/users/me/mfa');
  protected readonly setupInfo = signal<MfaSetup | null>(null);
  // The recovery codes can be read only in the response that creates them, so they live here until dismissed.
  protected readonly recoveryCodes = signal<string[] | null>(null);
  protected readonly error = signal('');

  private readonly enableModel = signal({ password: '', code: '' });
  protected readonly ef = form(this.enableModel, (p) => {
    required(p.password, { message: $localize`Enter your current password` });
    required(p.code, { message: $localize`Enter the code from your app` });
  });

  private readonly manageModel = signal({ password: '', code: '' });
  protected readonly mf = form(this.manageModel, (p) => {
    required(p.password, { message: $localize`Enter your current password` });
    required(p.code, { message: $localize`Enter a code` });
  });

  protected spaced(secret: string): string {
    return secret.match(/.{1,4}/g)?.join(' ') ?? secret;
  }

  protected async start(): Promise<void> {
    this.error.set('');
    try {
      this.setupInfo.set(
        await firstValueFrom(this.http.post<MfaSetup>('/api/v1/users/me/mfa/setup', null)),
      );
    } catch (err) {
      this.error.set(problemMessage(err));
    }
  }

  protected async enable(event: Event): Promise<void> {
    event.preventDefault();
    this.error.set('');
    await submit(this.ef, async () => {
      try {
        const { password, code } = this.enableModel();
        const result = await firstValueFrom(
          this.http.post<RecoveryCodes>('/api/v1/users/me/mfa/enable', {
            current_password: password,
            code,
          }),
        );
        this.enableModel.set({ password: '', code: '' });
        this.setupInfo.set(null);
        this.recoveryCodes.set(result.recovery_codes);
        await this.auth.loadUser();
        this.status.reload();
      } catch (err) {
        this.error.set(problemMessage(err));
      }
    });
  }

  protected async regenerate(event: Event): Promise<void> {
    event.preventDefault();
    this.error.set('');
    await submit(this.mf, async () => {
      try {
        const { password, code } = this.manageModel();
        const result = await firstValueFrom(
          this.http.post<RecoveryCodes>('/api/v1/users/me/mfa/recovery-codes', {
            current_password: password,
            code,
          }),
        );
        this.manageModel.set({ password: '', code: '' });
        this.recoveryCodes.set(result.recovery_codes);
        this.status.reload();
      } catch (err) {
        this.error.set(problemMessage(err));
      }
    });
  }

  protected async disable(): Promise<void> {
    this.error.set('');
    await submit(this.mf, async () => {
      try {
        const { password, code } = this.manageModel();
        await firstValueFrom(
          this.http.post('/api/v1/users/me/mfa/disable', { current_password: password, code }),
        );
        this.manageModel.set({ password: '', code: '' });
        await this.auth.loadUser();
        this.status.reload();
        this.snack.open($localize`Two-factor authentication is off`, undefined, { duration: 4000 });
      } catch (err) {
        this.error.set(problemMessage(err));
      }
    });
  }
}
