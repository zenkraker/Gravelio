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
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { Paged, PoopEntry, problemMessage } from '../core/api';
import { AuthService } from '../core/auth';
import { formatDateTime } from '../core/format';
import { OUTLINE_FIELDS } from '../core/outline-fields';
import { ConfirmDialog } from '../components/confirm-dialog';

const PAGE_SIZE = 20;
const BRISTOL = [
  { value: '1', label: '1 — Separate hard lumps' },
  { value: '2', label: '2 — Lumpy sausage' },
  { value: '3', label: '3 — Sausage with cracks' },
  { value: '4', label: '4 — Smooth soft sausage' },
  { value: '5', label: '5 — Soft blobs' },
  { value: '6', label: '6 — Mushy' },
  { value: '7', label: '7 — Liquid' },
];
const COLORS = ['brown', 'dark_brown', 'light_brown', 'yellow', 'green', 'black', 'red', 'white'];
const empty = { dateTime: '', bristol: '4', color: 'brown', notes: '' };

@Component({
  selector: 'app-poop',
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
    <h1 i18n>Bowel movements</h1>

    <mat-card appearance="outlined">
      <mat-card-content>
        <h2 i18n>Add a record</h2>
        <form novalidate (submit)="save($event)">
          <mat-form-field>
            <mat-label i18n>Date and time</mat-label>
            <input matInput type="datetime-local" [formField]="f.dateTime" />
          </mat-form-field>
          <mat-form-field>
            <mat-label i18n>Bristol type</mat-label>
            <mat-select [formField]="f.bristol">
              @for (b of bristol; track b.value) {
                <mat-option [value]="b.value">{{ b.label }}</mat-option>
              }
            </mat-select>
          </mat-form-field>
          <mat-form-field>
            <mat-label i18n>Color</mat-label>
            <mat-select [formField]="f.color">
              @for (color of colors; track color) {
                <mat-option [value]="color">{{ colorLabel(color) }}</mat-option>
              }
            </mat-select>
          </mat-form-field>
          <mat-form-field class="notes">
            <mat-label i18n>Notes</mat-label>
            <textarea matInput rows="2" [formField]="f.notes"></textarea>
          </mat-form-field>
          @if (error()) {
            <p class="error" role="alert">{{ error() }}</p>
          }
          <button mat-flat-button type="submit" [disabled]="f().submitting()" i18n>
            Save record
          </button>
        </form>
      </mat-card-content>
    </mat-card>

    @if (entries.error()) {
      <p class="error" role="alert" i18n>Could not load your records.</p>
    }
    <table
      mat-table
      [dataSource]="entries.value()?.items ?? []"
      aria-label="Bowel movements, newest first"
      i18n-aria-label
    >
      <ng-container matColumnDef="date">
        <th mat-header-cell *matHeaderCellDef i18n>Date and time</th>
        <td mat-cell *matCellDef="let p">{{ when(p.date_time) }}</td>
      </ng-container>
      <ng-container matColumnDef="bristol">
        <th mat-header-cell *matHeaderCellDef i18n>Bristol type</th>
        <td mat-cell *matCellDef="let p">{{ bristolLabel(p.bristol_type) }}</td>
      </ng-container>
      <ng-container matColumnDef="color">
        <th mat-header-cell *matHeaderCellDef i18n>Color</th>
        <td mat-cell *matCellDef="let p">{{ p.color ? colorLabel(p.color) : '—' }}</td>
      </ng-container>
      <ng-container matColumnDef="notes">
        <th mat-header-cell *matHeaderCellDef i18n>Notes</th>
        <td mat-cell *matCellDef="let p">{{ p.notes ?? '' }}</td>
      </ng-container>
      <ng-container matColumnDef="actions">
        <th mat-header-cell *matHeaderCellDef>
          <span class="cdk-visually-hidden" i18n>Actions</span>
        </th>
        <td mat-cell *matCellDef="let p">
          <button
            mat-button
            type="button"
            (click)="confirmDelete(p)"
            [attr.aria-label]="deleteLabel(p)"
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
          <td class="mat-mdc-cell" [attr.colspan]="columns.length" i18n>No records yet.</td>
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
export class Poop {
  private http = inject(HttpClient);
  private snack = inject(MatSnackBar);
  private dialog = inject(MatDialog);
  private auth = inject(AuthService);

  protected readonly bristol = BRISTOL;
  protected readonly colors = COLORS;
  protected readonly columns = ['date', 'bristol', 'color', 'notes', 'actions'];
  protected readonly pageSize = PAGE_SIZE;
  protected readonly pageIndex = signal(1);
  protected readonly error = signal('');

  protected readonly entries = httpResource<Paged<PoopEntry>>(() => ({
    url: '/api/v1/health/poop',
    params: { page: this.pageIndex(), page_size: PAGE_SIZE },
  }));

  private readonly model = signal({ ...empty });
  protected readonly f = form(this.model, (p) => {
    required(p.dateTime, { message: $localize`Enter the date and time` });
  });

  protected when(iso: string | null): string {
    return formatDateTime(iso, this.auth.user()?.timezone ?? null);
  }

  protected bristolLabel(value: number | null): string {
    switch (value) {
      case 1:
        return $localize`1 — Separate hard lumps`;
      case 2:
        return $localize`2 — Lumpy sausage`;
      case 3:
        return $localize`3 — Sausage with cracks`;
      case 4:
        return $localize`4 — Smooth soft sausage`;
      case 5:
        return $localize`5 — Soft blobs`;
      case 6:
        return $localize`6 — Mushy`;
      case 7:
        return $localize`7 — Liquid`;
      default:
        return '—';
    }
  }

  protected colorLabel(color: string): string {
    switch (color) {
      case 'brown':
        return $localize`Brown`;
      case 'dark_brown':
        return $localize`Dark brown`;
      case 'light_brown':
        return $localize`Light brown`;
      case 'yellow':
        return $localize`Yellow`;
      case 'green':
        return $localize`Green`;
      case 'black':
        return $localize`Black`;
      case 'red':
        return $localize`Red`;
      case 'white':
        return $localize`White`;
      default:
        return color;
    }
  }

  protected deleteLabel(p: PoopEntry): string {
    return $localize`Delete the bowel movement from ${p.date_time}`;
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
          this.http.post('/api/v1/health/poop', {
            date_time: v.dateTime,
            bristol_type: Number(v.bristol),
            color: v.color,
            notes: v.notes || null,
          }),
        );
        this.model.update((m) => ({ ...m, dateTime: '', notes: '' }));
        this.entries.reload();
        this.snack.open($localize`Bowel movement saved`, undefined, { duration: 4000 });
      } catch (err) {
        this.error.set(problemMessage(err));
      }
    });
  }

  protected confirmDelete(p: PoopEntry): void {
    this.dialog
      .open(ConfirmDialog, {
        data: {
          title: $localize`Delete record?`,
          message: $localize`The record from ${p.date_time} will be removed.`,
          confirm: $localize`Delete`,
        },
      })
      .afterClosed()
      .subscribe(async (confirmed) => {
        if (!confirmed) return;
        try {
          await firstValueFrom(this.http.delete(`/api/v1/health/poop/${p.id}`));
          this.entries.reload();
        } catch (err) {
          this.snack.open(problemMessage(err), $localize`Dismiss`, { duration: 8000 });
        }
      });
  }
}
