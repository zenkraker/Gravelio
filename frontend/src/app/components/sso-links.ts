import { HttpClient, httpResource } from '@angular/common/http';
import { Component, computed, inject } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { MatButtonModule } from '@angular/material/button';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { MySso, problemMessage } from '../core/api';

/** Connect or disconnect the account from the identity providers an admin has set up. */
@Component({
  selector: 'app-sso-links',
  imports: [MatButtonModule, MatTableModule],
  template: `
    @if (providers().length) {
      <section aria-labelledby="sso-title">
        <h2 id="sso-title" i18n>Single sign-on</h2>
        <p i18n>
          Connect a provider to sign in with it instead of a password. Only an account you connect
          here can be signed in to this way.
        </p>
        @if (notice(); as n) {
          <p [class.error]="n.error" [attr.role]="n.error ? 'alert' : 'status'">{{ n.text }}</p>
        }
        <table
          mat-table
          [dataSource]="providers()"
          aria-label="Single sign-on providers"
          i18n-aria-label
        >
          <ng-container matColumnDef="name">
            <th mat-header-cell *matHeaderCellDef i18n>Provider</th>
            <td mat-cell *matCellDef="let p">{{ p.name }}</td>
          </ng-container>
          <ng-container matColumnDef="status">
            <th mat-header-cell *matHeaderCellDef i18n>Status</th>
            <td mat-cell *matCellDef="let p">{{ p.linked ? connected : notConnected }}</td>
          </ng-container>
          <ng-container matColumnDef="actions">
            <th mat-header-cell *matHeaderCellDef>
              <span class="cdk-visually-hidden" i18n>Actions</span>
            </th>
            <td mat-cell *matCellDef="let p">
              @if (p.linked) {
                <button
                  mat-button
                  type="button"
                  [attr.aria-label]="disconnectLabel(p)"
                  (click)="unlink(p)"
                  i18n
                >
                  Disconnect
                </button>
              } @else {
                <a
                  mat-button
                  [href]="'/api/v1/users/me/sso/' + p.slug + '/link'"
                  [attr.aria-label]="connectLabel(p)"
                  i18n
                  >Connect</a
                >
              }
            </td>
          </ng-container>
          <tr mat-header-row *matHeaderRowDef="columns"></tr>
          <tr mat-row *matRowDef="let row; columns: columns"></tr>
        </table>
      </section>
    }
  `,
  styles: `
    .error {
      color: var(--mat-sys-error);
    }
  `,
})
export class SsoLinks {
  private readonly http = inject(HttpClient);
  private readonly snack = inject(MatSnackBar);
  private readonly query = inject(ActivatedRoute).snapshot.queryParamMap;

  protected readonly columns = ['name', 'status', 'actions'];
  protected readonly connected = $localize`Connected`;
  protected readonly notConnected = $localize`Not connected`;
  private readonly mine = httpResource<MySso[]>(() => '/api/v1/users/me/sso');
  protected readonly providers = computed(() => this.mine.value() ?? []);

  /** What the redirect back from the provider says happened (`?sso=linked` or `?sso_error=...`). */
  protected readonly notice = computed(() => {
    const error = this.query.get('sso_error');
    if (error === 'taken')
      return {
        error: true,
        text: $localize`That sign-in is already connected to another account.`,
      };
    if (error)
      return { error: true, text: $localize`The provider could not be connected. Try again.` };
    if (this.query.get('sso') === 'linked')
      return { error: false, text: $localize`The provider is connected to your account.` };
    return null;
  });

  protected connectLabel(p: MySso): string {
    return $localize`Connect ${p.name}`;
  }

  protected disconnectLabel(p: MySso): string {
    return $localize`Disconnect ${p.name}`;
  }

  protected async unlink(p: MySso): Promise<void> {
    try {
      await firstValueFrom(this.http.delete(`/api/v1/users/me/sso/${p.slug}`));
      this.mine.reload();
      this.snack.open($localize`${p.name} disconnected`, undefined, { duration: 4000 });
    } catch (err) {
      this.snack.open(problemMessage(err), $localize`Dismiss`, { duration: 8000 });
    }
  }
}
