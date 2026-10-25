import { HttpClient, httpResource } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { FormField, form, required, submit } from '@angular/forms/signals';
import { firstValueFrom } from 'rxjs';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatSnackBar } from '@angular/material/snack-bar';
import { DefaultGearSlot, Gear, Paged, problemMessage } from '../core/api';
import { ActivityTypes } from '../core/activity-types';
import { AuthService } from '../core/auth';
import { GEAR_TYPES, formatMoney, gearTypeLabel } from '../core/gear';
import { displayToMeters, distanceUnitLabel, formatDistance } from '../core/format';

const PAGE_SIZE = 12;
const emptyGear = {
  nickname: '',
  gearType: '1',
  brand: '',
  model: '',
  initialDistance: '0',
  purchaseValue: '',
};

@Component({
  selector: 'app-gears',
  imports: [
    FormField,
    RouterLink,
    MatButtonModule,
    MatCardModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatPaginatorModule,
  ],
  template: `
    <header>
      <h1 i18n>Gear</h1>
      <button
        mat-flat-button
        type="button"
        (click)="adding.set(!adding())"
        [attr.aria-expanded]="adding()"
        aria-controls="new-gear"
      >
        <mat-icon aria-hidden="true">add</mat-icon><span i18n>Add gear</span>
      </button>
    </header>

    @if (adding()) {
      <mat-card id="new-gear" appearance="outlined">
        <mat-card-content>
          <h2 i18n>New gear</h2>
          <form novalidate (submit)="create($event)">
            <mat-form-field
              ><mat-label i18n>Nickname</mat-label><input matInput [formField]="f.nickname"
            /></mat-form-field>
            <mat-form-field>
              <mat-label i18n>Type</mat-label>
              <select matNativeControl [formField]="f.gearType">
                @for (t of types; track t.id) {
                  <option [value]="t.id">{{ t.label }}</option>
                }
              </select>
            </mat-form-field>
            <mat-form-field
              ><mat-label i18n>Brand</mat-label><input matInput [formField]="f.brand"
            /></mat-form-field>
            <mat-form-field
              ><mat-label i18n>Model</mat-label><input matInput [formField]="f.model"
            /></mat-form-field>
            <mat-form-field>
              <mat-label>{{ distanceLabel() }}</mat-label>
              <input matInput type="number" step="any" [formField]="f.initialDistance" />
              <mat-hint i18n>Distance already covered before you added it</mat-hint>
            </mat-form-field>
            <mat-form-field
              ><mat-label i18n>Purchase value</mat-label
              ><input matInput type="number" step="any" [formField]="f.purchaseValue"
            /></mat-form-field>
            @if (error()) {
              <p class="error" role="alert">{{ error() }}</p>
            }
            <button mat-flat-button type="submit" [disabled]="f().submitting()" i18n>
              Save gear
            </button>
          </form>
        </mat-card-content>
      </mat-card>
    }

    <div class="filters" role="search">
      <mat-form-field>
        <mat-label i18n>Search by nickname</mat-label>
        <input matInput type="search" #search [value]="query()" (input)="setQuery(search.value)" />
      </mat-form-field>
      <mat-form-field>
        <mat-label i18n>Type</mat-label>
        <select matNativeControl #typeSelect (change)="setType(typeSelect.value)">
          <option value="" [selected]="typeFilter() === ''" i18n>All types</option>
          @for (t of types; track t.id) {
            <option [value]="t.id" [selected]="typeFilter() === '' + t.id">{{ t.label }}</option>
          }
        </select>
      </mat-form-field>
      <label class="check"
        ><input
          type="checkbox"
          #retired
          [checked]="showRetired()"
          (change)="setShowRetired(retired.checked)"
        /><span i18n>Show retired</span></label
      >
    </div>

    @if (gears.error()) {
      <p class="error" role="alert" i18n>Could not load your gear.</p>
    }
    <ul class="list">
      @for (g of gears.value()?.items; track g.id) {
        <li>
          <mat-card appearance="outlined">
            <mat-card-header>
              <h2 mat-card-title>
                <a [routerLink]="['/gear', g.id]">{{ g.nickname }}</a>
              </h2>
              <p mat-card-subtitle>
                {{ typeLabel(g.gear_type) }}
                @if (g.brand || g.model) {
                  · {{ g.brand }} {{ g.model }}
                }
                @if (!g.active) {
                  · <strong i18n>Retired</strong>
                }
              </p>
            </mat-card-header>
            <mat-card-content>
              <dl>
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
            </mat-card-content>
          </mat-card>
        </li>
      } @empty {
        @if (!gears.isLoading()) {
          <li>
            <p i18n>
              No gear yet. Add a bike, shoes or other equipment to track how far you use it.
            </p>
          </li>
        }
      }
    </ul>
    <mat-paginator
      [length]="gears.value()?.total ?? 0"
      [pageSize]="pageSize"
      [pageIndex]="pageIndex() - 1"
      [hidePageSize]="true"
      (page)="onPage($event)"
    />

    <h2 i18n>Default gear</h2>
    <p i18n>Gear chosen here is added automatically to the activities you upload.</p>
    <ul class="slots">
      @for (slot of defaults.value(); track slot.activity_type) {
        <li>
          <mat-form-field>
            <mat-label>{{ activityTypes.name(slot.activity_type) }}</mat-label>
            <select matNativeControl #choice (change)="setDefault(slot, choice.value)">
              <option value="" [selected]="slot.gear_id === null" i18n>None</option>
              @for (g of choicesFor(slot); track g.id) {
                <option [value]="g.id" [selected]="g.id === slot.gear_id">{{ g.nickname }}</option>
              }
            </select>
          </mat-form-field>
        </li>
      }
    </ul>
  `,
  styles: `
    header {
      display: flex;
      align-items: center;
      justify-content: space-between;
    }
    form,
    .filters {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(240px, 1fr));
      gap: 8px 16px;
      align-items: start;
      padding-top: 8px;
    }
    button[type='submit'],
    .error {
      grid-column: 1 / -1;
      justify-self: start;
    }
    .check {
      display: flex;
      align-items: center;
      gap: 8px;
      min-height: 56px;
    }
    .list,
    .slots {
      list-style: none;
      padding: 0;
      display: grid;
      gap: 12px;
    }
    .list {
      grid-template-columns: repeat(auto-fill, minmax(320px, 1fr));
    }
    .slots {
      grid-template-columns: repeat(auto-fill, minmax(240px, 1fr));
      gap: 0 16px;
    }
    dl {
      display: flex;
      gap: 32px;
      margin: 0;
    }
    dt {
      font: var(--mat-sys-label-medium);
      color: var(--mat-sys-on-surface-variant);
    }
    dd {
      margin: 0;
      font: var(--mat-sys-title-medium);
    }
    .error {
      color: var(--mat-sys-error);
      margin: 0;
    }
  `,
})
export class Gears {
  private http = inject(HttpClient);
  private snack = inject(MatSnackBar);
  private auth = inject(AuthService);
  protected readonly activityTypes = inject(ActivityTypes);

