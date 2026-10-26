import { BulkImport } from '../components/bulk-import';
import { HttpClient, httpResource } from '@angular/common/http';
import { Component, computed, inject, input, signal } from '@angular/core';
import { FormField, form, submit } from '@angular/forms/signals';
import { Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { OUTLINE_FIELDS } from '../core/outline-fields';
import { MatIconModule } from '@angular/material/icon';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Activity, Paged, problemMessage } from '../core/api';
import { ActivityTypes } from '../core/activity-types';
import { HealthToday } from '../components/health-today';
import { AuthService } from '../core/auth';
import {
  PACE_TYPES,
  formatDateTime,
  formatDistance,
  formatDuration,
  formatPace,
  formatSpeed,
} from '../core/format';

const PAGE_SIZE = 20;

const NO_FILTERS = { q: '', type: '', from: '', to: '', sort: 'start_time', order: 'desc' };

interface StravaImportResult {
  imported: number;
  skipped: number;
  failed: number;
}

@Component({
  selector: 'app-activities',
  providers: [OUTLINE_FIELDS],
  imports: [
    BulkImport,
    HealthToday,
    RouterLink,
    FormField,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatButtonToggleModule,
    MatCardModule,
    MatIconModule,
    MatPaginatorModule,
    MatProgressBarModule,
  ],
  template: `
    <header>
      <h1 i18n>Activities</h1>
      <div class="actions">
        <input
          #file
          class="cdk-visually-hidden"
          type="file"
          accept=".gpx,.tcx,.fit"
          aria-label="Activity file"
          i18n-aria-label
          tabindex="-1"
          (change)="upload(file)"
        />
        <input
          #zip
          class="cdk-visually-hidden"
          type="file"
          accept=".zip"
          aria-label="Strava export zip"
          i18n-aria-label
          tabindex="-1"
          (change)="importStrava(zip)"
        />
        @if (scope() === 'mine') {
          <button mat-button type="button" [disabled]="uploading()" (click)="zip.click()">
            <mat-icon aria-hidden="true">downloading</mat-icon
            ><span i18n>Import Strava export</span>
          </button>
          <button mat-flat-button type="button" [disabled]="uploading()" (click)="file.click()">
            <mat-icon aria-hidden="true">upload</mat-icon><span i18n>Upload activity</span>
          </button>
        }
      </div>
    </header>
    <div class="scope">
      <mat-button-toggle-group
        [value]="scope()"
        (change)="setScope($event.value)"
        aria-label="Activity feed"
        i18n-aria-label
      >
        <mat-button-toggle value="mine" i18n>Mine</mat-button-toggle>
        <mat-button-toggle value="following" i18n>Following</mat-button-toggle>
      </mat-button-toggle-group>
    </div>
    @if (scope() === 'mine') {
      <app-health-today />
      <app-bulk-import (finished)="page.reload()" />
      <form class="filters" novalidate (submit)="applyFilters($event)">
        <mat-form-field>
          <mat-label i18n>Search by name</mat-label>
          <input matInput type="search" [formField]="ff.q" />
        </mat-form-field>
        <mat-form-field>
          <mat-label i18n>Activity type</mat-label>
          <mat-select [formField]="ff.type">
            <mat-option value="" i18n>All types</mat-option>
            @for (t of types.options(); track t.id) {
              <mat-option [value]="'' + t.id">{{ t.name }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
        <mat-form-field>
          <mat-label i18n>From</mat-label>
          <input matInput type="date" [formField]="ff.from" />
        </mat-form-field>
        <mat-form-field>
          <mat-label i18n>To</mat-label>
          <input matInput type="date" [formField]="ff.to" />
        </mat-form-field>
        <mat-form-field>
          <mat-label i18n>Sort by</mat-label>
          <mat-select [formField]="ff.sort">
            @for (s of sortKeys; track s.id) {
              <mat-option [value]="s.id">{{ s.label }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
        <mat-form-field>
          <mat-label i18n>Order</mat-label>
          <mat-select [formField]="ff.order">
            <mat-option value="desc" i18n>Highest or newest first</mat-option>
            <mat-option value="asc" i18n>Lowest or oldest first</mat-option>
          </mat-select>
        </mat-form-field>
        <div class="filter-actions">
          <button mat-flat-button type="submit" i18n>Apply</button>
          <button mat-button type="button" (click)="clearFilters()" i18n>Clear</button>
          @if (gear()) {
            <span i18n>Only activities with the selected gear.</span>
          }
        </div>
      </form>
    }
    @if (uploading()) {
      <mat-progress-bar mode="indeterminate" aria-label="Uploading" i18n-aria-label />
    }
    @if (page.error()) {
      <p class="error" role="alert" i18n>Could not load activities.</p>
    }
    <ul class="list">
      @for (a of page.value()?.items; track a.id) {
        <li>
          <mat-card class="topo" appearance="outlined">
            <mat-card-header>
              <h2 mat-card-title>
                <a [routerLink]="['/activities', a.id]">{{ a.name }}</a>
                @if (a.is_hidden) {
                  <span class="hidden-badge" i18n>Hidden</span>
                }
              </h2>
              <p mat-card-subtitle>
                @if (scope() === 'following') {
                  <a [routerLink]="['/people', a.owner.id]">{{ a.owner.name }}</a> ·
                }
                {{ types.name(a.activity_type) }} · {{ when(a.start_time, a.timezone) }}
              </p>
            </mat-card-header>
            <mat-card-content>
              <dl>
                <div>
                  <dt i18n>Distance</dt>
                  <dd>{{ distance(a.distance) }}</dd>
                </div>
                <div>
                  <dt i18n>Time</dt>
                  <dd>{{ duration(a.total_timer_time) }}</dd>
                </div>
                <div>
                  @if (isPace(a.activity_type)) {
                    <dt i18n>Pace</dt>
                    <dd>{{ pace(a.pace) }}</dd>
                  } @else {
                    <dt i18n>Avg speed</dt>
                    <dd>{{ speed(a.average_speed) }}</dd>
                  }
                </div>
              </dl>
            </mat-card-content>
          </mat-card>
        </li>
      } @empty {
        @if (!page.isLoading()) {
          @if (scope() === 'following') {
            <li><p i18n>No activities from people you follow yet.</p></li>
          } @else {
            <li><p i18n>No activities yet. Upload a GPX, TCX or FIT file to get started.</p></li>
          }
        }
      }
    </ul>
    <mat-paginator
      [length]="page.value()?.total ?? 0"
      [pageSize]="pageSize"
      [pageIndex]="pageIndex() - 1"
      [hidePageSize]="true"
      (page)="onPage($event)"
    />
  `,
  styles: `
    header {
      display: flex;
      align-items: center;
      justify-content: space-between;
    }
    .actions {
      display: flex;
      align-items: center;
      gap: 8px;
    }
    .scope {
      margin: 8px 0 16px;
    }
    .filters {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(200px, 1fr));
      gap: 4px 16px;
    }
    .filter-actions {
      grid-column: 1 / -1;
      display: flex;
      align-items: center;
      gap: 8px;
      margin-bottom: 8px;
    }
    .list {
      list-style: none;
      padding: 0;
      display: grid;
      gap: 12px;
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
    }
    .hidden-badge {
      margin-inline-start: 8px;
      padding: 2px 8px;
      border-radius: var(--mat-sys-corner-full);
      background: var(--mat-sys-surface-container-high);
      color: var(--mat-sys-on-surface-variant);
      font: var(--mat-sys-label-small);
    }
  `,
})
export class Activities {
  private http = inject(HttpClient);
  private snack = inject(MatSnackBar);
  private auth = inject(AuthService);
  private router = inject(Router);
  protected readonly types = inject(ActivityTypes);

