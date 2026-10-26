import { DOCUMENT, DatePipe } from '@angular/common';
import { HttpClient, httpResource } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormField, form, required, submit } from '@angular/forms/signals';
import { firstValueFrom } from 'rxjs';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { ApiKey, CreatedApiKey, problemMessage } from '../core/api';
import { AuthService } from '../core/auth';
import { OUTLINE_FIELDS } from '../core/outline-fields';
import { ConfirmDialog } from './confirm-dialog';

/** Keys for tools that upload activities without a sign-in. A key is shown once, when it is made. */
@Component({
  selector: 'app-api-keys',
  providers: [OUTLINE_FIELDS],
  imports: [
    DatePipe,
    FormField,
    MatButtonModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatTableModule,
  ],
  template: `
    <h2 i18n>API keys</h2>
    <p i18n>
      A key lets a program upload activities for you, for example from a watch app or a script,
      without signing in. It can do nothing else. Send it in the <code>X-API-Key</code> header.
    </p>

    @if (created(); as c) {
      <mat-card appearance="outlined" class="created">
        <mat-card-content>
          <p i18n>
            Copy this key now. It is not stored and cannot be shown again; if you lose it, make a
            new one.
          </p>
          <p>
            <code class="secret">{{ c.key }}</code>
          </p>
          <button mat-flat-button type="button" (click)="copy(c.key)" i18n>Copy key</button>
          <button mat-button type="button" (click)="created.set(null)" i18n>
            I have copied it
          </button>
        </mat-card-content>
      </mat-card>
    }

    @if (keys.error()) {
      <p class="error" role="alert" i18n>Could not load your API keys.</p>
    }
    <table mat-table [dataSource]="keys.value() ?? []" aria-label="API keys" i18n-aria-label>
      <ng-container matColumnDef="name">
        <th mat-header-cell *matHeaderCellDef i18n>Name</th>
        <td mat-cell *matCellDef="let k">{{ k.name }}</td>
      </ng-container>
      <ng-container matColumnDef="prefix">
        <th mat-header-cell *matHeaderCellDef i18n>Key</th>
        <td mat-cell *matCellDef="let k">
          <code>{{ k.prefix }}…</code>
        </td>
      </ng-container>
      <ng-container matColumnDef="used">
        <th mat-header-cell *matHeaderCellDef i18n>Last used</th>
        <td mat-cell *matCellDef="let k">
          {{ k.last_used_at ? (k.last_used_at | date: 'medium') : '—' }}
        </td>
      </ng-container>
      <ng-container matColumnDef="status">
        <th mat-header-cell *matHeaderCellDef i18n>Status</th>
        <td mat-cell *matCellDef="let k">{{ status(k) }}</td>
      </ng-container>
      <ng-container matColumnDef="actions">
        <th mat-header-cell *matHeaderCellDef>
          <span class="cdk-visually-hidden" i18n>Actions</span>
        </th>
        <td mat-cell *matCellDef="let k">
          @if (!k.revoked_at) {
            <button
              mat-button
              type="button"
              (click)="revoke(k)"
              [attr.aria-label]="revokeLabel(k)"
              i18n
            >
              Revoke
            </button>
          }
          <button
            mat-button
            type="button"
            (click)="confirmDelete(k)"
            [attr.aria-label]="deleteLabel(k)"
            i18n
          >
            Delete
          </button>
        </td>
      </ng-container>
      <tr mat-header-row *matHeaderRowDef="columns"></tr>
      <tr mat-row *matRowDef="let row; columns: columns"></tr>
      @if (!keys.isLoading()) {
        <tr class="mat-mdc-row" *matNoDataRow>
          <td class="mat-mdc-cell" [attr.colspan]="columns.length" i18n>No API keys yet.</td>
        </tr>
      }
    </table>

    <h3 i18n>Make a key</h3>
    <mat-card appearance="outlined">
      <mat-card-content>
        <form novalidate (submit)="create($event)">
          <mat-form-field>
            <mat-label i18n>Name</mat-label>
            <input matInput [formField]="f.name" />
          </mat-form-field>
          <mat-form-field>
            <mat-label i18n>Expires</mat-label>
            <mat-select [formField]="f.expires">
              <mat-option value="" i18n>Never</mat-option>
              <mat-option value="30" i18n>In 30 days</mat-option>
              <mat-option value="90" i18n>In 90 days</mat-option>
              <mat-option value="365" i18n>In a year</mat-option>
            </mat-select>
          </mat-form-field>
          <mat-form-field>
            <mat-label i18n>Current password</mat-label>
            <input
              matInput
              type="password"
              autocomplete="current-password"
              [formField]="f.password"
            />
          </mat-form-field>
          @if (auth.user()?.mfa_enabled) {
            <mat-form-field>
              <mat-label i18n>Authenticator or recovery code</mat-label>
              <input matInput autocomplete="one-time-code" [formField]="f.code" />
            </mat-form-field>
          }
          @if (error()) {
            <p class="error" role="alert">{{ error() }}</p>
          }
          <div class="actions">
            <button mat-flat-button type="submit" [disabled]="f().submitting()" i18n>
              Make key
            </button>
          </div>
        </form>
      </mat-card-content>
    </mat-card>
  `,
  styles: `
    table {
      width: 100%;
      margin-bottom: 16px;
    }
    form {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(240px, 1fr));
      gap: 8px 16px;
      padding-top: 16px;
    }
    .actions,
    .error {
      grid-column: 1 / -1;
    }
    .error {
      color: var(--mat-sys-error);
      margin: 0;
    }
    .created {
      margin-bottom: 16px;
    }
    code {
      font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
      background: var(--mat-sys-surface-container-high);
      border-radius: var(--mat-sys-corner-small);
      padding: 2px 6px;
    }
    .secret {
      word-break: break-all;
    }
  `,
})
export class ApiKeys {
  private document = inject(DOCUMENT);
  private http = inject(HttpClient);
  private dialog = inject(MatDialog);
  private snack = inject(MatSnackBar);
  protected readonly auth = inject(AuthService);