  protected readonly types = GEAR_TYPES;
  protected readonly pageSize = PAGE_SIZE;
  protected readonly pageIndex = signal(1);
  protected readonly query = signal('');
  protected readonly typeFilter = signal('');
  protected readonly showRetired = signal(false);
  protected readonly adding = signal(false);
  protected readonly error = signal('');

  private readonly units = computed(() => this.auth.user()?.units ?? 'metric');
  private readonly currency = computed(() => this.auth.user()?.currency ?? 'euro');
  protected readonly distanceLabel = computed(
    () => $localize`Initial distance (${distanceUnitLabel(this.units())})`,
  );
  protected readonly distance = (m: number) => formatDistance(m, this.units());
  protected readonly money = (v: number) => formatMoney(v, this.currency());
  protected readonly typeLabel = gearTypeLabel;

  protected readonly gears = httpResource<Paged<Gear>>(() => ({
    url: '/api/v1/gears',
    params: {
      page: this.pageIndex(),
      page_size: PAGE_SIZE,
      ...(this.query().trim() ? { q: this.query().trim() } : {}),
      ...(this.typeFilter() ? { gear_type: this.typeFilter() } : {}),
      ...(this.showRetired() ? {} : { active: true }),
    },
  }));
  protected readonly defaults = httpResource<DefaultGearSlot[]>(() => '/api/v1/gears/defaults');
  private readonly activeGear = httpResource<Paged<Gear>>(() => ({
    url: '/api/v1/gears',
    params: { page_size: 100, active: true },
  }));

  private readonly model = signal({ ...emptyGear });
  protected readonly f = form(this.model, (p) => {
    required(p.nickname, { message: $localize`Enter a nickname` });
  });

  protected choicesFor(slot: DefaultGearSlot) {
    return (this.activeGear.value()?.items ?? []).filter((g) => g.gear_type === slot.gear_type);
  }

  protected setQuery(value: string): void {
    this.query.set(value);
    this.pageIndex.set(1);
  }
  protected setType(value: string): void {
    this.typeFilter.set(value);
    this.pageIndex.set(1);
  }
  protected setShowRetired(value: boolean): void {
    this.showRetired.set(value);
    this.pageIndex.set(1);
  }
  protected onPage(event: PageEvent): void {
    this.pageIndex.set(event.pageIndex + 1);
  }

  protected async create(event: Event): Promise<void> {
    event.preventDefault();
    this.error.set('');
    await submit(this.f, async () => {
      try {
        const v = this.model();
        await firstValueFrom(
          this.http.post('/api/v1/gears', {
            nickname: v.nickname,
            gear_type: Number(v.gearType),
            brand: v.brand || null,
            model: v.model || null,
            initial_distance: displayToMeters(Number(v.initialDistance) || 0, this.units()),
            purchase_value: v.purchaseValue === '' ? null : Number(v.purchaseValue),
          }),
        );
        this.model.set({ ...emptyGear });
        this.adding.set(false);
        this.gears.reload();
        this.activeGear.reload();
        this.snack.open($localize`Gear added`, undefined, { duration: 4000 });
      } catch (err) {
        this.error.set(problemMessage(err));
      }
    });
  }

  protected async setDefault(slot: DefaultGearSlot, value: string): Promise<void> {
    try {
      await firstValueFrom(
        this.http.put(`/api/v1/gears/defaults/${slot.activity_type}`, {
          gear_id: value === '' ? null : Number(value),
        }),
      );
      this.defaults.reload();
    } catch (err) {
      this.snack.open(problemMessage(err), $localize`Dismiss`, { duration: 8000 });
      this.defaults.reload();
    }
  }
}
