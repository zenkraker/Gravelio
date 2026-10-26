import { Avatar } from '../components/avatar';
import { HttpClient, httpResource } from '@angular/common/http';
import { Component, computed, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTabsModule } from '@angular/material/tabs';
import { Activity, Follow, Paged, PersonProfile, problemMessage } from '../core/api';
import { AuthService } from '../core/auth';
import { formatDateTime, formatDistance } from '../core/format';

const PAGE_SIZE = 20;

@Component({
  selector: 'app-person',
  imports: [
    Avatar,
    RouterLink,
    MatButtonModule,
    MatCardModule,
    MatIconModule,
    MatPaginatorModule,
    MatTabsModule,
  ],
  template: `
    <a mat-button routerLink="/people"
      ><mat-icon aria-hidden="true">arrow_back</mat-icon><span i18n>All people</span></a
    >
    @if (profile.error()) {
      <p class="error" role="alert" i18n>This profile could not be found.</p>
    }
    @if (profile.value(); as p) {
      <header>
        <app-avatar
          [userId]="p.person.id"
          [name]="p.person.name"
          [hasPhoto]="p.person.has_photo"
          [size]="64"
        />
        <div>
          <h1>{{ p.person.name }}</h1>
          <p>
            {{ '@' + p.person.username }}
            @if (p.person.city) {
              · {{ p.person.city }}
            }
          </p>
        </div>
        @if (isSelf()) {
          <a mat-button routerLink="/profile" i18n>Edit your profile</a>
        } @else {
          @if (p.outgoing?.status === 'accepted') {
            <button mat-button type="button" (click)="unfollow()" i18n>Unfollow</button>
          } @else {
            <button
              mat-flat-button
              type="button"
              [disabled]="p.outgoing?.status === 'pending'"
              (click)="follow()"
            >
              {{ p.outgoing?.status === 'pending' ? requestedLabel : followLabel }}
            </button>
          }
          @if (p.incoming?.status === 'accepted') {
            <span class="note" i18n>Follows you</span>
          }
        }
      </header>

      @if (p.incoming?.status === 'pending') {
        <mat-card appearance="outlined" class="request">
          <mat-card-content>
            <p>{{ incomingLabel(p.incoming.follower.name) }}</p>
            <button mat-flat-button type="button" (click)="acceptIncoming()" i18n>Accept</button>
            <button mat-button type="button" (click)="declineIncoming()" i18n>Decline</button>
          </mat-card-content>
        </mat-card>
      }

      <dl class="counts">
        <div>
          <dt i18n>Followers</dt>
          <dd>{{ p.followers }}</dd>
        </div>
        <div>
          <dt i18n>Following</dt>
          <dd>{{ p.following }}</dd>
        </div>
      </dl>

      <mat-tab-group
        [selectedIndex]="tabIndex()"
        (selectedIndexChange)="setTab($event)"
        animationDuration="0ms"
      >
        <mat-tab label="Activities" i18n-label>
          @if (activities.error()) {
            <p class="error" role="alert" i18n>Could not load these activities.</p>
          }
          <ul class="entries">
            @for (a of activities.value()?.items; track a.id) {
              <li>
                <a [routerLink]="['/activities', a.id]">{{ a.name }}</a>
                <span>{{ when(a.start_time, a.timezone) }} · {{ distance(a.distance) }}</span>
              </li>
            } @empty {
              @if (!activities.isLoading()) {
                <li><p i18n>No activities to show.</p></li>
              }
            }
          </ul>
          <mat-paginator
            [length]="activities.value()?.total ?? 0"
            [pageSize]="pageSize"
            [pageIndex]="pageIndex() - 1"
            [hidePageSize]="true"
            (page)="onPage($event)"
          />
        </mat-tab>

        @if (canViewNetwork()) {
          <mat-tab label="Followers" i18n-label>
            <ul class="entries">
              @for (f of followers.value()?.items; track f.follower_id) {
                <li>
                  <a [routerLink]="['/people', f.follower.id]">{{ f.follower.name }}</a>
                  <span>{{ '@' + f.follower.username }}</span>
                </li>
              } @empty {
                @if (!followers.isLoading()) {
                  <li><p i18n>No followers yet.</p></li>
                }
              }
            </ul>
          </mat-tab>
          <mat-tab label="Following" i18n-label>
            <ul class="entries">
              @for (f of following.value()?.items; track f.followee_id) {
                <li>
                  <a [routerLink]="['/people', f.followee.id]">{{ f.followee.name }}</a>
                  <span>{{ '@' + f.followee.username }}</span>
                </li>
              } @empty {
                @if (!following.isLoading()) {
                  <li><p i18n>Not following anyone yet.</p></li>
                }
              }
            </ul>
          </mat-tab>
        }
      </mat-tab-group>
    }
  `,
  styles: `
    header {
      display: flex;
      align-items: start;
      justify-content: space-between;
      gap: 16px;
      margin-top: 8px;
    }
    header p {
      color: var(--mat-sys-on-surface-variant);
      margin: 4px 0 0;
    }
    .counts {
      display: flex;
      gap: 32px;
      margin: 24px 0;
    }
    dt {
      font: var(--mat-sys-label-medium);
      color: var(--mat-sys-on-surface-variant);
    }
    dd {
      margin: 0;
      font: var(--mat-sys-title-large);
    }
    .entries {
      list-style: none;
      padding: 0;
      display: grid;
      gap: 8px;
    }
    .entries li {
      display: flex;
      justify-content: space-between;
      gap: 16px;
      border-bottom: 1px solid var(--mat-sys-outline-variant);
      padding: 8px 0;
    }
    .entries span {
      color: var(--mat-sys-on-surface-variant);
    }
    .request,
    .note {
      margin-top: 16px;
    }
    .error {
      color: var(--mat-sys-error);
    }
  `,
})
export class PersonPage {
  readonly id = input.required<string>(); // route param, via withComponentInputBinding