  protected readonly columns = ['name', 'prefix', 'used', 'status', 'actions'];
  protected readonly keys = httpResource<ApiKey[]>(() => '/api/v1/users/me/api-keys');
  protected readonly created = signal<CreatedApiKey | null>(null);
  protected readonly error = signal('');

  private readonly model = signal({ name: '', expires: '', password: '', code: '' });
  protected readonly f = form(this.model, (p) => {
    required(p.name, { message: $localize`Enter a name` });
    required(p.password, { message: $localize`Enter your current password` });
  });

  protected status(k: ApiKey): string {
    if (k.revoked_at) return $localize`Revoked`;
    if (k.expires_at && Date.parse(k.expires_at) < Date.now()) return $localize`Expired`;
    return k.expires_at ? $localize`Active until ${k.expires_at.slice(0, 10)}` : $localize`Active`;
  }

  protected revokeLabel(k: ApiKey): string {
    return $localize`Revoke key ${k.name}`;
  }

  protected deleteLabel(k: ApiKey): string {
    return $localize`Delete key ${k.name}`;
  }

  protected async create(event: Event): Promise<void> {
    event.preventDefault();
    this.error.set('');
    await submit(this.f, async () => {
      try {
        const m = this.model();
        this.created.set(
          await firstValueFrom(
            this.http.post<CreatedApiKey>('/api/v1/users/me/api-keys', {
              name: m.name,
              expires_in_days: m.expires ? Number(m.expires) : null,
              current_password: m.password,
              code: m.code || null,
            }),
          ),
        );
        this.model.set({ name: '', expires: '', password: '', code: '' });
        this.keys.reload();
      } catch (err) {
        this.error.set(problemMessage(err));
      }
    });
  }

  protected async copy(key: string): Promise<void> {
    try {
      await this.document.defaultView?.navigator.clipboard.writeText(key);
      this.snack.open($localize`Key copied`, undefined, { duration: 4000 });
    } catch {
      this.snack.open($localize`Select the key and copy it by hand.`, undefined, {
        duration: 6000,
      });
    }
  }

  protected async revoke(k: ApiKey): Promise<void> {
    try {
      await firstValueFrom(this.http.post(`/api/v1/users/me/api-keys/${k.id}/revoke`, null));
      this.keys.reload();
    } catch (err) {
      this.snack.open(problemMessage(err), $localize`Dismiss`, { duration: 8000 });
    }
  }

  protected confirmDelete(k: ApiKey): void {
    this.dialog
      .open(ConfirmDialog, {
        data: {
          title: $localize`Delete key?`,
          message: $localize`“${k.name}” stops working and disappears from this list.`,
          confirm: $localize`Delete`,
        },
      })
      .afterClosed()
      .subscribe(async (confirmed) => {
        if (!confirmed) return;
        try {
          await firstValueFrom(this.http.delete(`/api/v1/users/me/api-keys/${k.id}`));
          this.keys.reload();
        } catch (err) {
          this.snack.open(problemMessage(err), $localize`Dismiss`, { duration: 8000 });
        }
      });
  }
}
