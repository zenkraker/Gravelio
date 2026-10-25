import { HttpClient, httpResource } from '@angular/common/http';
import { Component, computed, inject, input } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Activity, ActivityStream, GearPage, StreamType, problemMessage } from '../core/api';
import { ActivityTypes } from '../core/activity-types';
import { AuthService } from '../core/auth';
import {
  PACE_TYPES,
  elevationValue,
  formatDateTime,
  formatDistance,
  formatDuration,
  formatElevation,
  formatPace,
  formatSpeed,
  paceSeconds,
  speedValue,
} from '../core/format';
import { ConfirmDialog } from '../components/confirm-dialog';
import { ChartPoint, StreamChart } from '../components/stream-chart';
import { RouteMap } from '../components/route-map';

@Component({
  selector: 'app-activity-detail',
  imports: [
    RouterLink,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    RouteMap,
    StreamChart,
  ],
  template: `
    <a mat-button routerLink="/activities"
      ><mat-icon aria-hidden="true">arrow_back</mat-icon><span i18n>All activities</span></a
    >
    @if (activity.error()) {
      <p class="error" role="alert" i18n>This activity could not be found.</p>
    }
    @if (activity.value(); as a) {
      <header>
        <div>
          <h1>{{ a.name }}</h1>
          <p>{{ types.name(a.activity_type) }} · {{ when(a.start_time, a.timezone) }}</p>
        </div>
        <button mat-button type="button" (click)="confirmDelete(a)">
          <mat-icon aria-hidden="true">delete</mat-icon><span i18n>Delete</span>
        </button>
      </header>
      @if (a.description) {
        <p>{{ a.description }}</p>
      }
      <mat-form-field class="gear">
        <mat-label i18n>Gear</mat-label>
        <select matNativeControl #gearSelect (change)="setGear(gearSelect.value)">
          <option value="" [selected]="a.gear_id === null" i18n>No gear</option>
          @for (g of gearChoices(a.gear_id); track g.id) {
            <option [value]="g.id" [selected]="g.id === a.gear_id">{{ g.nickname }}</option>
          }
        </select>
      </mat-form-field>
      <dl class="stats">
        <div>
          <dt i18n>Distance</dt>
          <dd>{{ distance(a.distance) }}</dd>
        </div>
        <div>
          <dt i18n>Moving time</dt>
          <dd>{{ duration(a.total_timer_time) }}</dd>
        </div>
        <div>
          <dt i18n>Elapsed time</dt>
          <dd>{{ duration(a.total_elapsed_time) }}</dd>
        </div>
        @if (isPace(a.activity_type)) {
          <div>
            <dt i18n>Pace</dt>
            <dd>{{ pace(a.pace) }}</dd>
          </div>
        } @else {
          <div>
            <dt i18n>Avg speed</dt>
            <dd>{{ speed(a.average_speed) }}</dd>
          </div>
        }
        <div>
          <dt i18n>Max speed</dt>
          <dd>{{ speed(a.max_speed) }}</dd>
        </div>
        <div>
          <dt i18n>Elevation gain</dt>
          <dd>{{ elevation(a.elevation_gain) }}</dd>
        </div>
        <div>
          <dt i18n>Elevation loss</dt>
          <dd>{{ elevation(a.elevation_loss) }}</dd>
        </div>
        @if (a.average_hr) {
          <div>
            <dt i18n>Avg / max HR</dt>
            <dd>{{ a.average_hr }} / {{ a.max_hr }} bpm</dd>
          </div>
        }
        @if (a.average_power) {
          <div>
            <dt i18n>Avg / max power</dt>
            <dd>{{ a.average_power }} / {{ a.max_power }} W</dd>
          </div>
        }
        @if (a.average_cad) {
          <div>
            <dt i18n>Avg / max cadence</dt>
            <dd>{{ a.average_cad }} / {{ a.max_cad }}</dd>
          </div>
        }
        @if (a.calories) {
          <div>
            <dt i18n>Calories</dt>
            <dd>{{ a.calories }} kcal</dd>
          </div>
        }
      </dl>
      @if (route().length > 1) {
        <app-route-map [points]="route()" />
      }
      <section class="charts">
        @if (hr().length) {
          <app-stream-chart i18n-label label="Heart rate (bpm)" [points]="hr()" />
        }
        @if (elevationSeries().length) {
          <app-stream-chart i18n-label label="Elevation" [points]="elevationSeries()" />
        }
        @if (isPace(a.activity_type)) {
          @if (paceSeries().length) {
            <app-stream-chart
              i18n-label
              label="Pace"
              [points]="paceSeries()"
              [format]="formatPaceTick"
            />
          }
        } @else if (speedSeries().length) {
          <app-stream-chart i18n-label label="Speed" [points]="speedSeries()" />
        }
        @if (power().length) {
          <app-stream-chart i18n-label label="Power (W)" [points]="power()" />
        }
        @if (cadence().length) {
          <app-stream-chart i18n-label label="Cadence" [points]="cadence()" />
        }
      </section>
    }
  `,
  styles: `
    header {
      display: flex;
      justify-content: space-between;
      align-items: start;
    }
    .stats {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(160px, 1fr));
      gap: 16px;
      margin: 16px 0;
    }
    dt {
      font: var(--mat-sys-label-medium);
      color: var(--mat-sys-on-surface-variant);
    }
    dd {
      margin: 0;
      font: var(--mat-sys-title-large);
    }
    .charts {
      display: grid;
      gap: 24px;
      margin-top: 24px;
    }
    .error {
      color: var(--mat-sys-error);
    }
    .gear {
      max-width: 320px;
    }
  `,
})
export class ActivityDetail {
  readonly id = input.required<string>(); // route param, via withComponentInputBinding

