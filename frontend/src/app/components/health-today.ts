import { httpResource } from '@angular/common/http';
import { Component, computed } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { DailyStats } from '../core/api';

/** Today's health numbers on the person's own calendar day, each linking to the page where it is logged. */
@Component({
  selector: 'app-health-today',
  imports: [MatCardModule, RouterLink],
  template: `
    @if (items(); as list) {
      <section aria-labelledby="today-title">
        <h2 id="today-title" i18n>Today</h2>
        <ul>
          @for (i of list; track i.link) {
            <li>
              <mat-card appearance="outlined">
                <mat-card-content>
                  <a [routerLink]="i.link">{{ i.label }}</a>
                  <strong>{{ i.value }}</strong>
                </mat-card-content>
              </mat-card>
            </li>
          }
        </ul>
      </section>
    }
  `,
  styles: `
    ul {
      list-style: none;
      padding: 0;
      margin: 0 0 16px;
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(150px, 1fr));
      gap: 12px;
    }
    mat-card-content {
      display: grid;
      gap: 4px;
    }
    strong {
      font: var(--mat-sys-title-large);
    }
  `,
})
export class HealthToday {
  private readonly stats = httpResource<DailyStats>(() => '/api/v1/health/stats/daily');

  /** Only what has data today, so a person who logs no health data sees nothing here. */
  protected readonly items = computed(() => {
    const s = this.stats.value();
    if (!s) return null;
    const list: { link: string; label: string; value: string }[] = [];
    if (s.steps !== null)
      list.push({ link: '/steps', label: $localize`Steps`, value: s.steps.toLocaleString() });
    if (s.water_ml !== null)
      list.push({ link: '/water', label: $localize`Water`, value: `${s.water_ml} ml` });
    if (s.weight)
      list.push({ link: '/weight', label: $localize`Weight`, value: `${s.weight.weight} kg` });
    if (s.sleep?.total_sleep_seconds) {
      const minutes = Math.round(s.sleep.total_sleep_seconds / 60);
      list.push({
        link: '/sleep',
        label: $localize`Sleep`,
        value: `${Math.floor(minutes / 60)} h ${minutes % 60} min`,
      });
    }
    if (s.fasting?.status === 'in_progress')
      list.push({ link: '/fasting', label: $localize`Fasting`, value: $localize`In progress` });
    if (s.poop_count > 0)
      list.push({ link: '/poop', label: $localize`Bowel movements`, value: String(s.poop_count) });
    return list.length ? list : null;
  });
}
