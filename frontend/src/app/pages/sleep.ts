import { DatePipe } from '@angular/common';
import { HttpClient, httpResource } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormField, form, required, submit } from '@angular/forms/signals';
import { firstValueFrom } from 'rxjs';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { Paged, SleepEntry, problemMessage } from '../core/api';
import { OUTLINE_FIELDS } from '../core/outline-fields';
import { ConfirmDialog } from '../components/confirm-dialog';

const PAGE_SIZE = 20;
const emptyEntry = {
  date: '',
  totalHours: '',
  deepMinutes: '',
  lightMinutes: '',
  remMinutes: '',
  awakeMinutes: '',
  awakeCount: '',
  stress: '',
};

@Component({
  selector: 'app-sleep',
  providers: [OUTLINE_FIELDS],
  imports: [
    DatePipe,
    FormField,
    MatButtonModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatPaginatorModule,
    MatTableModule,
  ],
  template: `
    <h1 i18n>Sleep</h1>

    <mat-card appearance="outlined">
      <mat-card-content>
        <h2 i18n>Add a sleep record</h2>
        <p i18n>
          Saving a second record for the same day replaces the first. Scores are calculated here.
        </p>
        <form novalidate (submit)="save($event)">
          <mat-form-field>
            <mat-label i18n>Date</mat-label>
            <input matInput type="date" [formField]="f.date" />
          </mat-form-field>
          <mat-form-field>
            <mat-label i18n>Total sleep (hours)</mat-label>
            <input matInput type="number" [formField]="f.totalHours" />
          </mat-form-field>
          <mat-form-field>
            <mat-label i18n>Deep sleep (minutes)</mat-label>
            <input matInput type="number" [formField]="f.deepMinutes" />
          </mat-form-field>
          <mat-form-field>
            <mat-label i18n>Light sleep (minutes)</mat-label>
            <input matInput type="number" [formField]="f.lightMinutes" />
          </mat-form-field>
          <mat-form-field>
            <mat-label i18n>REM sleep (minutes)</mat-label>
            <input matInput type="number" [formField]="f.remMinutes" />
          </mat-form-field>
          <mat-form-field>
            <mat-label i18n>Awake (minutes)</mat-label>
            <input matInput type="number" [formField]="f.awakeMinutes" />
          </mat-form-field>
          <mat-form-field>
            <mat-label i18n>Awakenings</mat-label>
            <input matInput type="number" [formField]="f.awakeCount" />
          </mat-form-field>
          <mat-form-field>
            <mat-label i18n>Average stress</mat-label>
            <input matInput type="number" [formField]="f.stress" />
          </mat-form-field>
          @if (error()) {
            <p class="error" role="alert">{{ error() }}</p>
          }
          <button mat-flat-button type="submit" [disabled]="f().submitting()" i18n>
            Save sleep
          </button>
        </form>
      </mat-card-content>
    </mat-card>

    @if (entries.error()) {
      <p class="error" role="alert" i18n>Could not load your sleep records.</p>
    }
    <table
      mat-table
      [dataSource]="entries.value()?.items ?? []"
      aria-label="Sleep records, newest first"
      i18n-aria-label
    >
      <ng-container matColumnDef="date">
        <th mat-header-cell *matHeaderCellDef i18n>Date</th>
        <td mat-cell *matCellDef="let e">{{ e.date | date: 'mediumDate' }}</td>
      </ng-container>
      <ng-container matColumnDef="total">
        <th mat-header-cell *matHeaderCellDef i18n>Sleep</th>
        <td mat-cell *matCellDef="let e">{{ total(e.total_sleep_seconds) }}</td>
      </ng-container>
      <ng-container matColumnDef="score">
        <th mat-header-cell *matHeaderCellDef i18n>Score</th>
        <td mat-cell *matCellDef="let e">
          {{ e.sleep_score_overall ?? '—' }} · {{ scoreLabel(e.sleep_score_quality) }}
        </td>
      </ng-container>
      <ng-container matColumnDef="actions">
        <th mat-header-cell *matHeaderCellDef>
          <span class="cdk-visually-hidden" i18n>Actions</span>
        </th>
        <td mat-cell *matCellDef="let e">
          <button
            mat-button
            type="button"
            (click)="confirmDelete(e)"
            [attr.aria-label]="deleteLabel(e)"
            i18n
          >
            Delete
          </button>
        </td>
      </ng-container>
      <tr mat-header-row *matHeaderRowDef="columns"></tr>
      <tr mat-row *matRowDef="let row; columns: columns"></tr>
      @if (!entries.isLoading()) {
        <tr class="mat-mdc-row" *matNoDataRow>
          <td class="mat-mdc-cell" [attr.colspan]="columns.length" i18n>No sleep records yet.</td>
        </tr>
      }
    </table>
    <mat-paginator
      [length]="entries.value()?.total ?? 0"
      [pageSize]="pageSize"
      [pageIndex]="pageIndex() - 1"
      [hidePageSize]="true"
      (page)="onPage($event)"
    />
  `,
  styles: `
    form {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(200px, 1fr));
      gap: 8px 16px;
      align-items: start;
      padding-top: 8px;
    }
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
export class Sleep {
  private http = inject(HttpClient);
  private snack = inject(MatSnackBar);
  private dialog = inject(MatDialog);

  protected readonly columns = ['date', 'total', 'score', 'actions'];
  protected readonly pageSize = PAGE_SIZE;
  protected readonly pageIndex = signal(1);
  protected readonly error = signal('');

  protected readonly entries = httpResource<Paged<SleepEntry>>(() => ({
    url: '/api/v1/health/sleep',
    params: { page: this.pageIndex(), page_size: PAGE_SIZE },
  }));

  private readonly model = signal({ ...emptyEntry });
  protected readonly f = form(this.model, (p) => {
    required(p.date, { message: $localize`Enter the date` });
    required(p.totalHours, { message: $localize`Enter the total sleep` });
  });

  protected total(seconds: number | null): string {
    if (seconds == null) return '—';
    const hours = Math.floor(seconds / 3600);
    const minutes = Math.round((seconds % 3600) / 60);
    return $localize`${hours}h ${minutes}m`;
  }

  protected scoreLabel(score: string | null): string {
    switch (score) {
      case 'EXCELLENT':
        return $localize`Excellent`;
      case 'GOOD':
        return $localize`Good`;
      case 'FAIR':
        return $localize`Fair`;
      case 'POOR':
        return $localize`Poor`;
      default:
        return '—';
    }
  }

  protected deleteLabel(e: SleepEntry): string {
    return $localize`Delete the sleep record from ${e.date}`;
  }

  protected onPage(event: PageEvent): void {
    this.pageIndex.set(event.pageIndex + 1);
  }

  protected async save(event: Event): Promise<void> {
    event.preventDefault();
    this.error.set('');
    await submit(this.f, async () => {
      try {
        const v = this.model();
        const minutes = (value: string) => (value === '' ? null : Math.round(Number(value) * 60));
        await firstValueFrom(
          this.http.post('/api/v1/health/sleep', {
            date: v.date,
            total_sleep_seconds: Math.round(Number(v.totalHours) * 3600),
            deep_sleep_seconds: minutes(v.deepMinutes),
            light_sleep_seconds: minutes(v.lightMinutes),
            rem_sleep_seconds: minutes(v.remMinutes),
            awake_sleep_seconds: minutes(v.awakeMinutes),
            awake_count: v.awakeCount === '' ? null : Number(v.awakeCount),
            avg_sleep_stress: v.stress === '' ? null : Number(v.stress),
          }),
        );
        this.model.set({ ...emptyEntry });
        this.entries.reload();
        this.snack.open($localize`Sleep saved`, undefined, { duration: 4000 });
      } catch (err) {
        this.error.set(problemMessage(err));
      }
    });
  }

  protected confirmDelete(e: SleepEntry): void {
    this.dialog
      .open(ConfirmDialog, {
        data: {
          title: $localize`Delete sleep record?`,
          message: $localize`The record from ${e.date} will be removed.`,
          confirm: $localize`Delete`,
        },
      })
      .afterClosed()
      .subscribe(async (confirmed) => {
        if (!confirmed) return;
        try {
          await firstValueFrom(this.http.delete(`/api/v1/health/sleep/${e.id}`));
          this.entries.reload();
        } catch (err) {
          this.snack.open(problemMessage(err), $localize`Dismiss`, { duration: 8000 });
        }
      });
  }
}
