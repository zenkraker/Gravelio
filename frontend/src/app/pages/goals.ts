import { HttpClient, httpResource } from '@angular/common/http';
import { DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormField, form, required, submit } from '@angular/forms/signals';
import { firstValueFrom } from 'rxjs';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { GoalProgress, problemMessage } from '../core/api';
import { AuthService } from '../core/auth';
import {
  GOAL_GROUPS,
  GOAL_INTERVALS,
  GOAL_METRICS,
  displayToTarget,
  formatGoalAmount,
  groupLabel,
  intervalLabel,
  metricLabel,
  targetToDisplay,
  targetUnit,
} from '../core/goals';
import { OUTLINE_FIELDS } from '../core/outline-fields';
import { ConfirmDialog } from '../components/confirm-dialog';

const empty = { interval: 'weekly', activityGroup: 'run', metric: 'distance', target: '' };

@Component({
  selector: 'app-goals',
  providers: [OUTLINE_FIELDS],
  imports: [
    DatePipe,
    FormField,
    MatButtonModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatProgressBarModule,
    MatSelectModule,
    MatTableModule,
  ],
  template: `
    <h1 i18n>Goals</h1>
    <p i18n>
      A goal is a target for one kind of activity over a day, week, month or year. Progress counts
      the activities of the current period, by the day each one was recorded.
    </p>

    @if (progress.error()) {
      <p class="error" role="alert" i18n>Could not load your goals.</p>
    }
    <table
      mat-table
      [dataSource]="progress.value() ?? []"
      aria-label="Goals and their progress"
      i18n-aria-label
    >
      <ng-container matColumnDef="goal">
        <th mat-header-cell *matHeaderCellDef i18n>Goal</th>
        <td mat-cell *matCellDef="let g">{{ title(g) }}</td>
      </ng-container>
      <ng-container matColumnDef="progress">
        <th mat-header-cell *matHeaderCellDef i18n>Progress</th>
        <td mat-cell *matCellDef="let g">
          <mat-progress-bar mode="determinate" [value]="g.percent" [attr.aria-label]="title(g)" />
          <span class="amount"
            >{{ amount(g, g.total) }} / {{ amount(g, g.target) }} ({{ g.percent }}%)</span
          >
        </td>
      </ng-container>
      <ng-container matColumnDef="period">
        <th mat-header-cell *matHeaderCellDef i18n>Period</th>
        <td mat-cell *matCellDef="let g">
          {{ g.start | date: 'mediumDate' }} – {{ g.end | date: 'mediumDate' }}
        </td>
      </ng-container>
      <ng-container matColumnDef="actions">
        <th mat-header-cell *matHeaderCellDef>
          <span class="cdk-visually-hidden" i18n>Actions</span>
        </th>
        <td mat-cell *matCellDef="let g">
          <button
            mat-button
            type="button"
            (click)="startEdit(g)"
            [attr.aria-label]="editLabel(g)"
            i18n
          >
            Edit
          </button>
          <button
            mat-button
            type="button"
            (click)="confirmDelete(g)"
            [attr.aria-label]="deleteLabel(g)"
            i18n
          >
            Delete
          </button>
        </td>
      </ng-container>
      <tr mat-header-row *matHeaderRowDef="columns"></tr>
      <tr mat-row *matRowDef="let row; columns: columns"></tr>
      @if (!progress.isLoading()) {
        <tr class="mat-mdc-row" *matNoDataRow>
          <td class="mat-mdc-cell" [attr.colspan]="columns.length" i18n>No goals yet.</td>
        </tr>
      }
    </table>

    <h2>{{ editing() ? editTitle : addTitle }}</h2>
    <mat-card appearance="outlined">
      <mat-card-content>
        <form novalidate (submit)="save($event)">
          <mat-form-field>
            <mat-label i18n>Period</mat-label>
            <mat-select [formField]="f.interval">
              @for (i of intervals; track i.id) {
                <mat-option [value]="i.id">{{ i.label }}</mat-option>
              }
            </mat-select>
          </mat-form-field>
          <mat-form-field>
            <mat-label i18n>Activity</mat-label>
            <mat-select [formField]="f.activityGroup">
              @for (g of groups; track g.id) {
                <mat-option [value]="g.id">{{ g.label }}</mat-option>
              }
            </mat-select>
          </mat-form-field>
          <mat-form-field>
            <mat-label i18n>Measure</mat-label>
            <mat-select [formField]="f.metric">
              @for (m of metrics; track m.id) {
                <mat-option [value]="m.id">{{ m.label }}</mat-option>
              }
            </mat-select>
          </mat-form-field>
          <mat-form-field>
            <mat-label>{{ targetLabel() }}</mat-label>
            <input matInput type="number" step="any" [formField]="f.target" />
          </mat-form-field>
          @if (error()) {
            <p class="error" role="alert">{{ error() }}</p>
          }
          <div class="actions">
            <button mat-flat-button type="submit" [disabled]="f().submitting()" i18n>
              Save goal
            </button>
            @if (editing()) {
              <button mat-button type="button" (click)="cancelEdit()" i18n>Cancel</button>
            }
          </div>
        </form>
      </mat-card-content>
    </mat-card>
  `,
  styles: `
    table {
      width: 100%;
    }
    mat-progress-bar {
      min-width: 160px;
    }
    .amount {
      font: var(--mat-sys-body-small);
      color: var(--mat-sys-on-surface-variant);
    }
    form {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(220px, 1fr));
      gap: 8px 16px;
      padding-top: 16px;
    }
    .actions,
    .error {
      grid-column: 1 / -1;
    }
    .actions {
      display: flex;
      gap: 8px;
    }
    .error {
      color: var(--mat-sys-error);
      margin: 0;
    }
  `,
})
export class Goals {
  private http = inject(HttpClient);
  private dialog = inject(MatDialog);
  private snack = inject(MatSnackBar);
  private auth = inject(AuthService);