  protected readonly pageSize = PAGE_SIZE;
  protected readonly pageIndex = signal(1);
  protected readonly uploading = signal(false);
  protected readonly scope = signal<'mine' | 'following'>('mine');
  /** From the address (?gear=ID): the gear page links here. */
  readonly gear = input<string>();

  protected readonly sortKeys = [
    { id: 'start_time', label: $localize`Date` },
    { id: 'name', label: $localize`Name` },
    { id: 'type', label: $localize`Type` },
    { id: 'distance', label: $localize`Distance` },
    { id: 'duration', label: $localize`Moving time` },
    { id: 'elevation', label: $localize`Elevation gain` },
    { id: 'calories', label: $localize`Calories` },
    { id: 'average_hr', label: $localize`Average heart rate` },
  ];
  // The form edits a draft; the list only reloads when the filters are applied, not on every keystroke.
  private readonly draft = signal({ ...NO_FILTERS });
  protected readonly ff = form(this.draft);
  private readonly applied = signal({ ...NO_FILTERS });

  protected readonly page = httpResource<Paged<Activity>>(() => {
    if (this.scope() === 'following') {
      return {
        url: '/api/v1/activities/feed',
        params: { page: this.pageIndex(), page_size: PAGE_SIZE },
      };
    }
    const f = this.applied();
    return {
      url: '/api/v1/activities',
      params: {
        page: this.pageIndex(),
        page_size: PAGE_SIZE,
        sort: f.sort,
        order: f.order,
        ...(f.q.trim() ? { q: f.q.trim() } : {}),
        ...(f.type ? { type: f.type } : {}),
        ...(f.from ? { from: f.from } : {}),
        ...(f.to ? { to: f.to } : {}),
        ...(this.gear() ? { gear_id: this.gear()! } : {}),
      },
    };
  });

