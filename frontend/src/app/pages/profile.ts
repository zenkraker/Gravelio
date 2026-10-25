import { HttpClient } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormField, form, required, submit } from '@angular/forms/signals';
import { firstValueFrom } from 'rxjs';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { OUTLINE_FIELDS } from '../core/outline-fields';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { User, problemMessage } from '../core/api';
import { AuthService } from '../core/auth';

const DAYS = ['sunday', 'monday', 'tuesday', 'wednesday', 'thursday', 'friday', 'saturday'];

@Component({
  selector: 'app-profile',
  providers: [OUTLINE_FIELDS],
  imports: [
    FormField,
    MatButtonModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
  ],
  template: `
    <h1 i18n>Profile</h1>
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
          @if (passwordError()) {
            <p class="error" role="alert">{{ passwordError() }}</p>
          }
          <button mat-flat-button type="submit" [disabled]="pw().submitting()" i18n>
            Change password
          </button>
        </form>
      </mat-card-content>
    </mat-card>
  `,
  styles: `
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
    .error {
      color: var(--mat-sys-error);
      margin: 0;
    }
  `,
})
export class Profile {
  private http = inject(HttpClient);
  private auth = inject(AuthService);
  private snack = inject(MatSnackBar);

  protected readonly days = DAYS;
  protected readonly profileError = signal('');
  protected readonly passwordError = signal('');

  private readonly profile = signal(this.fromUser(this.auth.user()!));
  protected readonly f = form(this.profile, (p) => {
    required(p.name, { message: $localize`Enter your name` });
    required(p.email, { message: $localize`Enter your email` });
  });

  private readonly passwords = signal({ current: '', next: '' });
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

  protected async changePassword(event: Event): Promise<void> {
    event.preventDefault();
    this.passwordError.set('');
    await submit(this.pw, async () => {
      try {
        const { current, next } = this.passwords();
        await firstValueFrom(
          this.http.put('/api/v1/users/me/password', {
            current_password: current,
            new_password: next,
          }),
        );
        this.passwords.set({ current: '', next: '' });
        this.snack.open($localize`Password changed`, undefined, { duration: 4000 });
      } catch (err) {
        this.passwordError.set(problemMessage(err));
      }
    });
  }
}
