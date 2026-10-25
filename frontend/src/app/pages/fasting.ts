import { HttpClient, httpResource } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { FormField, form, required, submit } from '@angular/forms/signals';
import { firstValueFrom } from 'rxjs';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { FastingEntry, FastingStats, Paged, problemMessage } from '../core/api';
import { AuthService } from '../core/auth';
import { formatDateTime, formatDuration } from '../core/format';
import { OUTLINE_FIELDS } from '../core/outline-fields';
import { ConfirmDialog } from '../components/confirm-dialog';

const PAGE_SIZE = 20;
const TYPES = ['16:8', '18:6', '20:4', 'OMAD', '24h', '36h', '48h', '72h', 'custom'];
const empty = { start: '', type: '16:8', targetHours: '16', notes: '' };

@Component({
  selector: 'app-fasting',
  providers: [OUTLINE_FIELDS],
  imports: [
    FormField,
    MatButtonModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatPaginatorModule,
    MatSelectModule,
    MatTableModule,
  ],
  template: `
    <h1 i18n>Fasting</h1>

    @if (active(); as a) {
      <mat-card appearance="outlined" class="active">
        <mat-card-content>
          <h2 i18n>Fasting in progress</h2>
          <p>
            <span i18n>Started</span> {{ when(a.fast_start_time) }} · <span i18n>Target</span>
            {{ duration(a.target_duration_seconds) }}
          </p>
          <button mat-flat-button type="button" (click)="end(a, 'completed')" i18n>End fast</button>
          <button mat-button type="button" (click)="end(a, 'cancelled')" i18n>Cancel fast</button>
        </mat-card-content>
      </mat-card>
    }

    @if (stats.value(); as s) {
      <dl class="stats">
        <div>
          <dt i18n>Completed fasts</dt>
          <dd>{{ s.total_fasts }}</dd>
        </div>
        <div>
          <dt i18n>Current streak</dt>
          <dd>{{ s.current_streak }}</dd>
        </div>
        <div>
          <dt i18n>Longest streak</dt>
          <dd>{{ s.longest_streak }}</dd>
        </div>
        <div>
          <dt i18n>Average fast</dt>
          <dd>{{ duration(s.avg_duration_seconds) }}</dd>
        </div>
        <div>
          <dt i18n>Completion rate</dt>
          <dd>{{ s.completion_rate }} %</dd>
        </div>
      </dl>
    }

    <mat-card appearance="outlined">
      <mat-card-content>
        <h2 i18n>Start a fast</h2>
        <p i18n>Only one fast can be in progress at a time.</p>
        <form novalidate (submit)="save($event)">
          <mat-form-field>
            <mat-label i18n>Start time</mat-label>
            <input matInput type="datetime-local" [formField]="f.start" />
          </mat-form-field>
          <mat-form-field>
            <mat-label i18n>Type</mat-label>
            <mat-select [formField]="f.type">
              @for (type of types; track type) {
                <mat-option [value]="type">{{ type }}</mat-option>
              }
            </mat-select>
          </mat-form-field>
          <mat-form-field>
            <mat-label i18n>Target (hours)</mat-label>
            <input matInput type="number" [formField]="f.targetHours" />
          </mat-form-field>
          <mat-form-field class="notes">
            <mat-label i18n>Notes</mat-label>
            <textarea matInput rows="2" [formField]="f.notes"></textarea>
          </mat-form-field>
          @if (error()) {
            <p class="error" role="alert">{{ error() }}</p>
          }
          <button mat-flat-button type="submit" [disabled]="f().submitting()" i18n>
            Start fast
          </button>
        </form>
      </mat-card-content>
    </mat-card>

    @if (history.error()) {
      <p class="error" role="alert" i18n>Could not load your fasts.</p>
    }
    <table
      mat-table
      [dataSource]="history.value()?.items ?? []"
      aria-label="Fasting sessions, newest first"
      i18n-aria-label
    >
      <ng-container matColumnDef="start">
        <th mat-header-cell *matHeaderCellDef i18n>Start</th>
        <td mat-cell *matCellDef="let f">{{ when(f.fast_start_time) }}</td>
      </ng-container>
      <ng-container matColumnDef="end">
        <th mat-header-cell *matHeaderCellDef i18n>End</th>
        <td mat-cell *matCellDef="let f">{{ when(f.fast_end_time) }}</td>
      </ng-container>
      <ng-container matColumnDef="duration">
        <th mat-header-cell *matHeaderCellDef i18n>Duration</th>
        <td mat-cell *matCellDef="let f">
          {{ duration(f.actual_duration_seconds ?? f.target_duration_seconds) }}
        </td>
      </ng-container>
      <ng-container matColumnDef="type">
        <th mat-header-cell *matHeaderCellDef i18n>Type</th>
        <td mat-cell *matCellDef="let f">{{ f.fasting_type ?? '—' }}</td>
      </ng-container>
      <ng-container matColumnDef="status">
        <th mat-header-cell *matHeaderCellDef i18n>Status</th>
        <td mat-cell *matCellDef="let f">{{ statusLabel(f.status) }}</td>
      </ng-container>
      <ng-container matColumnDef="actions">
        <th mat-header-cell *matHeaderCellDef>
          <span class="cdk-visually-hidden" i18n>Actions</span>
        </th>
        <td mat-cell *matCellDef="let f">
          <button
            mat-button
            type="button"
            (click)="confirmDelete(f)"
            [attr.aria-label]="deleteLabel(f)"
            i18n
          >
            Delete
          </button>
        </td>
      </ng-container>
      <tr mat-header-row *matHeaderRowDef="columns"></tr>
      <tr mat-row *matRowDef="let row; columns: columns"></tr>
      @if (!history.isLoading()) {
        <tr class="mat-mdc-row" *matNoDataRow>
          <td class="mat-mdc-cell" [attr.colspan]="columns.length" i18n>No fasts yet.</td>
        </tr>
      }
    </table>
    <mat-paginator
      [length]="history.value()?.total ?? 0"
      [pageSize]="pageSize"
      [pageIndex]="pageIndex() - 1"
      [hidePageSize]="true"
      (page)="onPage($event)"
    />
  `,
  styles: `
    .active {
      margin-bottom: 16px;
    }
    .stats {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(150px, 1fr));
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
    .stats + mat-card {
      margin-bottom: 16px;
    }
    form {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(200px, 1fr));
      gap: 8px 16px;
      align-items: start;
      padding-top: 8px;
    }
    .notes,
    button[type='submit'],
    .error {
      grid-column: 1 / -1;
      justify-self: start;
    }
    table {
      width: 100%;
      margin-top: 16px;
    }
    .error {
      color: var(--mat-sys-error);
      margin: 0;
    }
  `,
})
export class Fasting {
  private http = inject(HttpClient);
  private snack = inject(MatSnackBar);
  private dialog = inject(MatDialog);
  private auth = inject(AuthService);

