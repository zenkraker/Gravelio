import { httpResource } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleChange, MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatTableModule } from '@angular/material/table';
import { ActivityStats, StatsBucket } from '../core/api';
import { AuthService } from '../core/auth';
import { formatDistance, formatDuration, formatElevation } from '../core/format';

type Period = 'week' | 'month' | 'year' | 'lifetime';

/** YYYY-MM-DD in the browser's local calendar, which is the anchor the API expects. */
function iso(date: Date): string {
  return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`;
}

function parse(isoDate: string): Date {
  return new Date(`${isoDate}T00:00:00`);
}

const day = (isoDate: string) => parse(isoDate).getDate();
/** The last day of an exclusive-end bucket. */
const lastDay = (end: string) => {
  const date = parse(end);
  date.setDate(date.getDate() - 1);
  return date.getDate();
};
const monthName = (isoDate: string) =>
  new Intl.DateTimeFormat(undefined, { month: 'short' }).format(parse(isoDate));
const weekdayName = (isoDate: string) =>
  new Intl.DateTimeFormat(undefined, { weekday: 'short' }).format(parse(isoDate));

@Component({
  selector: 'app-stats',
  imports: [MatButtonModule, MatButtonToggleModule, MatTableModule],
  template: `
    <h1 i18n>Statistics</h1>

    <mat-button-toggle-group
      [value]="period()"
      (change)="pickPeriod($event)"
      aria-label="Period"
      i18n-aria-label
    >
      <mat-button-toggle value="week" i18n>Week</mat-button-toggle>
      <mat-button-toggle value="month" i18n>Month</mat-button-toggle>
      <mat-button-toggle value="year" i18n>Year</mat-button-toggle>
      <mat-button-toggle value="lifetime" i18n>All time</mat-button-toggle>
    </mat-button-toggle-group>

    <div class="range">
      @if (period() !== 'lifetime') {
        <button mat-button type="button" (click)="move(-1)" i18n>Previous</button>
      }
      <strong aria-live="polite">{{ rangeLabel() }}</strong>
      @if (period() !== 'lifetime') {
        <button mat-button type="button" (click)="move(1)" i18n>Next</button>
      }
    </div>

    @if (data(); as s) {
      <dl class="totals" aria-label="Totals" i18n-aria-label>
        <div>
          <dt i18n>Activities</dt>
          <dd>{{ s.activity_count }}</dd>
        </div>
        <div>
          <dt i18n>Distance</dt>
          <dd>{{ distance(s.total_distance) }}</dd>
        </div>
        <div>
          <dt i18n>Time</dt>
          <dd>{{ duration(s.total_duration) }}</dd>
        </div>
        <div>
          <dt i18n>Elevation</dt>
          <dd>{{ elevation(s.total_elevation_gain) }}</dd>
        </div>
        <div>
          <dt i18n>Calories</dt>
          <dd>{{ calories(s.total_calories) }}</dd>
        </div>
      </dl>

      @if (s.activity_count === 0) {
        <p i18n>No activities in this period.</p>
      } @else {
        <table mat-table [dataSource]="s.breakdown" aria-label="Totals per period" i18n-aria-label>
          <ng-container matColumnDef="bucket">
            <th mat-header-cell *matHeaderCellDef>{{ bucketHeader() }}</th>
            <td mat-cell *matCellDef="let b">{{ bucketLabel(b) }}</td>
          </ng-container>
          <ng-container matColumnDef="count">
            <th mat-header-cell *matHeaderCellDef i18n>Activities</th>
            <td mat-cell *matCellDef="let b">{{ b.count }}</td>
          </ng-container>
          <ng-container matColumnDef="distance">
            <th mat-header-cell *matHeaderCellDef i18n>Distance</th>
            <td mat-cell *matCellDef="let b">{{ distance(b.distance) }}</td>
          </ng-container>
          <ng-container matColumnDef="duration">
            <th mat-header-cell *matHeaderCellDef i18n>Time</th>
            <td mat-cell *matCellDef="let b">{{ duration(b.duration) }}</td>
          </ng-container>
          <tr mat-header-row *matHeaderRowDef="bucketColumns"></tr>
          <tr mat-row *matRowDef="let row; columns: bucketColumns"></tr>
        </table>

        <h2 i18n>By activity type</h2>
        <table
          mat-table
          [dataSource]="s.type_breakdown"
          aria-label="Totals per activity type"
          i18n-aria-label
        >
          <ng-container matColumnDef="type">
            <th mat-header-cell *matHeaderCellDef i18n>Type</th>
            <td mat-cell *matCellDef="let t">{{ t.name }}</td>
          </ng-container>
          <ng-container matColumnDef="count">
            <th mat-header-cell *matHeaderCellDef i18n>Activities</th>
            <td mat-cell *matCellDef="let t">{{ t.count }}</td>
          </ng-container>
          <ng-container matColumnDef="distance">
            <th mat-header-cell *matHeaderCellDef i18n>Distance</th>
            <td mat-cell *matCellDef="let t">{{ distance(t.distance) }}</td>
          </ng-container>
          <ng-container matColumnDef="duration">
            <th mat-header-cell *matHeaderCellDef i18n>Time</th>
            <td mat-cell *matCellDef="let t">{{ duration(t.duration) }}</td>
          </ng-container>
          <ng-container matColumnDef="elevation">
            <th mat-header-cell *matHeaderCellDef i18n>Elevation</th>
            <td mat-cell *matCellDef="let t">{{ elevation(t.elevation_gain) }}</td>
          </ng-container>
          <tr mat-header-row *matHeaderRowDef="typeColumns"></tr>
          <tr mat-row *matRowDef="let row; columns: typeColumns"></tr>
        </table>
      }
    } @else if (stats.error()) {
      <p class="error" role="alert" i18n>Could not load your statistics.</p>
    } @else {
      <p i18n>Loading statistics…</p>
    }
  `,
  styles: `
    .range {
      display: flex;
      align-items: center;
      gap: 8px;
      margin: 8px 0 16px;
    }
    .totals {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(140px, 1fr));
      gap: 16px;
      margin: 0 0 24px;
    }
    .totals div {
      padding: 12px 16px;
      border-radius: var(--mat-sys-corner-medium);
      background: var(--mat-sys-surface-container);
    }
    .totals dt {
      font: var(--mat-sys-label-medium);
      color: var(--mat-sys-on-surface-variant);
    }
    .totals dd {
      margin: 4px 0 0;
      font: var(--mat-sys-headline-small);
    }
    table {
      width: 100%;
    }
    h2 {
      margin-top: 24px;
    }
    .error {
      color: var(--mat-sys-error);
    }
  `,
})
export class Stats {
  private readonly auth = inject(AuthService);

  protected readonly bucketColumns = ['bucket', 'count', 'distance', 'duration'];
  protected readonly typeColumns = ['type', 'count', 'distance', 'duration', 'elevation'];

  protected readonly period = signal<Period>('week');
  protected readonly anchor = signal(iso(new Date()));

  protected readonly stats = httpResource<ActivityStats>(() => ({
    url: '/api/v1/activities/stats',
    params: { period: this.period(), date: this.anchor() },
  }));
  protected readonly data = computed(() => this.stats.value());

  private readonly units = computed(() => this.auth.user()?.units ?? 'metric');
  protected readonly distance = (m: number) => formatDistance(m, this.units());
  protected readonly duration = formatDuration;
  protected readonly elevation = (m: number) => formatElevation(m, this.units());
  protected readonly calories = (kcal: number) => `${Math.round(kcal)} kcal`;

  protected readonly bucketHeader = computed(() => {
    switch (this.period()) {
      case 'week':
        return $localize`Day`;
      case 'month':
        return $localize`Week`;
      case 'year':
        return $localize`Month`;
      default:
        return $localize`Year`;
    }
  });

  protected readonly rangeLabel = computed(() => {
    const period = this.period();
    const anchor = parse(this.anchor());
    if (period === 'lifetime') return $localize`All time`;
    if (period === 'year') return String(anchor.getFullYear());
    if (period === 'month')
      return new Intl.DateTimeFormat(undefined, { month: 'long', year: 'numeric' }).format(anchor);

    // Same Monday-based week the API uses.
    const start = new Date(anchor);
    start.setDate(start.getDate() - ((start.getDay() + 6) % 7));
    const end = new Date(start);
    end.setDate(end.getDate() + 6);
    const short = { day: 'numeric', month: 'short' } as const;
    return `${new Intl.DateTimeFormat(undefined, short).format(start)} – ${new Intl.DateTimeFormat(undefined, short).format(end)}`;
  });

  protected pickPeriod(event: MatButtonToggleChange): void {
    this.period.set(event.value as Period);
  }

  protected move(direction: number): void {
    const next = parse(this.anchor());
    switch (this.period()) {
      case 'week':
        next.setDate(next.getDate() + 7 * direction);
        break;
      case 'month':
        next.setDate(1);
        next.setMonth(next.getMonth() + direction);
        break;
      case 'year':
        next.setFullYear(next.getFullYear() + direction);
        break;
    }
    this.anchor.set(iso(next));
  }

  protected bucketLabel(bucket: StatsBucket): string {
    switch (this.period()) {
      case 'week':
        return weekdayName(bucket.start);
      case 'month':
        return `${day(bucket.start)}–${lastDay(bucket.end)}`;
      case 'year':
        return monthName(bucket.start);
      default:
        return bucket.start.slice(0, 4);
    }
  }
}
