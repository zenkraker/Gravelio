import { HttpClient, httpResource } from '@angular/common/http';
import { Component, computed, inject, input, linkedSignal, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { FormField, form, required, submit } from '@angular/forms/signals';
import { firstValueFrom } from 'rxjs';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTableModule } from '@angular/material/table';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { OUTLINE_FIELDS } from '../core/outline-fields';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Gear, GearComponent, problemMessage } from '../core/api';
import { AuthService } from '../core/auth';
import { GEAR_TYPES, componentLabel, formatMoney, gearTypeLabel } from '../core/gear';
import {
  displayToMeters,
  distanceUnitLabel,
  formatDistance,
  metersToDisplay,
} from '../core/format';
import { ConfirmDialog } from '../components/confirm-dialog';

const emptyComponent = {
  type: '',
  brand: '',
  model: '',
  purchaseDate: '',
  retiredDate: '',
  expectedDistance: '',
  purchaseValue: '',
};

@Component({
  selector: 'app-gear-detail',
  providers: [OUTLINE_FIELDS],
  imports: [
    FormField,
    RouterLink,
    MatButtonModule,
    MatCardModule,
    MatCheckboxModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    MatSelectModule,
    MatTableModule,
  ],
  template: `
    <a mat-button routerLink="/gear"
      ><mat-icon aria-hidden="true">arrow_back</mat-icon><span i18n>All gear</span></a
    >
    @if (gear.error()) {
      <p class="error" role="alert" i18n>This gear could not be found.</p>
    }
    @if (gear.value(); as g) {
      <header>
        <div>
          <h1>{{ g.nickname }}</h1>
          <p>
            {{ typeLabel(g.gear_type) }}
            @if (!g.active) {
              · <strong i18n>Retired</strong>
            }
          </p>
        </div>
        <button mat-button type="button" (click)="confirmDeleteGear(g)">
          <mat-icon aria-hidden="true">delete</mat-icon><span i18n>Delete</span>
        </button>
      </header>
      <dl class="stats">
        <div>
          <dt i18n>Distance</dt>
          <dd>{{ distance(g.total_distance) }}</dd>
        </div>
        <div>
          <dt i18n>Activities</dt>
          <dd>{{ g.activity_count }}</dd>
        </div>
        @if (g.purchase_value !== null) {
          <div>
            <dt i18n>Purchase value</dt>
            <dd>{{ money(g.purchase_value) }}</dd>
          </div>
        }
      </dl>

      <h2 i18n>Details</h2>
      <mat-card appearance="outlined">
        <mat-card-content>
          <form novalidate (submit)="saveGear($event)">
            <mat-form-field
              ><mat-label i18n>Nickname</mat-label><input matInput [formField]="gf.nickname"
            /></mat-form-field>
            <mat-form-field>
              <mat-label i18n>Type</mat-label>
              <mat-select [formField]="gf.gearType">
                @for (t of types; track t.id) {
                  <mat-option [value]="'' + t.id">{{ t.label }}</mat-option>
                }
              </mat-select>
            </mat-form-field>
            <mat-form-field
              ><mat-label i18n>Brand</mat-label><input matInput [formField]="gf.brand"
            /></mat-form-field>
            <mat-form-field
              ><mat-label i18n>Model</mat-label><input matInput [formField]="gf.model"
            /></mat-form-field>
            <mat-form-field
              ><mat-label>{{ initialLabel() }}</mat-label
              ><input matInput type="number" step="any" [formField]="gf.initialDistance"
            /></mat-form-field>
            <mat-form-field
              ><mat-label i18n>Purchase value</mat-label
              ><input matInput type="number" step="any" [formField]="gf.purchaseValue"
            /></mat-form-field>
            <mat-checkbox [formField]="gf.active" i18n
              >In use (untick when you retire it)</mat-checkbox
            >
            @if (gearError()) {
              <p class="error" role="alert">{{ gearError() }}</p>
            }
            <button mat-flat-button type="submit" [disabled]="gf().submitting()" i18n>Save</button>
          </form>
        </mat-card-content>
      </mat-card>

      <h2 i18n>Components</h2>
      @if (allowedTypes().length === 0) {
        <p i18n>This kind of gear has no components.</p>
      } @else {
        @if (components.value()?.length) {
          <table
            mat-table
            [dataSource]="components.value() ?? []"
            aria-label="Components of this gear"
            i18n-aria-label
          >
            <ng-container matColumnDef="component">
              <th mat-header-cell *matHeaderCellDef i18n>Component</th>
              <th mat-cell scope="row" *matCellDef="let c">
                {{ label(c.type) }}
                @if (c.brand || c.model) {
                  <br /><small>{{ c.brand }} {{ c.model }}</small>
                }
              </th>
            </ng-container>
            <ng-container matColumnDef="bought">
              <th mat-header-cell *matHeaderCellDef i18n>Bought</th>
              <td mat-cell *matCellDef="let c">{{ c.purchase_date }}</td>
            </ng-container>
            <ng-container matColumnDef="retired">
              <th mat-header-cell *matHeaderCellDef i18n>Retired</th>
              <td mat-cell *matCellDef="let c">{{ c.retired_date ?? '—' }}</td>
            </ng-container>
            <ng-container matColumnDef="wear">
              <th mat-header-cell *matHeaderCellDef i18n>Wear</th>
              <td mat-cell *matCellDef="let c">
                {{ distance(c.used_distance) }}
                @if (c.expected_distance !== null) {
                  <span> / {{ distance(c.expected_distance) }}</span>
                  <mat-progress-bar
                    mode="determinate"
                    [value]="wearPercent(c)"
                    [attr.aria-label]="wearLabel(c)"
                  />
                }
              </td>
            </ng-container>
            <ng-container matColumnDef="actions">
              <th mat-header-cell *matHeaderCellDef i18n>Actions</th>
              <td mat-cell *matCellDef="let c">
                <button
                  mat-button
                  type="button"
                  (click)="startEdit(c)"
                  [attr.aria-label]="editLabel(c)"
                  i18n
                >
                  Edit
                </button>
                <button
                  mat-button
                  type="button"
                  (click)="confirmDeleteComponent(c)"
                  [attr.aria-label]="deleteLabel(c)"
                  i18n
                >
                  Delete
                </button>
              </td>
            </ng-container>
            <tr mat-header-row *matHeaderRowDef="columns"></tr>
            <tr mat-row *matRowDef="let row; columns: columns"></tr>
          </table>
        }

        <h3>{{ editing() ? editTitle : addTitle }}</h3>
        <mat-card appearance="outlined">
          <mat-card-content>
            <form novalidate (submit)="saveComponent($event)">
              <mat-form-field>
                <mat-label i18n>Component</mat-label>
                <mat-select [formField]="cf.type">
                  <mat-option value="" i18n>Choose…</mat-option>
                  @for (t of allowedTypes(); track t) {
                    <mat-option [value]="t">{{ label(t) }}</mat-option>
                  }
                </mat-select>
              </mat-form-field>
              <mat-form-field
                ><mat-label i18n>Brand</mat-label><input matInput [formField]="cf.brand"
              /></mat-form-field>
              <mat-form-field
                ><mat-label i18n>Model</mat-label><input matInput [formField]="cf.model"
              /></mat-form-field>
              <mat-form-field
                ><mat-label i18n>Bought on</mat-label
                ><input matInput type="date" [formField]="cf.purchaseDate"
              /></mat-form-field>
              <mat-form-field
                ><mat-label i18n>Retired on</mat-label
                ><input matInput type="date" [formField]="cf.retiredDate" /><mat-hint i18n
                  >Leave empty while it is fitted</mat-hint
                ></mat-form-field
              >
              <mat-form-field
                ><mat-label>{{ expectedLabel() }}</mat-label
                ><input matInput type="number" step="any" [formField]="cf.expectedDistance"
              /></mat-form-field>
              <mat-form-field
                ><mat-label i18n>Purchase value</mat-label
                ><input matInput type="number" step="any" [formField]="cf.purchaseValue"
              /></mat-form-field>
              @if (componentError()) {
                <p class="error" role="alert">{{ componentError() }}</p>
              }
              <div class="actions">
                <button mat-flat-button type="submit" [disabled]="cf().submitting()" i18n>
                  Save component
                </button>
                @if (editing()) {
                  <button mat-button type="button" (click)="cancelEdit()" i18n>Cancel</button>
                }
              </div>
            </form>
          </mat-card-content>
        </mat-card>
      }
    }
  `,
  styles: `
    header {
      display: flex;
      justify-content: space-between;
      align-items: start;
    }
    .stats {
      display: flex;
      gap: 32px;
      margin: 16px 0;
      flex-wrap: wrap;
    }
    dt {
      font: var(--mat-sys-label-medium);
      color: var(--mat-sys-on-surface-variant);
    }
    dd {
      margin: 0;
      font: var(--mat-sys-title-large);
    }
    form {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(240px, 1fr));
      gap: 8px 16px;
      align-items: start;
      padding-top: 8px;
    }
    mat-checkbox {
      align-self: center;
    }
    button[type='submit'],
    .error,
    .actions {
      grid-column: 1 / -1;
      justify-self: start;
    }
    .error {
      color: var(--mat-sys-error);
      margin: 0;
    }
    table {
      width: 100%;
      margin-bottom: 16px;
    }
    mat-progress-bar {
      width: 160px;
    }
  `,
})
export class GearDetail {
  readonly id = input.required<string>(); // route param

