import { HttpClient, httpResource } from '@angular/common/http';
import { DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormField, form, required, submit } from '@angular/forms/signals';
import { firstValueFrom } from 'rxjs';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { OUTLINE_FIELDS } from '../core/outline-fields';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { Paged, WeightEntry, problemMessage } from '../core/api';
import { AuthService } from '../core/auth';
import { displayToKg, kgToDisplay, weightUnitLabel } from '../core/format';
import { ConfirmDialog } from '../components/confirm-dialog';

const PAGE_SIZE = 20;
const emptyEntry = { date: '', weight: '', bodyFat: '' };

@Component({
  selector: 'app-weight',
  providers: [OUTLINE_FIELDS],
  imports: [
    DatePipe,
    FormField,
    MatButtonModule,
    MatCardModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatPaginatorModule,
    MatTableModule,
  ],
  template: `
    <h1 i18n>Weight</h1>

    <mat-card appearance="outlined">
      <mat-card-content>
        <h2 i18n>Add a reading</h2>
        <p i18n>Saving a second reading for the same day replaces the first.</p>
        <form novalidate (submit)="save($event)">
          <mat-form-field>
            <mat-label i18n>Date</mat-label>
            <input matInput type="date" [formField]="f.date" />
          </mat-form-field>
          <mat-form-field>
            <mat-label>{{ weightLabel() }}</mat-label>
            <input matInput type="number" step="any" [formField]="f.weight" />
          </mat-form-field>
          <mat-form-field>
            <mat-label i18n>Body fat (%)</mat-label>
            <input matInput type="number" step="any" [formField]="f.bodyFat" />
          </mat-form-field>
          @if (error()) {
            <p class="error" role="alert">{{ error() }}</p>
          }
          <button mat-flat-button type="submit" [disabled]="f().submitting()" i18n>
            Save reading
          </button>
        </form>
      </mat-card-content>
    </mat-card>

    @if (entries.error()) {
      <p class="error" role="alert" i18n>Could not load your readings.</p>
    }
    <table
      mat-table
      [dataSource]="entries.value()?.items ?? []"
      aria-label="Weight readings, newest first"
      i18n-aria-label
    >
      <ng-container matColumnDef="date">
        <th mat-header-cell *matHeaderCellDef i18n>Date</th>
        <td mat-cell *matCellDef="let e">{{ e.date | date: 'mediumDate' }}</td>
      </ng-container>
      <ng-container matColumnDef="weight">
        <th mat-header-cell *matHeaderCellDef>{{ weightLabel() }}</th>
        <td mat-cell *matCellDef="let e">{{ shown(e.weight) }}</td>
      </ng-container>
      <ng-container matColumnDef="bodyFat">
        <th mat-header-cell *matHeaderCellDef i18n>Body fat</th>
        <td mat-cell *matCellDef="let e">{{ e.body_fat === null ? '' : e.body_fat + ' %' }}</td>
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
          <td class="mat-mdc-cell" [attr.colspan]="columns.length" i18n>No readings yet.</td>
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
export class Weight {
  private http = inject(HttpClient);
  private snack = inject(MatSnackBar);
  private dialog = inject(MatDialog);
  private auth = inject(AuthService);

  protected readonly columns = ['date', 'weight', 'bodyFat', 'actions'];
  protected readonly pageSize = PAGE_SIZE;
  protected readonly pageIndex = signal(1);
  protected readonly error = signal('');

  private readonly units = computed(() => this.auth.user()?.units ?? 'metric');
  protected readonly weightLabel = computed(
    () => $localize`Weight (${weightUnitLabel(this.units())})`,
  );
  protected readonly shown = (kg: number) =>
    `${kgToDisplay(kg, this.units())} ${weightUnitLabel(this.units())}`;

  protected readonly entries = httpResource<Paged<WeightEntry>>(() => ({
    url: '/api/v1/health/weight',
    params: { page: this.pageIndex(), page_size: PAGE_SIZE },
  }));

  private readonly model = signal({ ...emptyEntry });
  protected readonly f = form(this.model, (p) => {
    required(p.date, { message: $localize`Enter the date` });
    required(p.weight, { message: $localize`Enter your weight` });
  });

  protected deleteLabel(e: WeightEntry): string {
    return $localize`Delete the reading from ${e.date}`;
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
          this.http.post('/api/v1/health/weight', {
            date: v.date,
            weight: displayToKg(Number(v.weight), this.units()),
            body_fat: v.bodyFat === '' ? null : Number(v.bodyFat),
          }),
        );
        this.model.set({ ...emptyEntry });
        this.entries.reload();
        this.snack.open($localize`Reading saved`, undefined, { duration: 4000 });
      } catch (err) {
        this.error.set(problemMessage(err));
      }
    });
  }

  protected confirmDelete(e: WeightEntry): void {
    this.dialog
      .open(ConfirmDialog, {
        data: {
          title: $localize`Delete reading?`,
          message: $localize`The reading from ${e.date} will be removed.`,
          confirm: $localize`Delete`,
        },
      })
      .afterClosed()
      .subscribe(async (confirmed) => {
        if (!confirmed) return;
        try {
          await firstValueFrom(this.http.delete(`/api/v1/health/weight/${e.id}`));
          this.entries.reload();
        } catch (err) {
          this.snack.open(problemMessage(err), $localize`Dismiss`, { duration: 8000 });
        }
      });
  }
}