  private http = inject(HttpClient);
  private snack = inject(MatSnackBar);
  private auth = inject(AuthService);

  protected readonly pageSize = PAGE_SIZE;
  protected readonly pageIndex = signal(1);
  protected readonly tab = signal<'activities' | 'followers' | 'following'>('activities');
  protected readonly followLabel = $localize`Follow`;
  protected readonly requestedLabel = $localize`Requested`;

  protected readonly profile = httpResource<PersonProfile>(() => `/api/v1/people/${this.id()}`);
  protected readonly activities = httpResource<Paged<Activity>>(() => ({
    url: `/api/v1/users/${this.id()}/activities`,
    params: { page: this.pageIndex(), page_size: PAGE_SIZE },
  }));

  protected readonly isSelf = computed(
    () => this.profile.value()?.person.id === this.auth.user()?.id,
  );
  protected readonly canViewNetwork = computed(() => {
    const p = this.profile.value();
    return !!p && (p.person.id === this.auth.user()?.id || p.outgoing?.status === 'accepted');
  });
  protected readonly tabIndex = computed(() =>
    this.tab() === 'followers' ? 1 : this.tab() === 'following' ? 2 : 0,
  );
  protected readonly followers = httpResource<Paged<Follow>>(() =>
    this.canViewNetwork() && this.tab() === 'followers'
      ? { url: `/api/v1/users/${this.id()}/followers`, params: { page_size: 100 } }
      : undefined,
  );
  protected readonly following = httpResource<Paged<Follow>>(() =>
    this.canViewNetwork() && this.tab() === 'following'
      ? { url: `/api/v1/users/${this.id()}/following`, params: { page_size: 100 } }
      : undefined,
  );

  private readonly units = computed(() => this.auth.user()?.units ?? 'metric');
  protected readonly when = formatDateTime;
  protected readonly distance = (m: number) => formatDistance(m, this.units());

  protected incomingLabel(name: string): string {
    return $localize`${name} wants to follow you.`;
  }

  protected setTab(index: number): void {
    this.tab.set(index === 1 ? 'followers' : index === 2 ? 'following' : 'activities');
  }

  protected onPage(event: PageEvent): void {
    this.pageIndex.set(event.pageIndex + 1);
  }

  protected async follow(): Promise<void> {
    try {
      await firstValueFrom(this.http.post(`/api/v1/users/${this.id()}/followers`, null));
      this.profile.reload();
      this.snack.open($localize`Follow request sent`, undefined, { duration: 4000 });
    } catch (err) {
      this.snack.open(problemMessage(err), $localize`Dismiss`, { duration: 8000 });
    }
  }

  protected async unfollow(): Promise<void> {
    const me = this.auth.user();
    if (!me) return;
    try {
      await firstValueFrom(this.http.delete(`/api/v1/users/${this.id()}/followers/${me.id}`));
      this.profile.reload();
      this.activities.reload();
      this.followers.reload();
      this.following.reload();
      this.snack.open($localize`Unfollowed`, undefined, { duration: 4000 });
    } catch (err) {
      this.snack.open(problemMessage(err), $localize`Dismiss`, { duration: 8000 });
    }
  }

  protected async acceptIncoming(): Promise<void> {
    const incoming = this.profile.value()?.incoming;
    if (!incoming) return;
    try {
      await firstValueFrom(
        this.http.post(`/api/v1/follow-requests/${incoming.follower_id}/accept`, null),
      );
      this.profile.reload();
      this.activities.reload();
      this.followers.reload();
      this.following.reload();
      this.snack.open($localize`Request accepted`, undefined, { duration: 4000 });
    } catch (err) {
      this.snack.open(problemMessage(err), $localize`Dismiss`, { duration: 8000 });
    }
  }

  protected async declineIncoming(): Promise<void> {
    const incoming = this.profile.value()?.incoming;
    if (!incoming) return;
    try {
      await firstValueFrom(this.http.delete(`/api/v1/follow-requests/${incoming.follower_id}`));
      this.profile.reload();
      this.snack.open($localize`Request declined`, undefined, { duration: 4000 });
    } catch (err) {
      this.snack.open(problemMessage(err), $localize`Dismiss`, { duration: 8000 });
    }
  }
}