  private http = inject(HttpClient);
  private router = inject(Router);
  private dialog = inject(MatDialog);
  private snack = inject(MatSnackBar);
  private auth = inject(AuthService);

  protected readonly types = GEAR_TYPES;
  protected readonly typeLabel = gearTypeLabel;
  protected readonly label = componentLabel;
  protected readonly addTitle = $localize`Add a component`;
  protected readonly editTitle = $localize`Edit component`;

  protected readonly gear = httpResource<Gear>(() => `/api/v1/gears/${this.id()}`);
  protected readonly components = httpResource<GearComponent[]>(
    () => `/api/v1/gears/${this.id()}/components`,
  );
  private readonly typesByGear = httpResource<Record<string, string[]>>(
    () => '/api/v1/gears/component-types',
  );
  protected readonly allowedTypes = computed(
    () => this.typesByGear.value()?.[String(this.gear.value()?.gear_type)] ?? [],
  );

  private readonly units = computed(() => this.auth.user()?.units ?? 'metric');
  private readonly currency = computed(() => this.auth.user()?.currency ?? 'euro');
  protected readonly distance = (m: number) => formatDistance(m, this.units());
  protected readonly money = (v: number) => formatMoney(v, this.currency());
  protected readonly initialLabel = computed(
    () => $localize`Initial distance (${distanceUnitLabel(this.units())})`,
  );
  protected readonly expectedLabel = computed(
    () => $localize`Replace after (${distanceUnitLabel(this.units())})`,
  );
  protected readonly columns = ['component', 'bought', 'retired', 'wear', 'actions'];
  protected readonly wearPercent = (c: GearComponent) =>
    Math.min(100, (c.used_distance / (c.expected_distance || 1)) * 100);
  protected readonly wearLabel = (c: GearComponent) => $localize`Wear of ${this.label(c.type)}`;
  protected readonly editLabel = (c: GearComponent) => $localize`Edit ${this.label(c.type)}`;
  protected readonly deleteLabel = (c: GearComponent) => $localize`Delete ${this.label(c.type)}`;

