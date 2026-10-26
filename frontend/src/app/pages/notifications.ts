import { HttpClient, httpResource } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { MatButtonModule } from '@angular/material/button';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { Notification, Paged, problemMessage } from '../core/api';
import { AuthService } from '../core/auth';
import { formatDateTime } from '../core/format';
import { Notifications } from '../core/notifications';

const PAGE_SIZE = 20;

@Component({
  selector: 'app-notifications',
  imports: [RouterLink, MatButtonModule, MatPaginatorModule, MatTableModule],
  template: `
    <div class="header">
      <h1 i18n>Notifications</h1>
      <button mat-button type="button" [disabled]="unreadCount() === 0" (click)="markAll()" i18n>
        Mark all as read
      </button>
    </div>

    @if (page.error()) {
      <p class="error" role="alert" i18n>Could not load your notifications.</p>
    }
    <table
      mat-table
      [dataSource]="page.value()?.items ?? []"
      aria-label="Notifications"
      i18n-aria-label
    >
      <ng-container matColumnDef="message">
        <th mat-header-cell *matHeaderCellDef i18n>Notification</th>
        <td mat-cell *matCellDef="let n">
          <a [routerLink]="['/people', n.actor_id]">{{ message(n) }}</a>
          @if (!n.read) {
            <span class="cdk-visually-hidden" i18n>(unread)</span>
          }
        </td>
      </ng-container>
      <ng-container matColumnDef="when">
        <th mat-header-cell *matHeaderCellDef i18n>When</th>
        <td mat-cell *matCellDef="let n">{{ when(n.created_at) }}</td>
      </ng-container>
      <ng-container matColumnDef="actions">
        <th mat-header-cell *matHeaderCellDef>
          <span class="cdk-visually-hidden" i18n>Actions</span>
        </th>
        <td mat-cell *matCellDef="let n">
          @if (!n.read) {
            <button
              mat-button
              type="button"
              (click)="markRead(n)"
              [attr.aria-label]="readLabel(n)"
              i18n
            >
              Mark as read
            </button>
          }
        </td>
      </ng-container>
      <tr mat-header-row *matHeaderRowDef="columns"></tr>
      <tr mat-row *matRowDef="let row; columns: columns" [class.unread]="!row.read"></tr>
      @if (!page.isLoading() && !(page.value()?.total ?? 0)) {
        <tr class="mat-mdc-row" *matNoDataRow>
          <td class="mat-mdc-cell" [attr.colspan]="columns.length" i18n>No notifications.</td>
        </tr>
      }
    </table>
    <mat-paginator
      [length]="page.value()?.total ?? 0"
      [pageSize]="pageSize"
      [pageIndex]="pageIndex() - 1"
      [hidePageSize]="true"
      (page)="onPage($event)"
    />
  `,
  styles: `
    .header {
      display: flex;
      align-items: center;
      gap: 16px;
    }
    .header h1 {
      flex: 1;
    }
    table {
      width: 100%;
    }
    .unread {
      font-weight: 600;
    }
    .error {
      color: var(--mat-sys-error);
    }
  `,
})
export class NotificationsPage {
  private readonly http = inject(HttpClient);
  private readonly badge = inject(Notifications);
  private readonly auth = inject(AuthService);
  private readonly snack = inject(MatSnackBar);

  protected readonly pageSize = PAGE_SIZE;
  protected readonly pageIndex = signal(1);
  protected readonly columns = ['message', 'when', 'actions'];

  protected readonly page = httpResource<Paged<Notification>>(() => ({
    url: '/api/v1/notifications',
    params: { page: this.pageIndex(), page_size: PAGE_SIZE },
  }));
  protected readonly unreadCount = computed(
    () => this.page.value()?.items.filter((n) => !n.read).length ?? 0,
  );

  protected when(iso: string): string {
    return formatDateTime(iso, this.auth.user()?.timezone ?? null);
  }

  protected message(n: Notification): string {
    switch (n.type) {
      case 11:
        return $localize`${n.actor_name} wants to follow you.`;
      case 12:
        return $localize`${n.actor_name} accepted your follow request.`;
      default:
        return $localize`${n.actor_name} sent you a notification.`;
    }
  }

  protected readLabel(n: Notification): string {
    // The visible text must be part of the accessible name (WCAG 2.5.3), so it comes first.
    return $localize`Mark as read: notification from ${n.actor_name}`;
  }

  protected onPage(event: PageEvent): void {
    this.pageIndex.set(event.pageIndex + 1);
  }

  protected async markRead(n: Notification): Promise<void> {
    try {
      await firstValueFrom(this.http.put(`/api/v1/notifications/${n.id}/read`, null));
      this.reload();
    } catch (err) {
      this.snack.open(problemMessage(err), $localize`Dismiss`, { duration: 8000 });
    }
  }

  protected async markAll(): Promise<void> {
    try {
      await firstValueFrom(this.http.put('/api/v1/notifications/read-all', null));
      this.reload();
    } catch (err) {
      this.snack.open(problemMessage(err), $localize`Dismiss`, { duration: 8000 });
    }
  }

  private reload(): void {
    this.page.reload();
    this.badge.refresh();
  }
}
