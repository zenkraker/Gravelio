import { HttpClient, httpResource } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormField, form, required, submit } from '@angular/forms/signals';
import { firstValueFrom } from 'rxjs';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { OUTLINE_FIELDS } from '../core/outline-fields';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { Paged, User, problemMessage } from '../core/api';
import { AuthService } from '../core/auth';
import { ConfirmDialog } from '../components/confirm-dialog';

@Component({
  selector: 'app-users',
  providers: [OUTLINE_FIELDS],
  imports: [
    FormField,
    MatButtonModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatPaginatorModule,
    MatTableModule,
  ],
  template: `
    <h1 i18n>Users</h1>
    <table
      mat-table
      [dataSource]="users.value()?.items ?? []"
      aria-label="All users"
      i18n-aria-label
    >
      <ng-container matColumnDef="username">
        <th mat-header-cell *matHeaderCellDef i18n>Username</th>
        <th mat-cell scope="row" *matCellDef="let u">{{ u.username }}</th>
      </ng-container>
      <ng-container matColumnDef="name">
        <th mat-header-cell *matHeaderCellDef i18n>Name</th>
        <td mat-cell *matCellDef="let u">{{ u.name }}</td>
      </ng-container>
      <ng-container matColumnDef="email">
        <th mat-header-cell *matHeaderCellDef i18n>Email</th>
        <td mat-cell *matCellDef="let u">{{ u.email }}</td>
      </ng-container>
      <ng-container matColumnDef="role">
        <th mat-header-cell *matHeaderCellDef i18n>Role</th>
        <td mat-cell *matCellDef="let u">{{ u.access_type }}</td>
      </ng-container>
      <ng-container matColumnDef="status">
        <th mat-header-cell *matHeaderCellDef i18n>Status</th>
        <td mat-cell *matCellDef="let u">
          {{ u.pending_approval ? pendingLabel : u.active ? activeLabel : inactiveLabel }}
        </td>
      </ng-container>
      <ng-container matColumnDef="actions">
        <th mat-header-cell *matHeaderCellDef i18n>Actions</th>
        <td mat-cell *matCellDef="let u">
          @if (u.id !== me()?.id) {
            @if (u.pending_approval) {
              <button
                mat-button
                type="button"
                [attr.aria-label]="approveLabel(u)"
                (click)="approve(u)"
                i18n
              >
                Approve
              </button>
            } @else {
              <button mat-button type="button" (click)="toggleActive(u)">
                {{ u.active ? deactivateLabel : activateLabel }}
              </button>
            }
            <button mat-button type="button" (click)="confirmDelete(u)" i18n>Delete</button>
          }
        </td>
      </ng-container>
      <tr mat-header-row *matHeaderRowDef="columns"></tr>
      <tr mat-row *matRowDef="let row; columns: columns"></tr>
    </table>
    <mat-paginator
      [length]="users.value()?.total ?? 0"
      [pageSize]="pageSize"
      [pageIndex]="pageIndex() - 1"
      [hidePageSize]="true"
      (page)="onPage($event)"
    />

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
            <mat-select [formField]="f.access_type">
              <mat-option value="regular" i18n>Regular</mat-option>
              <mat-option value="admin" i18n>Admin</mat-option>
            </mat-select>
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

  protected readonly columns = ['username', 'name', 'email', 'role', 'status', 'actions'];
  protected readonly pageSize = 20;
  protected readonly pageIndex = signal(1);
  protected readonly users = httpResource<Paged<User>>(() => ({
    url: '/api/v1/users',
    params: { page: this.pageIndex(), page_size: this.pageSize },
  }));
  protected readonly error = signal('');
  protected readonly pendingLabel = $localize`Pending approval`;
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

  protected onPage(event: PageEvent): void {
    this.pageIndex.set(event.pageIndex + 1);
  }

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

  protected approveLabel(user: User): string {
    return $localize`Approve ${user.username}`;
  }

  protected async approve(user: User): Promise<void> {
    try {
      await firstValueFrom(this.http.put(`/api/v1/users/${user.id}/approve`, {}));
      this.users.reload();
    } catch (err) {
      this.snack.open(problemMessage(err), $localize`Dismiss`, { duration: 8000 });
    }
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
