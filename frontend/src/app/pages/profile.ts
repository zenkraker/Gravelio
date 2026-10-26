import { HttpClient, httpResource } from '@angular/common/http';
import { Component, effect, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { FormField, form, required, submit } from '@angular/forms/signals';
import { firstValueFrom } from 'rxjs';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { OUTLINE_FIELDS } from '../core/outline-fields';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { PrivacySettings, User, problemMessage } from '../core/api';
import { AuthService } from '../core/auth';
import { DataTransfer } from '../components/data-transfer';

const DAYS = ['sunday', 'monday', 'tuesday', 'wednesday', 'thursday', 'friday', 'saturday'];

type BooleanPrivacyKey = {
  [K in keyof PrivacySettings]: PrivacySettings[K] extends boolean ? K : never;
}[keyof PrivacySettings];

@Component({
  selector: 'app-profile',
  providers: [OUTLINE_FIELDS],
  imports: [
    DataTransfer,
    RouterLink,
    FormField,
    MatButtonModule,
    MatCardModule,
    MatCheckboxModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
  ],
  template: `
    <header>
      <h1 i18n>Profile</h1>
      <span>
        <a mat-button routerLink="/security" i18n>Security</a>
        <a mat-button [routerLink]="['/people', auth.user()?.id]" i18n>View public profile</a>
      </span>
    </header>
    <mat-card appearance="outlined">
      <mat-card-content>
        <form novalidate (submit)="saveProfile($event)">
          <mat-form-field
            ><mat-label i18n>Name</mat-label><input matInput [formField]="f.name"
          /></mat-form-field>
          <mat-form-field
            ><mat-label i18n>Email</mat-label
            ><input matInput type="email" autocomplete="email" [formField]="f.email"
          /></mat-form-field>
          <mat-form-field
            ><mat-label i18n>City</mat-label><input matInput [formField]="f.city"
          /></mat-form-field>
          <mat-form-field
            ><mat-label i18n>Birthdate</mat-label
            ><input matInput type="date" [formField]="f.birthdate"
          /></mat-form-field>
          <mat-form-field>
            <mat-label i18n>Units</mat-label>
            <mat-select [formField]="f.units">
              <mat-option value="metric" i18n>Metric</mat-option>
              <mat-option value="imperial" i18n>Imperial</mat-option>
            </mat-select>
          </mat-form-field>
          <mat-form-field>
            <mat-label i18n>Gender</mat-label>
            <mat-select [formField]="f.gender">
              <mat-option value="male" i18n>Male</mat-option>
              <mat-option value="female" i18n>Female</mat-option>
              <mat-option value="unspecified" i18n>Unspecified</mat-option>
            </mat-select>
          </mat-form-field>
          <mat-form-field>
            <mat-label i18n>First day of week</mat-label>
            <mat-select [formField]="f.first_day_of_week">
              @for (day of days; track day) {
                <mat-option [value]="day">{{ day }}</mat-option>
              }
            </mat-select>
          </mat-form-field>
          <mat-form-field
            ><mat-label i18n>Height (cm)</mat-label
            ><input matInput type="number" [formField]="f.height"
          /></mat-form-field>
          <mat-form-field
            ><mat-label i18n>Max heart rate (bpm)</mat-label
            ><input matInput type="number" [formField]="f.max_heart_rate"
          /></mat-form-field>
          <mat-form-field
            ><mat-label i18n>Timezone</mat-label><input matInput [formField]="f.timezone"
          /></mat-form-field>
          <mat-form-field>
            <mat-label i18n>Currency</mat-label>
            <mat-select [formField]="f.currency">
              <mat-option value="euro" i18n>Euro (€)</mat-option>
              <mat-option value="dollar" i18n>US dollar ($)</mat-option>
              <mat-option value="pound" i18n>Pound (£)</mat-option>
            </mat-select>
          </mat-form-field>
          <mat-form-field
            ><mat-label i18n>Language</mat-label><input matInput [formField]="f.preferred_language"
          /></mat-form-field>
          @if (profileError()) {
            <p class="error" role="alert">{{ profileError() }}</p>
          }
          <button mat-flat-button type="submit" [disabled]="f().submitting()" i18n>Save</button>
        </form>
      </mat-card-content>
    </mat-card>

    <h2 i18n>Activity privacy</h2>
    <mat-card appearance="outlined">
      <mat-card-content>
        @if (privacy.error()) {
          <p class="error" role="alert" i18n>Could not load privacy settings.</p>
        }
        @if (privacyModel(); as p) {
          <p i18n>
            These settings hide your data from other people. You always see your own activities in
            full.
          </p>
          <form novalidate (submit)="savePrivacy($event)">
            <mat-form-field class="visibility">
              <mat-label i18n>Default visibility</mat-label>
              <mat-select
                [value]="'' + p.default_activity_visibility"
                (selectionChange)="setDefaultVisibility($event.value)"
              >
                <mat-option value="2" i18n>Private</mat-option>
                <mat-option value="1" i18n>Followers</mat-option>
                <mat-option value="0" i18n>Public</mat-option>
              </mat-select>
            </mat-form-field>
            <p class="hint" i18n>
              Default visibility applies to new activities; each activity can override it.
            </p>
            <div class="checks">
              <mat-checkbox
                [checked]="p.hide_activity_start_time"
                (change)="setHide('hide_activity_start_time', $event.checked)"
                i18n
                >Hide start time</mat-checkbox
              >
              <mat-checkbox
                [checked]="p.hide_activity_location"
                (change)="setHide('hide_activity_location', $event.checked)"
                i18n
                >Hide location</mat-checkbox
              >
              <mat-checkbox
                [checked]="p.hide_activity_map"
                (change)="setHide('hide_activity_map', $event.checked)"
                i18n
                >Hide map</mat-checkbox
              >
              <mat-checkbox
                [checked]="p.hide_activity_hr"
                (change)="setHide('hide_activity_hr', $event.checked)"
                i18n
                >Hide heart rate</mat-checkbox
              >
              <mat-checkbox
                [checked]="p.hide_activity_power"
                (change)="setHide('hide_activity_power', $event.checked)"
                i18n
                >Hide power</mat-checkbox
              >
              <mat-checkbox
                [checked]="p.hide_activity_cadence"
                (change)="setHide('hide_activity_cadence', $event.checked)"
                i18n
                >Hide cadence</mat-checkbox
              >
              <mat-checkbox
                [checked]="p.hide_activity_elevation"
                (change)="setHide('hide_activity_elevation', $event.checked)"
                i18n
                >Hide elevation</mat-checkbox
              >
              <mat-checkbox
                [checked]="p.hide_activity_speed"
                (change)="setHide('hide_activity_speed', $event.checked)"
                i18n
                >Hide speed</mat-checkbox
              >
              <mat-checkbox
                [checked]="p.hide_activity_pace"
                (change)="setHide('hide_activity_pace', $event.checked)"
                i18n
                >Hide pace</mat-checkbox
              >
              <mat-checkbox
                [checked]="p.hide_activity_gear"
                (change)="setHide('hide_activity_gear', $event.checked)"
                i18n
                >Hide gear</mat-checkbox
              >
            </div>
            @if (privacyError()) {
              <p class="error" role="alert">{{ privacyError() }}</p>
            }
            <button mat-flat-button type="submit" [disabled]="privacy.isLoading()" i18n>
              Save privacy
            </button>
          </form>
        }
      </mat-card-content>
    </mat-card>

    <h2 i18n>Change password</h2>
    <mat-card appearance="outlined">
      <mat-card-content>
        <form novalidate (submit)="changePassword($event)">
          <mat-form-field
            ><mat-label i18n>Current password</mat-label
            ><input
              matInput
              type="password"
              autocomplete="current-password"
              [formField]="pw.current"
          /></mat-form-field>
          <mat-form-field
            ><mat-label i18n>New password</mat-label
            ><input matInput type="password" autocomplete="new-password" [formField]="pw.next"
          /></mat-form-field>
          @if (auth.user()?.mfa_enabled) {
            <mat-form-field
              ><mat-label i18n>Authenticator or recovery code</mat-label
              ><input matInput autocomplete="one-time-code" [formField]="pw.code"
            /></mat-form-field>
          }
          @if (passwordError()) {
            <p class="error" role="alert">{{ passwordError() }}</p>
          }
          <button mat-flat-button type="submit" [disabled]="pw().submitting()" i18n>
            Change password
          </button>
        </form>
      </mat-card-content>
    </mat-card>

    <app-data-transfer />
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
    button,
    .error {
      grid-column: 1 / -1;
      justify-self: start;
    }
    .hint,
    .checks {
      grid-column: 1 / -1;
    }
    .checks {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(220px, 1fr));
      gap: 4px 16px;
    }
    .visibility {
      max-width: 280px;
    }
    .error {
      color: var(--mat-sys-error);
      margin: 0;
    }
  `,
})
export class Profile {
  private http = inject(HttpClient);
  protected readonly auth = inject(AuthService);
  private snack = inject(MatSnackBar);

  protected readonly days = DAYS;
  protected readonly profileError = signal('');
  protected readonly passwordError = signal('');
  protected readonly privacyError = signal('');
  protected readonly privacy = httpResource<PrivacySettings>(() => '/api/v1/users/me/privacy');
  protected readonly privacyModel = signal<PrivacySettings | null>(null);

  constructor() {
    effect(() => {
      const settings = this.privacy.value();
      if (settings) this.privacyModel.set({ ...settings });
    });
  }

  private readonly profile = signal(this.fromUser(this.auth.user()!));
  protected readonly f = form(this.profile, (p) => {
    required(p.name, { message: $localize`Enter your name` });
    required(p.email, { message: $localize`Enter your email` });
  });

  private readonly passwords = signal({ current: '', next: '', code: '' });
  protected readonly pw = form(this.passwords, (p) => {
    required(p.current, { message: $localize`Enter your current password` });
    required(p.next, { message: $localize`Enter a new password` });
  });

  private fromUser(u: User) {
    return {
      name: u.name,
      email: u.email,
      city: u.city ?? '',
      birthdate: u.birthdate ?? '',
      units: u.units,
      gender: u.gender,
      first_day_of_week: u.first_day_of_week,
      height: u.height,
      max_heart_rate: u.max_heart_rate,
      timezone: u.timezone,
      preferred_language: u.preferred_language,
      currency: u.currency,
    };
  }

  protected async saveProfile(event: Event): Promise<void> {
    event.preventDefault();
    this.profileError.set('');
    await submit(this.f, async () => {
      try {
        const v = this.profile();
        await firstValueFrom(
          this.http.put('/api/v1/users/me', {
            ...v,
            city: v.city || null,
            birthdate: v.birthdate || null,
          }),
        );
        await this.auth.loadUser();
        this.snack.open($localize`Profile saved`, undefined, { duration: 4000 });
      } catch (err) {
        this.profileError.set(problemMessage(err));
      }
    });
  }

  protected setHide(key: BooleanPrivacyKey, value: boolean): void {
    this.privacyModel.update((p) => (p ? { ...p, [key]: value } : p));
  }

  protected setDefaultVisibility(value: string): void {
    this.privacyModel.update((p) => (p ? { ...p, default_activity_visibility: Number(value) } : p));
  }

  protected async savePrivacy(event: Event): Promise<void> {
    event.preventDefault();
    const settings = this.privacyModel();
    if (!settings) return;
    this.privacyError.set('');
    try {
      await firstValueFrom(this.http.put('/api/v1/users/me/privacy', settings));
      this.snack.open($localize`Privacy saved`, undefined, { duration: 4000 });
      this.privacy.reload();
    } catch (err) {
      this.privacyError.set(problemMessage(err));
    }
  }

  protected async changePassword(event: Event): Promise<void> {
    event.preventDefault();
    this.passwordError.set('');
    await submit(this.pw, async () => {
      try {
        const { current, next, code } = this.passwords();
        await firstValueFrom(
          this.http.put('/api/v1/users/me/password', {
            current_password: current,
            new_password: next,
            code: code || null,
          }),
        );
        this.passwords.set({ current: '', next: '', code: '' });
        this.snack.open($localize`Password changed`, undefined, { duration: 4000 });
      } catch (err) {
        this.passwordError.set(problemMessage(err));
      }
    });
  }
}
