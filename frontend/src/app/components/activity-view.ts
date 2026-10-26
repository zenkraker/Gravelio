import { Component, computed, input } from '@angular/core';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTableModule } from '@angular/material/table';
import { ActivityLap, ActivityStream, HrZone, StreamType } from '../core/api';
import {
  PACE_TYPES,
  elevationValue,
  formatDistance,
  formatDuration,
  formatElevation,
  formatPace,
  formatSpeed,
  paceSeconds,
  speedValue,
} from '../core/format';
import { RouteMap } from './route-map';
import { ChartPoint, StreamChart } from './stream-chart';

/** What the stats grid, map and charts need; the signed-in and the public activity both have it. */
export interface ActivityFacts {
  activity_type: number;
  distance: number;
  start_time: string | null;
  total_timer_time: number;
  total_elapsed_time: number;
  pace: number | null;
  average_speed: number | null;
  max_speed: number | null;
  elevation_gain: number | null;
  elevation_loss: number | null;
  average_hr: number | null;
  max_hr: number | null;
  average_power: number | null;
  max_power: number | null;
  average_cad: number | null;
  max_cad: number | null;
  calories?: number | null;
}

/** The numbers, route map and charts of one activity. Fields the viewer may not see arrive as null and simply don't render. */
@Component({
  selector: 'app-activity-view',
  imports: [MatProgressBarModule, MatTableModule, RouteMap, StreamChart],
  template: `
    @let a = activity();
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
      @if (isPace()) {
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
      @if (isPace()) {
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
    @if (hrZones().length) {
      <h2 i18n>Heart rate zones</h2>
      <ul class="zones">
        @for (z of hrZones(); track z.zone) {
          <li>
            <span class="zone-name">{{ zoneLabel(z) }}</span>
            <mat-progress-bar
              mode="determinate"
              [value]="z.percent"
              [attr.aria-label]="zoneLabel(z)"
            />
            <span class="zone-value">{{ z.percent }}% · {{ duration(z.seconds) }}</span>
          </li>
        }
      </ul>
    }
    @if (laps().length > 1) {
      <h2 i18n>Laps</h2>
      <table mat-table [dataSource]="laps()" aria-label="Laps" i18n-aria-label>
        <ng-container matColumnDef="index">
          <th mat-header-cell *matHeaderCellDef i18n>Lap</th>
          <td mat-cell *matCellDef="let l">{{ l.index }}</td>
        </ng-container>
        <ng-container matColumnDef="distance">
          <th mat-header-cell *matHeaderCellDef i18n>Distance</th>
          <td mat-cell *matCellDef="let l">
            {{ l.distance === null ? '—' : distance(l.distance) }}
          </td>
        </ng-container>
        <ng-container matColumnDef="time">
          <th mat-header-cell *matHeaderCellDef i18n>Time</th>
          <td mat-cell *matCellDef="let l">
            {{ duration(l.total_timer_time ?? l.total_elapsed_time) }}
          </td>
        </ng-container>
        <ng-container matColumnDef="speed">
          <th mat-header-cell *matHeaderCellDef>{{ isPace() ? paceLabel : speedLabel }}</th>
          <td mat-cell *matCellDef="let l">{{ lapSpeed(l) }}</td>
        </ng-container>
        <ng-container matColumnDef="hr">
          <th mat-header-cell *matHeaderCellDef i18n>Avg HR</th>
          <td mat-cell *matCellDef="let l">{{ l.average_hr ?? '—' }}</td>
        </ng-container>
        <ng-container matColumnDef="elevation">
          <th mat-header-cell *matHeaderCellDef i18n>Gain</th>
          <td mat-cell *matCellDef="let l">
            {{ l.elevation_gain === null ? '—' : elevation(l.elevation_gain) }}
          </td>
        </ng-container>
        <tr mat-header-row *matHeaderRowDef="lapColumns"></tr>
        <tr mat-row *matRowDef="let row; columns: lapColumns"></tr>
      </table>
    }
  `,
  styles: `
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
    .zones {
      list-style: none;
      padding: 0;
      display: grid;
      gap: 8px;
    }
    .zones li {
      display: grid;
      grid-template-columns: minmax(120px, 200px) 1fr minmax(110px, auto);
      align-items: center;
      gap: 12px;
    }
    .charts {
      display: grid;
      gap: 24px;
      margin-top: 24px;
    }
  `,
})
export class ActivityView {
  readonly activity = input.required<ActivityFacts>();
  readonly streams = input.required<ActivityStream[]>();
  readonly laps = input<ActivityLap[]>([]);
  readonly hrZones = input<HrZone[]>([]);
  readonly units = input<'metric' | 'imperial'>('metric');

  protected readonly lapColumns = ['index', 'distance', 'time', 'speed', 'hr', 'elevation'];
  protected readonly paceLabel = $localize`Pace`;
  protected readonly speedLabel = $localize`Speed`;

  protected readonly distance = (m: number) => formatDistance(m, this.units());
  protected readonly duration = formatDuration;
  protected readonly pace = (p: number | null) => formatPace(p, this.units());
  protected readonly speed = (s: number | null) => formatSpeed(s, this.units());
  protected readonly elevation = (m: number | null) => formatElevation(m, this.units());
  protected readonly formatPaceTick = (seconds: number) => formatDuration(seconds);
  protected readonly isPace = computed(() => PACE_TYPES.has(this.activity().activity_type));

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

  protected zoneLabel(z: HrZone): string {
    return z.range ? $localize`Zone ${z.zone} (${z.range} bpm)` : $localize`Zone ${z.zone}`;
  }

  /** A lap's speed or pace; a speed the owner hides stays a dash, it is never worked out from distance and time. */
  protected lapSpeed(lap: ActivityLap): string {
    const v = lap.average_speed;
    return this.isPace() ? this.pace(v ? 1 / v : null) : this.speed(v);
  }

  private waypoints(type: number) {
    return this.streams().find((s) => s.stream_type === type)?.waypoints ?? [];
  }

  // A hidden start time turns the timestamps into offsets from a fixed epoch, so a missing start is origin 0.
  private series(type: number, key: string, convert: (value: number) => number): ChartPoint[] {
    const startTime = this.activity().start_time;
    const origin = startTime ? Date.parse(startTime) : 0;
    return this.waypoints(type).map((w) => ({
      x: (Date.parse(w.time) - origin) / 1000,
      y: convert(Number(w[key])),
    }));
  }
}
