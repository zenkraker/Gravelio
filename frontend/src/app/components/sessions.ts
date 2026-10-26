import { HttpClient, httpResource } from '@angular/common/http';
import { Component, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { MatButtonModule } from '@angular/material/button';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { Paged, Session, problemMessage } from '../core/api';
import { AuthService } from '../core/auth';
import { formatDateTime } from '../core/format';

/** The devices signed in to this account. Ending one signs that device out at its next request. */
@Component({
  selector: 'app-sessions',
  imports: [MatButtonModule, MatTableModule],
  template: `
    <h2 i18n>Signed-in devices</h2>
    <p i18n>
      Every browser or app that is signed in to your account. End a session you do not recognise;
      changing your password ends all the others.
    </p>
    @if (sessions.error()) {
      <p class="error" role="alert" i18n>Could not load your sessions.</p>
    }
    <table
      mat-table
      [dataSource]="sessions.value()?.items ?? []"
      aria-label="Signed-in devices"
      i18n-aria-label
    >
      <ng-container matColumnDef="device">
        <th mat-header-cell *matHeaderCellDef i18n>Device</th>
        <td mat-cell *matCellDef="let s">
          {{ s.device }}
          @if (s.current) {
            <strong i18n>(this device)</strong>
          }
        </td>
      </ng-container>
      <ng-container matColumnDef="ip">
        <th mat-header-cell *matHeaderCellDef i18n>IP address</th>
        <td mat-cell *matCellDef="let s">{{ s.ip_address ?? '—' }}</td>
      </ng-container>
      <ng-container matColumnDef="seen">
        <th mat-header-cell *matHeaderCellDef i18n>Last seen</th>
        <td mat-cell *matCellDef="let s">{{ when(s.last_seen_at) }}</td>
      </ng-container>
      <ng-container matColumnDef="actions">
        <th mat-header-cell *matHeaderCellDef>
          <span class="cdk-visually-hidden" i18n>Actions</span>
        </th>
        <td mat-cell *matCellDef="let s">
          @if (!s.current) {
            <button mat-button type="button" [attr.aria-label]="endLabel(s)" (click)="end(s)" i18n>
              End session
            </button>
          }
        </td>
      </ng-container>
      <tr mat-header-row *matHeaderRowDef="columns"></tr>
      <tr mat-row *matRowDef="let row; columns: columns"></tr>
    </table>
  `,
  styles: `
    .error {
      color: var(--mat-sys-error);
    }
  `,
})
export class Sessions {
  private http = inject(HttpClient);
  private snack = inject(MatSnackBar);
  private auth = inject(AuthService);

  protected readonly columns = ['device', 'ip', 'seen', 'actions'];
  protected readonly sessions = httpResource<Paged<Session>>(
    () => '/api/v1/users/me/sessions?page_size=100',
  );

  protected when(iso: string): string {
    return formatDateTime(iso, this.auth.user()?.timezone ?? null);
  }

  protected endLabel(s: Session): string {
    return $localize`End session: ${s.device}, last seen ${this.when(s.last_seen_at)}`;
  }

  protected async end(s: Session): Promise<void> {
    try {
      await firstValueFrom(this.http.delete(`/api/v1/users/me/sessions/${s.id}`));
      this.sessions.reload();
    } catch (err) {
      this.snack.open(problemMessage(err), $localize`Dismiss`, { duration: 8000 });
    }
  }
}
