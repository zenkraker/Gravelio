import { DOCUMENT } from '@angular/common';
import { HttpClient, httpResource } from '@angular/common/http';
import { Component, computed, inject, input } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { OUTLINE_FIELDS } from '../core/outline-fields';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import {
  Activity,
  ActivityLap,
  ActivityStream,
  Gear,
  HrZone,
  Paged,
  PublicSettings,
  problemMessage,
} from '../core/api';
import { ActivityTypes } from '../core/activity-types';
import { AuthService } from '../core/auth';
import { formatDateTime } from '../core/format';
import { ConfirmDialog } from '../components/confirm-dialog';
import { ActivityEdit } from '../components/activity-edit';
import { ActivityView } from '../components/activity-view';

@Component({
  selector: 'app-activity-detail',
  providers: [OUTLINE_FIELDS],
  imports: [
    RouterLink,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatSelectModule,
    ActivityEdit,
    ActivityView,
  ],
  template: `
    <a mat-button [routerLink]="backLink()"
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
          @if (!isOwner()) {
            <p>
              <span i18n>Shared by</span>
              <a [routerLink]="['/people', a.owner.id]">{{ a.owner.name }}</a>
            </p>
          }
        </div>
        @if (isOwner()) {
          @if (canShare()) {
            <button mat-button type="button" (click)="copyLink(a)">
              <mat-icon aria-hidden="true">link</mat-icon><span i18n>Copy public link</span>
            </button>
          }
          <button mat-button type="button" (click)="confirmDelete(a)">
            <mat-icon aria-hidden="true">delete</mat-icon><span i18n>Delete</span>
          </button>
        }
      </header>
      @if (a.is_hidden) {
        <p class="hidden-note" i18n>
          This activity is hidden. Only you can see it, and it is left out of your statistics, goals
          and gear totals. You can unhide it under Edit details.
        </p>
      }
      @if (a.description) {
        <p>{{ a.description }}</p>
      }
      @if (isOwner()) {
        <div class="owner-controls">
          <mat-form-field class="gear">
            <mat-label i18n>Gear</mat-label>
            <mat-select [value]="'' + (a.gear_id ?? '')" (selectionChange)="setGear($event.value)">
              <mat-option value="" i18n>No gear</mat-option>
              @for (g of gearChoices(a.gear_id); track g.id) {
                <mat-option [value]="'' + g.id">{{ g.nickname }}</mat-option>
              }
            </mat-select>
          </mat-form-field>
          <mat-form-field class="visibility">
            <mat-label i18n>Visibility</mat-label>
            <mat-select [value]="'' + a.visibility" (selectionChange)="setVisibility($event.value)">
              <mat-option value="0" i18n>Public</mat-option>
              <mat-option value="1" i18n>Followers</mat-option>
              <mat-option value="2" i18n>Private</mat-option>
            </mat-select>
          </mat-form-field>
        </div>
      }
      @if (isOwner()) {
        <app-activity-edit
          [activity]="a"
          [etag]="activity.headers()?.get('ETag') ?? null"
          (saved)="activity.reload()"
        />
      }
      <app-activity-view
        [activity]="a"
        [streams]="streams.value() ?? []"
        [laps]="laps.value() ?? []"
        [hrZones]="hrZones.value() ?? []"
        [units]="units()"
      />
    }
  `,
  styles: `
    .hidden-note {
      color: var(--mat-sys-on-surface-variant);
    }
    header {
      display: flex;
      justify-content: space-between;
      align-items: start;
    }
    .error {
      color: var(--mat-sys-error);
    }
    .gear {
      max-width: 320px;
    }
    .owner-controls {
      display: flex;
      flex-wrap: wrap;
      gap: 16px;
    }
    .visibility {
      max-width: 200px;
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
  private readonly gears = httpResource<Paged<Gear>>(() =>
    this.isOwner() ? { url: '/api/v1/gears', params: { page_size: 100 } } : undefined,
  );
  private readonly hrZones = httpResource<HrZone[]>(
    () => `/api/v1/activities/${this.id()}/hr-zones`,
  );
  private readonly laps = httpResource<ActivityLap[]>(() => `/api/v1/activities/${this.id()}/laps`);
  private readonly streams = httpResource<ActivityStream[]>(
    () => `/api/v1/activities/${this.id()}/streams`,
  );

  protected readonly units = computed(() => this.auth.user()?.units ?? 'metric');
  protected readonly when = formatDateTime;
  private readonly document = inject(DOCUMENT);
  private readonly publicSettings = httpResource<PublicSettings>(
    () => '/api/v1/server-settings/public',
  );
  protected readonly isOwner = computed(() => {
    const a = this.activity.value();
    return !!a && a.owner.id === this.auth.user()?.id;
  });
  protected readonly backLink = computed(() => {
    const a = this.activity.value();
    return a && a.owner.id !== this.auth.user()?.id ? ['/people', a.owner.id] : '/activities';
  });
  /** A public link only works when the admin allows links and the activity itself is public. */
  protected readonly canShare = computed(
    () =>
      !!this.publicSettings.value()?.public_shareable_links &&
      this.activity.value()?.visibility === 0,
  );

  protected async copyLink(activity: Activity): Promise<void> {
    const link = `${this.document.location.origin}/share/${activity.id}`;
    try {
      await this.document.defaultView?.navigator.clipboard.writeText(link);
      this.snack.open($localize`Link copied`, undefined, { duration: 4000 });
    } catch {
      // The clipboard can be blocked; show the link so it can still be copied by hand.
      this.snack.open(link, $localize`Dismiss`, { duration: 15000 });
    }
  }

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

  protected async setVisibility(value: string): Promise<void> {
    const a = this.activity.value();
    if (!a) return;
    try {
      await firstValueFrom(
        this.http.put(`/api/v1/activities/${a.id}`, {
          name: a.name,
          description: a.description,
          activity_type: a.activity_type,
          visibility: Number(value),
        }),
      );
      this.activity.reload();
    } catch (err) {
      this.snack.open(problemMessage(err), $localize`Dismiss`, { duration: 8000 });
    }
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
