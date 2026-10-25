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
import { Paged, WaterEntry, problemMessage } from '../core/api';
import { OUTLINE_FIELDS } from '../core/outline-fields';
import { ConfirmDialog } from '../components/confirm-dialog';

const PAGE_SIZE = 20;
const emptyEntry = { date: '', amount: '' };

@Component({
  selector: 'app-water',
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
    <h1 i18n>Water</h1>

    <mat-card appearance="outlined">
      <mat-card-content>
        <h2 i18n>Add water</h2>
        <p i18n>Each save adds to that day's total.</p>
        <form novalidate (submit)="save($event)">
          <mat-form-field>
            <mat-label i18n>Date</mat-label>
            <input matInput type="date" [formField]="f.date" />
          </mat-form-field>
          <mat-form-field>
            <mat-label i18n>Amount (ml)</mat-label>
            <input matInput type="number" [formField]="f.amount" />
          </mat-form-field>
          @if (error()) {
            <p class="error" role="alert">{{ error() }}</p>
          }
          <button mat-flat-button type="submit" [disabled]="f().submitting()" i18n>
            Add water
          </button>
        </form>
      </mat-card-content>
    </mat-card>

    @if (entries.error()) {
      <p class="error" role="alert" i18n>Could not load your water records.</p>
    }
    <table
      mat-table
      [dataSource]="entries.value()?.items ?? []"
      aria-label="Water intake, newest first"
      i18n-aria-label
    >
      <ng-container matColumnDef="date">
        <th mat-header-cell *matHeaderCellDef i18n>Date</th>
        <td mat-cell *matCellDef="let e">{{ e.date | date: 'mediumDate' }}</td>
      </ng-container>
      <ng-container matColumnDef="amount">
        <th mat-header-cell *matHeaderCellDef i18n>Amount</th>
        <td mat-cell *matCellDef="let e">{{ e.amount_ml }} ml</td>
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
          <td class="mat-mdc-cell" [attr.colspan]="columns.length" i18n>No water records yet.</td>
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
export class Water {
  private http = inject(HttpClient);
  private snack = inject(MatSnackBar);
  private dialog = inject(MatDialog);

  protected readonly columns = ['date', 'amount', 'actions'];
  protected readonly pageSize = PAGE_SIZE;
  protected readonly pageIndex = signal(1);
  protected readonly error = signal('');

  protected readonly entries = httpResource<Paged<WaterEntry>>(() => ({
    url: '/api/v1/health/water',
    params: { page: this.pageIndex(), page_size: PAGE_SIZE },
  }));

  private readonly model = signal({ ...emptyEntry });
  protected readonly f = form(this.model, (p) => {
    required(p.date, { message: $localize`Enter the date` });
    required(p.amount, { message: $localize`Enter the amount` });
  });

  protected deleteLabel(e: WaterEntry): string {
    return $localize`Delete the water from ${e.date}`;
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
        await firstValueFrom(
          this.http.post('/api/v1/health/water', {
            date: v.date,
            amount_ml: Number(v.amount),
          }),
        );
        this.model.set({ ...emptyEntry });
        this.entries.reload();
        this.snack.open($localize`Water added`, undefined, { duration: 4000 });
      } catch (err) {
        this.error.set(problemMessage(err));
      }
    });
  }

  protected confirmDelete(e: WaterEntry): void {
    this.dialog
      .open(ConfirmDialog, {
        data: {
          title: $localize`Delete water?`,
          message: $localize`The water total from ${e.date} will be removed.`,
          confirm: $localize`Delete`,
        },
      })
      .afterClosed()
      .subscribe(async (confirmed) => {
        if (!confirmed) return;
        try {
          await firstValueFrom(this.http.delete(`/api/v1/health/water/${e.id}`));
          this.entries.reload();
        } catch (err) {
          this.snack.open(problemMessage(err), $localize`Dismiss`, { duration: 8000 });
        }
      });
  }
}