  protected readonly columns = ['goal', 'progress', 'period', 'actions'];
  protected readonly intervals = GOAL_INTERVALS;
  protected readonly groups = GOAL_GROUPS;
  protected readonly metrics = GOAL_METRICS;
  protected readonly addTitle = $localize`Add a goal`;
  protected readonly editTitle = $localize`Edit goal`;

  private readonly units = computed(() => this.auth.user()?.units ?? 'metric');
  protected readonly progress = httpResource<GoalProgress[]>(() => '/api/v1/goals/progress');
  protected readonly error = signal('');
  protected readonly editing = signal<GoalProgress | null>(null);

  private readonly model = signal({ ...empty });
  protected readonly f = form(this.model, (p) => {
    required(p.target, { message: $localize`Enter a target` });
  });
  protected readonly targetLabel = computed(() => {
    const unit = targetUnit(this.model().metric, this.units());
    return unit ? $localize`Target (${unit})` : $localize`Target`;
  });

  protected title(g: GoalProgress): string {
    return `${intervalLabel(g.interval)} · ${groupLabel(g.activity_group)} · ${metricLabel(g.metric)}`;
  }

  protected amount(g: GoalProgress, stored: number): string {
    return formatGoalAmount(g.metric, stored, this.units());
  }

  protected editLabel(g: GoalProgress): string {
    return $localize`Edit goal ${this.title(g)}`;
  }

  protected deleteLabel(g: GoalProgress): string {
    return $localize`Delete goal ${this.title(g)}`;
  }

  protected startEdit(g: GoalProgress): void {
    this.error.set('');
    this.editing.set(g);
    this.model.set({
      interval: g.interval,
      activityGroup: g.activity_group,
      metric: g.metric,
      target: String(targetToDisplay(g.metric, g.target, this.units())),
    });
  }

  protected cancelEdit(): void {
    this.editing.set(null);
    this.model.set({ ...empty });
  }

  protected async save(event: Event): Promise<void> {
    event.preventDefault();
    this.error.set('');
    await submit(this.f, async () => {
      try {
        const m = this.model();
        const body = {
          interval: m.interval,
          activity_group: m.activityGroup,
          metric: m.metric,
          target: displayToTarget(m.metric, Number(m.target), this.units()),
        };
        const editing = this.editing();
        await firstValueFrom(
          editing
            ? this.http.put(`/api/v1/goals/${editing.goal_id}`, body)
            : this.http.post('/api/v1/goals', body),
        );
        this.cancelEdit();
        this.progress.reload();
        this.snack.open($localize`Goal saved`, undefined, { duration: 4000 });
      } catch (err) {
        this.error.set(problemMessage(err));
      }
    });
  }

  protected confirmDelete(g: GoalProgress): void {
    this.dialog
      .open(ConfirmDialog, {
        data: {
          title: $localize`Delete goal?`,
          message: $localize`“${this.title(g)}” will be removed. Your activities are not affected.`,
          confirm: $localize`Delete`,
        },
      })
      .afterClosed()
      .subscribe(async (confirmed) => {
        if (!confirmed) return;
        try {
          await firstValueFrom(this.http.delete(`/api/v1/goals/${g.goal_id}`));
          if (this.editing()?.goal_id === g.goal_id) this.cancelEdit();
          this.progress.reload();
        } catch (err) {
          this.snack.open(problemMessage(err), $localize`Dismiss`, { duration: 8000 });
        }
      });
  }
}
