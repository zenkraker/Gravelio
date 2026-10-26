import { httpResource } from '@angular/common/http';
import { Service, inject } from '@angular/core';
import { UnreadCount } from './api';
import { AuthService } from './auth';

/** The unread badge shared by the toolbar and the notifications page. */
@Service()
export class Notifications {
  private readonly auth = inject(AuthService);

  private readonly unread = httpResource<UnreadCount>(() =>
    this.auth.user() ? '/api/v1/notifications/unread-count' : undefined,
  );

  count(): number {
    return this.unread.value()?.count ?? 0;
  }

  refresh(): void {
    this.unread.reload();
  }
}
