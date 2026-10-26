import { HttpClient, httpResource } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormField, form, readonly, required, submit } from '@angular/forms/signals';
import { firstValueFrom } from 'rxjs';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { Paged, SsoProvider, problemMessage } from '../core/api';
import { ConfirmDialog } from '../components/confirm-dialog';
import { OUTLINE_FIELDS } from '../core/outline-fields';

const EMPTY = {
  name: '',
  slug: '',
  issuer: '',
  client_id: '',
  client_secret: '',
  scopes: 'openid profile email',
  enabled: true,
  auto_create_users: false,
};

/** The OpenID Connect providers people can sign in with: Authentik, Keycloak, a company directory, and so on. */
@Component({
  selector: 'app-sso-providers',
  providers: [OUTLINE_FIELDS],
  imports: [
    FormField,
    MatButtonModule,
    MatCardModule,
    MatCheckboxModule,
    MatFormFieldModule,
    MatInputModule,
    MatTableModule,
  ],
  template: `
    <h1 i18n>Sign-in providers</h1>
    <p i18n>
      People can sign in with an OpenID Connect provider. Register Gravelio there as an application
      with the redirect address shown below, then enter the provider's details here.
    </p>
    @if (providers.error()) {
      <p class="error" role="alert" i18n>Could not load the providers.</p>
    }
    <table
      mat-table
      [dataSource]="providers.value()?.items ?? []"
      aria-label="Sign-in providers"
      i18n-aria-label
    >
      <ng-container matColumnDef="name">
        <th mat-header-cell *matHeaderCellDef i18n>Name</th>
        <th mat-cell scope="row" *matCellDef="let p">{{ p.name }}</th>
      </ng-container>
      <ng-container matColumnDef="issuer">
        <th mat-header-cell *matHeaderCellDef i18n>Issuer</th>
        <td mat-cell *matCellDef="let p">{{ p.issuer }}</td>
      </ng-container>
      <ng-container matColumnDef="status">
        <th mat-header-cell *matHeaderCellDef i18n>Status</th>
        <td mat-cell *matCellDef="let p">{{ p.enabled ? enabledLabel : disabledLabel }}</td>
      </ng-container>
      <ng-container matColumnDef="actions">
        <th mat-header-cell *matHeaderCellDef>
          <span class="cdk-visually-hidden" i18n>Actions</span>
        </th>
        <td mat-cell *matCellDef="let p">
          <button mat-button type="button" [attr.aria-label]="editLabel(p)" (click)="edit(p)" i18n>
            Edit
          </button>
          <button
            mat-button
            type="button"
            [attr.aria-label]="deleteLabel(p)"
            (click)="confirmDelete(p)"
            i18n
          >
            Delete
          </button>
        </td>
      </ng-container>
      <tr mat-header-row *matHeaderRowDef="columns"></tr>
      <tr mat-row *matRowDef="let row; columns: columns"></tr>
      @if (!providers.isLoading()) {
        <tr class="mat-mdc-row" *matNoDataRow>
          <td class="mat-mdc-cell" [attr.colspan]="columns.length" i18n>
            No providers yet: sign-in is by password only.
          </td>
        </tr>
      }
    </table>

    <h2>{{ editing() ? editTitle : addTitle }}</h2>
    <mat-card appearance="outlined">
      <mat-card-content>
        <form novalidate (submit)="save($event)">
          <mat-form-field>
            <mat-label i18n>Name shown on the sign-in button</mat-label>
            <input matInput [formField]="f.name" />
          </mat-form-field>
          <mat-form-field>
            <mat-label i18n>Address name</mat-label>
            <input matInput autocomplete="off" [formField]="f.slug" />
            <mat-hint i18n
              >Lowercase letters, digits and hyphens. It cannot be changed later.</mat-hint
            >
          </mat-form-field>
          <mat-form-field class="wide">
            <mat-label i18n>Issuer</mat-label>
            <input matInput type="url" autocomplete="off" [formField]="f.issuer" />
            <mat-hint i18n>For example https://auth.example.com/realms/home</mat-hint>
          </mat-form-field>
          <mat-form-field>
            <mat-label i18n>Client ID</mat-label>
            <input matInput autocomplete="off" [formField]="f.client_id" />
          </mat-form-field>
          <mat-form-field>
            <mat-label i18n>Client secret</mat-label>
            <input
              matInput
              type="password"
              autocomplete="new-password"
              [formField]="f.client_secret"
            />
            @if (editing()?.has_secret) {
              <mat-hint i18n>Leave empty to keep the stored secret.</mat-hint>
            }
          </mat-form-field>
          <mat-form-field class="wide">
            <mat-label i18n>Scopes</mat-label>
            <input matInput autocomplete="off" [formField]="f.scopes" />
          </mat-form-field>
          <mat-checkbox [formField]="f.enabled" i18n>Enabled</mat-checkbox>
          <mat-checkbox [formField]="f.auto_create_users" i18n>
            Create an account for people who sign in for the first time (only for an email the
            provider has verified and that no account uses)
          </mat-checkbox>
          @if (slug(); as s) {
            <p class="wide" i18n>
              Redirect address to register with the provider:
              <code>{{ redirect(s) }}</code>
            </p>
          }
          @if (error()) {
            <p class="error wide" role="alert">{{ error() }}</p>
          }
          <div class="wide">
            <button mat-flat-button type="submit" [disabled]="f().submitting()">
              {{ editing() ? saveLabel : addLabel }}
            </button>
            @if (editing()) {
              <button mat-button type="button" (click)="reset()" i18n>Cancel</button>
            }
          </div>
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
      grid-template-columns: repeat(auto-fill, minmax(260px, 1fr));
      gap: 8px 16px;
      padding-top: 16px;
    }
    .wide {
      grid-column: 1 / -1;
    }
    .error {
      color: var(--mat-sys-error);
      margin: 0;
    }
  `,
})
export class SsoProviders {
  private readonly http = inject(HttpClient);
  private readonly dialog = inject(MatDialog);
  private readonly snack = inject(MatSnackBar);

