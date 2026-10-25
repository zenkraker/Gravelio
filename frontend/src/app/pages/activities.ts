import { HttpClient, httpResource } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Activity, Paged, problemMessage } from '../core/api';
import { ActivityTypes } from '../core/activity-types';
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

@Component({
  selector: 'app-activities',
  imports: [
    RouterLink,
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
      @if (scope() === 'mine') {
        <button mat-flat-button type="button" [disabled]="uploading()" (click)="file.click()">
          <mat-icon aria-hidden="true">upload</mat-icon><span i18n>Upload activity</span>
        </button>
      }
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
    .scope {
      margin: 8px 0 16px;
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
  `,
})
export class Activities {
  private http = inject(HttpClient);
  private snack = inject(MatSnackBar);
  private auth = inject(AuthService);
  protected readonly types = inject(ActivityTypes);

  protected readonly pageSize = PAGE_SIZE;
  protected readonly pageIndex = signal(1);
  protected readonly uploading = signal(false);
  protected readonly scope = signal<'mine' | 'following'>('mine');
  protected readonly page = httpResource<Paged<Activity>>(() => ({
    url: this.scope() === 'mine' ? '/api/v1/activities' : '/api/v1/activities/feed',
    params: { page: this.pageIndex(), page_size: PAGE_SIZE },
  }));

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
}