  protected readonly types = TYPES;
  protected readonly columns = ['start', 'end', 'duration', 'type', 'status', 'actions'];
  protected readonly pageSize = PAGE_SIZE;
  protected readonly pageIndex = signal(1);
  protected readonly error = signal('');

  protected readonly history = httpResource<Paged<FastingEntry>>(() => ({
    url: '/api/v1/health/fasting',
    params: { page: this.pageIndex(), page_size: PAGE_SIZE },
  }));
  protected readonly active = computed(
    () => this.history.value()?.items.find((f) => f.status === 'in_progress') ?? null,
  );
  protected readonly stats = httpResource<FastingStats>(() => '/api/v1/health/fasting/stats');

  private readonly model = signal({ ...empty });
  protected readonly f = form(this.model, (p) => {
    required(p.start, { message: $localize`Enter the start time` });
  });

  protected when(iso: string | null): string {
    return formatDateTime(iso, this.auth.user()?.timezone ?? null);
  }

  protected duration(seconds: number | null): string {
    return seconds == null ? '—' : formatDuration(seconds);
  }

  protected statusLabel(status: string): string {
    switch (status) {
      case 'in_progress':
        return $localize`In progress`;
      case 'completed':
        return $localize`Completed`;
      case 'broken':
        return $localize`Broken`;
      case 'cancelled':
        return $localize`Cancelled`;
      default:
        return status;
    }
  }

  protected deleteLabel(f: FastingEntry): string {
    return $localize`Delete the fast from ${f.fast_start_time}`;
  }

  protected onPage(event: PageEvent): void {
    this.pageIndex.set(event.pageIndex + 1);
  }

  private reload(): void {
    this.history.reload();
    this.stats.reload();
  }

  protected async save(event: Event): Promise<void> {
    event.preventDefault();
    this.error.set('');
    await submit(this.f, async () => {
      try {
        const v = this.model();
        await firstValueFrom(
          this.http.post('/api/v1/health/fasting', {
            fast_start_time: v.start,
            fasting_type: v.type,
            target_duration_seconds:
              v.targetHours === '' ? null : Math.round(Number(v.targetHours) * 3600),
            notes: v.notes || null,
          }),
        );
        this.model.update((m) => ({ ...m, start: '', notes: '' }));
        this.reload();
        this.snack.open($localize`Fast started`, undefined, { duration: 4000 });
      } catch (err) {
        this.error.set(problemMessage(err));
      }
    });
  }

  protected async end(fast: FastingEntry, status: 'completed' | 'cancelled'): Promise<void> {
    try {
      await firstValueFrom(
        this.http.post(`/api/v1/health/fasting/${fast.id}/complete`, {
          fast_end_time: new Date().toISOString(),
          status,
        }),
      );
      this.reload();
      this.snack.open(
        status === 'completed' ? $localize`Fast ended` : $localize`Fast cancelled`,
        undefined,
        { duration: 4000 },
      );
    } catch (err) {
      this.snack.open(problemMessage(err), $localize`Dismiss`, { duration: 8000 });
    }
  }

  protected confirmDelete(f: FastingEntry): void {
    this.dialog
      .open(ConfirmDialog, {
        data: {
          title: $localize`Delete fast?`,
          message: $localize`The fast from ${f.fast_start_time} will be removed.`,
          confirm: $localize`Delete`,
        },
      })
      .afterClosed()
      .subscribe(async (confirmed) => {
        if (!confirmed) return;
        try {
          await firstValueFrom(this.http.delete(`/api/v1/health/fasting/${f.id}`));
          this.reload();
        } catch (err) {
          this.snack.open(problemMessage(err), $localize`Dismiss`, { duration: 8000 });
        }
      });
  }
}