  protected readonly gearError = signal('');
  protected readonly componentError = signal('');
  protected readonly editing = signal<GearComponent | null>(null);

  private readonly gearModel = linkedSignal(() => {
    const g = this.gear.value();
    return {
      nickname: g?.nickname ?? '',
      gearType: String(g?.gear_type ?? 1),
      brand: g?.brand ?? '',
      model: g?.model ?? '',
      initialDistance: String(metersToDisplay(g?.initial_distance ?? 0, this.units())),
      purchaseValue: g?.purchase_value == null ? '' : String(g.purchase_value),
      active: g?.active ?? true,
    };
  });
  protected readonly gf = form(this.gearModel, (p) => {
    required(p.nickname, { message: $localize`Enter a nickname` });
  });

  private readonly componentModel = signal({ ...emptyComponent });
  protected readonly cf = form(this.componentModel, (p) => {
    required(p.type, { message: $localize`Choose a component` });
    required(p.purchaseDate, { message: $localize`Enter the purchase date` });
  });

  protected async saveGear(event: Event): Promise<void> {
    event.preventDefault();
    this.gearError.set('');
    await submit(this.gf, async () => {
      try {
        const v = this.gearModel();
        await firstValueFrom(
          this.http.put(`/api/v1/gears/${this.id()}`, {
            nickname: v.nickname,
            gear_type: Number(v.gearType),
            brand: v.brand || null,
            model: v.model || null,
            active: v.active,
            initial_distance: displayToMeters(Number(v.initialDistance) || 0, this.units()),
            purchase_value: v.purchaseValue === '' ? null : Number(v.purchaseValue),
          }),
        );
        this.gear.reload();
        this.snack.open($localize`Gear saved`, undefined, { duration: 4000 });
      } catch (err) {
        this.gearError.set(problemMessage(err));
      }
    });
  }