  protected readonly columns = ['name', 'issuer', 'status', 'actions'];
  protected readonly providers = httpResource<Paged<SsoProvider>>(() => ({
    url: '/api/v1/sso-providers',
    params: { page_size: 100 },
  }));
  protected readonly enabledLabel = $localize`Enabled`;
  protected readonly disabledLabel = $localize`Disabled`;
  protected readonly addTitle = $localize`Add a provider`;
  protected readonly editTitle = $localize`Edit the provider`;
  protected readonly addLabel = $localize`Add provider`;
  protected readonly saveLabel = $localize`Save provider`;

  protected readonly editing = signal<SsoProvider | null>(null);
  protected readonly error = signal('');
  private readonly model = signal({ ...EMPTY });
  protected readonly f = form(this.model, (p) => {
    required(p.name, { message: $localize`Enter a name` });
    required(p.slug, { message: $localize`Enter an address name` });
    // The address name names the connections people made: it is fixed once the provider exists.
    readonly(p.slug, () => this.editing() !== null);
    required(p.issuer, { message: $localize`Enter the issuer address` });
    required(p.client_id, { message: $localize`Enter the client ID` });
  });

  protected slug(): string {
    return this.model().slug;
  }

  protected redirect(slug: string): string {
    return `${location.origin}/api/v1/auth/sso/${slug}/callback`;
  }

  protected editLabel(p: SsoProvider): string {
    return $localize`Edit ${p.name}`;
  }

  protected deleteLabel(p: SsoProvider): string {
    return $localize`Delete ${p.name}`;
  }

  protected edit(p: SsoProvider): void {
    this.editing.set(p);
    this.model.set({
      name: p.name,
      slug: p.slug,
      issuer: p.issuer,
      client_id: p.client_id,
      client_secret: '',
      scopes: p.scopes,
      enabled: p.enabled,
      auto_create_users: p.auto_create_users,
    });
  }

  protected reset(): void {
    this.editing.set(null);
    this.model.set({ ...EMPTY });
    this.error.set('');
  }

  protected async save(event: Event): Promise<void> {
    event.preventDefault();
    this.error.set('');
    await submit(this.f, async () => {
      try {
        const { client_secret, slug, ...rest } = this.model();
        const secret = client_secret ? { client_secret } : {};
        const current = this.editing();
        if (current) {
          await firstValueFrom(
            this.http.put(`/api/v1/sso-providers/${current.id}`, { ...rest, ...secret }),
          );
        } else {
          await firstValueFrom(
            this.http.post('/api/v1/sso-providers', { ...rest, slug, ...secret }),
          );
        }
        this.reset();
        this.providers.reload();
        this.snack.open($localize`Provider saved`, undefined, { duration: 4000 });
      } catch (err) {
        this.error.set(problemMessage(err));
      }
    });
  }

  protected confirmDelete(p: SsoProvider): void {
    this.dialog
      .open(ConfirmDialog, {
        data: {
          title: $localize`Delete provider?`,
          message: $localize`${p.name} will be removed, along with the connections people made to it. They can still sign in with their password.`,
          confirm: $localize`Delete`,
        },
      })
      .afterClosed()
      .subscribe(async (confirmed) => {
        if (!confirmed) return;
        try {
          await firstValueFrom(this.http.delete(`/api/v1/sso-providers/${p.id}`));
          this.providers.reload();
        } catch (err) {
          this.snack.open(problemMessage(err), $localize`Dismiss`, { duration: 8000 });
        }
      });
  }
}