  private http = inject(HttpClient);
  private router = inject(Router);
  private dialog = inject(MatDialog);
  private snack = inject(MatSnackBar);
  private auth = inject(AuthService);
  protected readonly types = inject(ActivityTypes);

  protected readonly activity = httpResource<Activity>(() => `/api/v1/activities/${this.id()}`);
  private readonly gears = httpResource<GearPage>(() => ({
    url: '/api/v1/gears',
    params: { page_size: 100 },
  }));
  private readonly streams = httpResource<ActivityStream[]>(
    () => `/api/v1/activities/${this.id()}/streams`,
  );

  private readonly units = computed(() => this.auth.user()?.units ?? 'metric');
  protected readonly distance = (m: number) => formatDistance(m, this.units());
  protected readonly duration = formatDuration;
  protected readonly pace = (p: number | null) => formatPace(p, this.units());
  protected readonly speed = (s: number | null) => formatSpeed(s, this.units());
  protected readonly elevation = (m: number | null) => formatElevation(m, this.units());
  protected readonly when = formatDateTime;
  protected readonly isPace = (type: number) => PACE_TYPES.has(type);
  protected readonly formatPaceTick = (seconds: number) => formatDuration(seconds);

  protected readonly route = computed<[number, number][]>(() =>
    this.waypoints(StreamType.Map).map((w) => [Number(w['lat']), Number(w['lon'])]),
  );
  protected readonly hr = computed(() => this.series(StreamType.Hr, 'hr', (v) => v));
  protected readonly power = computed(() => this.series(StreamType.Power, 'power', (v) => v));
  protected readonly cadence = computed(() => this.series(StreamType.Cadence, 'cad', (v) => v));
  protected readonly elevationSeries = computed(() =>
    this.series(StreamType.Elevation, 'ele', (v) => elevationValue(v, this.units())),
  );
  protected readonly speedSeries = computed(() =>
    this.series(StreamType.Speed, 'vel', (v) => speedValue(v, this.units())),
  );
  protected readonly paceSeries = computed(() =>
    this.series(StreamType.Pace, 'pace', (v) => paceSeconds(v, this.units())),
  );

  /** Active gear, plus the gear already on the activity even if it has been retired since. */
  protected gearChoices(current: number | null) {
    return (this.gears.value()?.items ?? []).filter((g) => g.active || g.id === current);
  }

  protected async setGear(value: string): Promise<void> {
    try {
      await firstValueFrom(
        this.http.put(`/api/v1/activities/${this.id()}/gear`, {
          gear_id: value === '' ? null : Number(value),
        }),
      );
      this.activity.reload();
    } catch (err) {
      this.snack.open(problemMessage(err), $localize`Dismiss`, { duration: 8000 });
    }
  }

  private waypoints(type: number) {
    return this.streams.value()?.find((s) => s.stream_type === type)?.waypoints ?? [];
  }

  private series(type: number, key: string, convert: (value: number) => number): ChartPoint[] {
    const start = Date.parse(this.activity.value()?.start_time ?? '');
    return this.waypoints(type).map((w) => ({
      x: (Date.parse(w.time) - start) / 1000,
      y: convert(Number(w[key])),
    }));
  }

  protected confirmDelete(activity: Activity): void {
    this.dialog
      .open(ConfirmDialog, {
        data: {
          title: $localize`Delete activity?`,
          message: $localize`“${activity.name}” and its data will be permanently deleted.`,
          confirm: $localize`Delete`,
        },
      })
      .afterClosed()
      .subscribe(async (confirmed) => {
        if (!confirmed) return;
        try {
          await firstValueFrom(this.http.delete(`/api/v1/activities/${activity.id}`));
          await this.router.navigateByUrl('/activities');
        } catch (err) {
          this.snack.open(problemMessage(err), $localize`Dismiss`, { duration: 8000 });
        }
      });
  }
}