  protected startEdit(c: GearComponent): void {
    this.editing.set(c);
    this.componentError.set('');
    this.componentModel.set({
      type: c.type,
      brand: c.brand ?? '',
      model: c.model ?? '',
      purchaseDate: c.purchase_date,
      retiredDate: c.retired_date ?? '',
      expectedDistance:
        c.expected_distance == null
          ? ''
          : String(metersToDisplay(c.expected_distance, this.units())),
      purchaseValue: c.purchase_value == null ? '' : String(c.purchase_value),
    });
  }

  protected cancelEdit(): void {
    this.editing.set(null);
    this.componentModel.set({ ...emptyComponent });
  }

  protected async saveComponent(event: Event): Promise<void> {
    event.preventDefault();
    this.componentError.set('');
    await submit(this.cf, async () => {
      try {
        const v = this.componentModel();
        const body = {
          type: v.type,
          brand: v.brand || null,
          model: v.model || null,
          purchase_date: v.purchaseDate,
          retired_date: v.retiredDate || null,
          expected_distance:
            v.expectedDistance === ''
              ? null
              : displayToMeters(Number(v.expectedDistance), this.units()),
          purchase_value: v.purchaseValue === '' ? null : Number(v.purchaseValue),
        };
        const editing = this.editing();
        const url = `/api/v1/gears/${this.id()}/components`;
        await firstValueFrom(
          editing ? this.http.put(`${url}/${editing.id}`, body) : this.http.post(url, body),
        );
        this.cancelEdit();
        this.components.reload();
        this.snack.open($localize`Component saved`, undefined, { duration: 4000 });
      } catch (err) {
        this.componentError.set(problemMessage(err));
      }
    });
  }

  protected confirmDeleteComponent(c: GearComponent): void {
    this.confirm(
      $localize`Delete component?`,
      $localize`${this.label(c.type)} will be removed from this gear.`,
      async () => {
        await firstValueFrom(this.http.delete(`/api/v1/gears/${this.id()}/components/${c.id}`));
        this.components.reload();
      },
    );
  }

  protected confirmDeleteGear(g: Gear): void {
    this.confirm(
      $localize`Delete gear?`,
      $localize`“${g.nickname}” and its components will be deleted. Your activities stay, without this gear.`,
      async () => {
        await firstValueFrom(this.http.delete(`/api/v1/gears/${g.id}`));
        await this.router.navigateByUrl('/gear');
      },
    );
  }

  private confirm(title: string, message: string, action: () => Promise<void>): void {
    this.dialog
      .open(ConfirmDialog, { data: { title, message, confirm: $localize`Delete` } })
      .afterClosed()
      .subscribe(async (confirmed) => {
        if (!confirmed) return;
        try {
          await action();
        } catch (err) {
          this.snack.open(problemMessage(err), $localize`Dismiss`, { duration: 8000 });
        }
      });
  }
}
