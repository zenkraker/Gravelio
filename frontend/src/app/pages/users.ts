import { HttpClient, httpResource } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormField, form, required, submit } from '@angular/forms/signals';
import { firstValueFrom } from 'rxjs';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSnackBar } from '@angular/material/snack-bar';
import { User, problemMessage } from '../core/api';
import { AuthService } from '../core/auth';
import { ConfirmDialog } from '../components/confirm-dialog';

@Component({
  selector: 'app-users',
  imports: [FormField, MatButtonModule, MatCardModule, MatFormFieldModule, MatInputModule],
  template: `
    <h1 i18n>Users</h1>
    <table>
      <caption class="visually-hidden" i18n>
        All users
      </caption>
      <thead>
        <tr>
          <th scope="col" i18n>Username</th>
          <th scope="col" i18n>Name</th>
          <th scope="col" i18n>Email</th>
          <th scope="col" i18n>Role</th>
          <th scope="col" i18n>Status</th>
          <th scope="col" i18n>Actions</th>
        </tr>
      </thead>
      <tbody>
        @for (u of users.value(); track u.id) {
          <tr>
            <th scope="row">{{ u.username }}</th>
            <td>{{ u.name }}</td>
            <td>{{ u.email }}</td>
            <td>{{ u.access_type }}</td>
            <td>{{ u.active ? activeLabel : inactiveLabel }}</td>
            <td>
              @if (u.id !== me()?.id) {
                <button mat-button type="button" (click)="toggleActive(u)">
                  {{ u.active ? deactivateLabel : activateLabel }}
                </button>
                <button mat-button type="button" (click)="confirmDelete(u)" i18n>Delete</button>
              }
            </td>
          </tr>
        }
      </tbody>
    </table>

    <h2 i18n>Add user</h2>
    <mat-card appearance="outlined">
      <mat-card-content>
        <form novalidate (submit)="create($event)">
          <mat-form-field
            ><mat-label i18n>Name</mat-label><input matInput [formField]="f.name"
          /></mat-form-field>
          <mat-form-field
            ><mat-label i18n>Username</mat-label
            ><input matInput autocomplete="off" [formField]="f.username"
          /></mat-form-field>
          <mat-form-field
            ><mat-label i18n>Email</mat-label
            ><input matInput type="email" autocomplete="off" [formField]="f.email"
          /></mat-form-field>
          <mat-form-field>
            <mat-label i18n>Role</mat-label>
            <select matNativeControl [formField]="f.access_type">
              <option value="regular" i18n>Regular</option>
              <option value="admin" i18n>Admin</option>
            </select>
          </mat-form-field>
          <mat-form-field
            ><mat-label i18n>Password</mat-label
            ><input matInput type="password" autocomplete="new-password" [formField]="f.password"
          /></mat-form-field>
          @if (error()) {
            <p class="error" role="alert">{{ error() }}</p>
          }
          <button mat-flat-button type="submit" [disabled]="f().submitting()" i18n>Add user</button>
        </form>
      </mat-card-content>
    </mat-card>
  `,
  styles: `
    table {
      width: 100%;
      border-collapse: collapse;
    }
    th,
    td {
      text-align: start;
      padding: 8px;
      border-bottom: 1px solid var(--mat-sys-outline-variant);
    }
    .visually-hidden {
      position: absolute;
      width: 1px;
      height: 1px;
      overflow: hidden;
      clip-path: inset(50%);
    }
    form {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(240px, 1fr));
      gap: 8px 16px;
      padding-top: 16px;
    }
    button[type='submit'],
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
export class Users {
  private http = inject(HttpClient);
  private dialog = inject(MatDialog);
  private snack = inject(MatSnackBar);
  protected readonly me = inject(AuthService).user;

  protected readonly users = httpResource<User[]>(() => '/api/v1/users');
  protected readonly error = signal('');
  protected readonly activeLabel = $localize`Active`;
  protected readonly inactiveLabel = $localize`Inactive`;
  protected readonly activateLabel = $localize`Activate`;
  protected readonly deactivateLabel = $localize`Deactivate`;

  private readonly model = signal({
    name: '',
    username: '',
    email: '',
    access_type: 'regular',
    password: '',
  });
  protected readonly f = form(this.model, (p) => {
    required(p.name, { message: $localize`Enter a name` });
    required(p.username, { message: $localize`Enter a username` });
    required(p.email, { message: $localize`Enter an email` });
    required(p.password, { message: $localize`Enter a password` });
  });

  protected async create(event: Event): Promise<void> {
    event.preventDefault();
    this.error.set('');
    await submit(this.f, async () => {
      try {
        await firstValueFrom(this.http.post('/api/v1/users', { ...this.model(), active: true }));
        this.model.set({ name: '', username: '', email: '', access_type: 'regular', password: '' });
        this.users.reload();
      } catch (err) {
        this.error.set(problemMessage(err));
      }
    });
  }

  protected async toggleActive(user: User): Promise<void> {
    try {
      await firstValueFrom(
        this.http.put(`/api/v1/users/${user.id}`, {
          name: user.name,
          username: user.username,
          email: user.email,
          access_type: user.access_type,
          active: !user.active,
        }),
      );
      this.users.reload();
    } catch (err) {
      this.snack.open(problemMessage(err), $localize`Dismiss`, { duration: 8000 });
    }
  }

  protected confirmDelete(user: User): void {
    this.dialog
      .open(ConfirmDialog, {
        data: {
          title: $localize`Delete user?`,
          message: $localize`${user.username} and all of their activities will be permanently deleted.`,
          confirm: $localize`Delete`,
        },
      })
      .afterClosed()
      .subscribe(async (confirmed) => {
        if (!confirmed) return;
        try {
          await firstValueFrom(this.http.delete(`/api/v1/users/${user.id}`));
          this.users.reload();
        } catch (err) {
          this.snack.open(problemMessage(err), $localize`Dismiss`, { duration: 8000 });
        }
      });
  }
}
