import { httpResource } from '@angular/common/http';
import { Component, inject, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { ActivityStream, PublicActivity } from '../core/api';
import { ActivityTypes } from '../core/activity-types';
import { formatDateTime } from '../core/format';
import { ActivityView } from '../components/activity-view';

/** An activity opened from a share link, without signing in. The API decides what it may show; this page just renders it. */
@Component({
  selector: 'app-public-activity',
  imports: [RouterLink, MatButtonModule, ActivityView],
  template: `
    <header class="bar">
      <a class="brand" routerLink="/" i18n>Gravelio</a>
      <a mat-button routerLink="/login" i18n>Sign in</a>
    </header>
    <main>
      @if (activity.error()) {
        <h1 i18n>This activity is not shared</h1>
        <p i18n>The link may be wrong, or the owner may have stopped sharing it.</p>
      }
      @if (activity.value(); as a) {
        <h1>{{ a.name }}</h1>
        <p>
          {{ types.name(a.activity_type) }}
          @if (a.start_time) {
            · {{ when(a.start_time, a.timezone) }}
          }
        </p>
        @if (a.owner_name) {
          <p><span i18n>Shared by</span> {{ a.owner_name }}</p>
        }
        @if (a.description) {
          <p>{{ a.description }}</p>
        }
        <app-activity-view [activity]="a" [streams]="streams.value() ?? []" />
      }
    </main>
  `,
  styles: `
    .bar {
      display: flex;
      align-items: center;
      justify-content: space-between;
      padding: 8px 16px;
      background: var(--mat-sys-surface-container);
    }
    .brand {
      font: var(--mat-sys-title-large);
      color: var(--mat-sys-primary);
      text-decoration: none;
    }
    main {
      max-width: 1100px;
      margin: 0 auto;
      padding: 16px;
    }
  `,
})
export class PublicActivityPage {
  readonly id = input.required<string>(); // route param, via withComponentInputBinding
  protected readonly types = inject(ActivityTypes);
  protected readonly when = formatDateTime;

  protected readonly activity = httpResource<PublicActivity>(
    () => `/api/v1/public/activities/${this.id()}`,
  );
  protected readonly streams = httpResource<ActivityStream[]>(() =>
    this.activity.hasValue() ? `/api/v1/public/activities/${this.id()}/streams` : undefined,
  );
}