  private readonly units = computed(() => this.auth.user()?.units ?? 'metric');
  protected readonly distance = (m: number) => formatDistance(m, this.units());
  protected readonly duration = formatDuration;
  protected readonly pace = (p: number | null) => formatPace(p, this.units());
  protected readonly speed = (s: number | null) => formatSpeed(s, this.units());
  protected readonly when = formatDateTime;
  protected readonly isPace = (type: number) => PACE_TYPES.has(type);

  protected onPage(event: PageEvent): void {
    this.pageIndex.set(event.pageIndex + 1);
  }

  protected async applyFilters(event: Event): Promise<void> {
    event.preventDefault();
    await submit(this.ff, async () => {
      this.applied.set({ ...this.draft() });
      this.pageIndex.set(1);
    });
  }

  protected clearFilters(): void {
    this.draft.set({ ...NO_FILTERS });
    this.applied.set({ ...NO_FILTERS });
    this.pageIndex.set(1);
    if (this.gear()) void this.router.navigate([], { queryParams: { gear: null } });
  }

  protected setScope(value: string): void {
    this.scope.set(value === 'following' ? 'following' : 'mine');
    this.pageIndex.set(1);
  }

  protected async upload(input: HTMLInputElement): Promise<void> {
    const file = input.files?.item(0);
    if (!file) return;
    const body = new FormData();
    body.append('file', file);
    this.uploading.set(true);
    try {
      await firstValueFrom(this.http.post('/api/v1/activities/upload', body));
      this.snack.open($localize`Activity uploaded`, undefined, { duration: 4000 });
      this.pageIndex.set(1);
      this.page.reload();
    } catch (err) {
      this.snack.open(problemMessage(err), $localize`Dismiss`, { duration: 8000 });
    } finally {
      input.value = '';
      this.uploading.set(false);
    }
  }

  protected async importStrava(input: HTMLInputElement): Promise<void> {
    const file = input.files?.item(0);
    if (!file) return;
    const body = new FormData();
    body.append('file', file);
    this.uploading.set(true);
    try {
      const result = await firstValueFrom(
        this.http.post<StravaImportResult>('/api/v1/activities/import/strava', body),
      );
      this.snack.open(
        $localize`Strava import: ${result.imported} new, ${result.skipped} already present, ${result.failed} failed.`,
        undefined,
        { duration: 8000 },
      );
      if (result.imported > 0) {
        this.pageIndex.set(1);
        this.page.reload();
      }
    } catch (err) {
      this.snack.open(problemMessage(err), $localize`Dismiss`, { duration: 8000 });
    } finally {
      input.value = '';
      this.uploading.set(false);
    }
  }
}
