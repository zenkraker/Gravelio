import { HttpClient, httpResource } from '@angular/common/http';
import { Component, computed, effect, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatSnackBar } from '@angular/material/snack-bar';
import { problemMessage, ServerSettings, ServerSettingsRead } from '../core/api';

type SettingKey = keyof ServerSettings;

@Component({
  selector: 'app-server-settings',
  imports: [MatButtonModule, MatCardModule, MatCheckboxModule],
  template: `
    <h1 i18n>Server settings</h1>

    <mat-card appearance="outlined">
      <mat-card-content>
        @if (settings.error()) {
          <p class="error" role="alert" i18n>Could not load the server settings.</p>
        }
        @if (model(); as m) {
          <h2 i18n>Sign-up</h2>
          <p i18n>These options apply to people creating their own account.</p>
          <div class="option">
            <mat-checkbox
              [checked]="m.signup_enabled"
              (change)="set('signup_enabled', $event.checked)"
            >
              Allow self sign-up
            </mat-checkbox>
          </div>
          <div class="option">
            <mat-checkbox
              [checked]="m.signup_require_email_verification"
              [disabled]="!m.signup_enabled || !emailConfigured()"
              (change)="set('signup_require_email_verification', $event.checked)"
            >
              Require email confirmation
            </mat-checkbox>
            @if (!emailConfigured()) {
              <span class="hint" i18n>
                Email is not configured on this server, so confirmation links cannot be sent.
              </span>
            }
          </div>
          <div class="option">
            <mat-checkbox
              [checked]="m.signup_require_admin_approval"
              [disabled]="!m.signup_enabled"
              (change)="set('signup_require_admin_approval', $event.checked)"
            >
              Require an administrator to approve new accounts
            </mat-checkbox>
          </div>
          <h2 i18n>Sharing</h2>
          <p i18n>
            A public link lets anyone with the address open an activity whose visibility is Public,
            without signing in.
          </p>
          <div class="option">
            <mat-checkbox
              [checked]="m.public_shareable_links"
              (change)="set('public_shareable_links', $event.checked)"
            >
              Allow public links
            </mat-checkbox>
          </div>
          <div class="option">
            <mat-checkbox
              [checked]="m.public_shareable_links_user_info"
              [disabled]="!m.public_shareable_links"
              (change)="set('public_shareable_links_user_info', $event.checked)"
            >
              Show the owner's name on public links
            </mat-checkbox>
          </div>
          <button mat-flat-button type="button" (click)="save()" i18n>Save settings</button>
        } @else if (!settings.error()) {
          <p i18n>Loading settings…</p>
        }
      </mat-card-content>
    </mat-card>
  `,
  styles: `
    .option {
      display: flex;
      flex-direction: column;
      margin-bottom: 12px;
    }
    .hint {
      color: var(--mat-sys-on-surface-variant);
      font: var(--mat-sys-body-small);
      margin-left: 32px;
    }
    button {
      margin-top: 8px;
    }
    .error {
      color: var(--mat-sys-error);
    }
  `,
})
export class ServerSettingsPage {
  private readonly http = inject(HttpClient);
  private readonly snack = inject(MatSnackBar);

  protected readonly settings = httpResource<ServerSettingsRead>(() => '/api/v1/server-settings');
  protected readonly model = signal<ServerSettings | null>(null);
  // The API never exposes SMTP details, only whether outgoing email is usable.
  protected readonly emailConfigured = computed(
    () => this.settings.value()?.email_configured ?? true,
  );

  constructor() {
    effect(() => {
      const value = this.settings.value();
      if (value && !this.model()) {
        this.model.set({
          signup_enabled: value.signup_enabled,
          signup_require_email_verification: value.signup_require_email_verification,
          signup_require_admin_approval: value.signup_require_admin_approval,
          public_shareable_links: value.public_shareable_links,
          public_shareable_links_user_info: value.public_shareable_links_user_info,
        });
      }
    });
  }

  protected set(key: SettingKey, value: boolean): void {
    this.model.update((m) => (m ? { ...m, [key]: value } : m));
  }

  protected async save(): Promise<void> {
    const value = this.model();
    if (!value) return;
    try {
      const saved = await firstValueFrom(
        this.http.put<ServerSettings>('/api/v1/server-settings', value),
      );
      this.model.set({ ...saved });
      this.settings.reload();
      this.snack.open($localize`Server settings saved`, undefined, { duration: 4000 });
    } catch (err) {
      this.snack.open(problemMessage(err), $localize`Dismiss`, { duration: 8000 });
    }
  }
}
