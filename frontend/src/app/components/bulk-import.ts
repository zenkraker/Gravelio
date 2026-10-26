import { HttpClient } from '@angular/common/http';
import { Component, computed, effect, inject, output, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSnackBar } from '@angular/material/snack-bar';
import { ImportJob, Paged, problemMessage } from '../core/api';

const ACTIVE = ['queued', 'running'];

/** Imports a zip of GPX, TCX and FIT files in the background and shows how far it has got. */
@Component({
  selector: 'app-bulk-import',
  imports: [MatButtonModule, MatCardModule, MatIconModule, MatProgressBarModule],
  template: `
    <input
      #zip
      class="cdk-visually-hidden"
      type="file"
      accept=".zip"
      aria-label="Bulk import zip"
      i18n-aria-label
      tabindex="-1"
      (change)="start(zip)"
    />
    <button mat-button type="button" [disabled]="busy() || active()" (click)="zip.click()">
      <mat-icon aria-hidden="true">folder_zip</mat-icon
      ><span i18n>Import a zip of activity files</span>
    </button>
    @if (job(); as j) {
      <mat-card appearance="outlined" class="job">
        <mat-card-content>
          <p role="status">{{ summary(j) }}</p>
          <mat-progress-bar
            mode="determinate"
            [value]="percent()"
            aria-label="Import progress"
            i18n-aria-label
          />
          @if (j.error) {
            <p class="error" role="alert">{{ j.error }}</p>
          }
          @if (j.failures.length) {
            <p i18n>These files could not be imported:</p>
            <ul>
              @for (f of j.failures; track f.file) {
                <li>
                  <code>{{ f.file }}</code> — {{ f.error }}
                </li>
              }
            </ul>
          }
          @if (active()) {
            <button mat-button type="button" (click)="cancel(j)" i18n>Cancel import</button>
          } @else {
            <button mat-button type="button" (click)="job.set(null)" i18n>Dismiss</button>
          }
        </mat-card-content>
      </mat-card>
    }
  `,
  styles: `
    .job {
      margin: 12px 0;
    }
    .error {
      color: var(--mat-sys-error);
    }
  `,
})
export class BulkImport {
  private readonly http = inject(HttpClient);
  private readonly snack = inject(MatSnackBar);

  /** Emits when an import this page watched has finished, so the list can show what arrived. */
  readonly finished = output<void>();

  protected readonly job = signal<ImportJob | null>(null);
  protected readonly busy = signal(false);
  protected readonly active = computed(() => ACTIVE.includes(this.job()?.status ?? ''));
  protected readonly percent = computed(() => {
    const j = this.job();
    return j && j.total_files ? Math.round((100 * j.processed_files) / j.total_files) : 0;
  });

  constructor() {
    // An import started earlier (or in another tab) is picked up where it is.
    void this.refresh(true);
    effect((onCleanup) => {
      // Depends on the job itself, not only on whether it is active: each answer schedules the next poll.
      const j = this.job();
      if (!j || !ACTIVE.includes(j.status)) return;
      const timer = setTimeout(() => void this.refresh(false), 2000);
      onCleanup(() => clearTimeout(timer));
    });
  }

  protected summary(j: ImportJob): string {
    const done = $localize`${j.processed_files} of ${j.total_files} files`;
    switch (j.status) {
      case 'queued':
        return $localize`Waiting to start (${j.total_files} files).`;
      case 'running':
        return $localize`Importing: ${done}, ${j.imported} activities added.`;
      case 'completed':
        return $localize`Import finished: ${j.imported} activities added, ${j.skipped} files already there, ${j.hidden_duplicates} kept hidden as duplicates, ${j.failed} failed.`;
      case 'cancelled':
        return $localize`Import cancelled after ${done}; ${j.imported} activities were added.`;
      default:
        return $localize`Import stopped after ${done}; ${j.imported} activities were added.`;
    }
  }

  protected async start(input: HTMLInputElement): Promise<void> {
    const file = input.files?.[0];
    input.value = '';
    if (!file) return;
    this.busy.set(true);
    try {
      const body = new FormData();
      body.append('file', file);
      // A fresh key per pick: a retry of this very request (a dropped connection) finds the job it already made.
      this.job.set(
        await firstValueFrom(
          this.http.post<ImportJob>('/api/v1/activities/import/zip', body, {
            headers: { 'Idempotency-Key': crypto.randomUUID() },
          }),
        ),
      );
    } catch (err) {
      this.snack.open(problemMessage(err), $localize`Dismiss`, { duration: 8000 });
    } finally {
      this.busy.set(false);
    }
  }

  protected async cancel(j: ImportJob): Promise<void> {
    try {
      this.job.set(
        await firstValueFrom(
          this.http.post<ImportJob>(`/api/v1/activities/import/jobs/${j.id}/cancel`, {}),
        ),
      );
    } catch (err) {
      this.snack.open(problemMessage(err), $localize`Dismiss`, { duration: 8000 });
    }
  }

  private async refresh(initial: boolean): Promise<void> {
    try {
      const page = await firstValueFrom(
        this.http.get<Paged<ImportJob>>('/api/v1/activities/import/jobs', {
          params: { page_size: 1 },
        }),
      );
      const latest = page.items[0] ?? null;
      const before = this.job();
      // On load only an import still in progress is worth showing; afterwards, follow the one being watched.
      if (initial ? latest && ACTIVE.includes(latest.status) : latest?.id === before?.id)
        this.job.set(latest);
      if (before && ACTIVE.includes(before.status) && latest && !ACTIVE.includes(latest.status))
        this.finished.emit();
    } catch {
      // A missed poll is tried again on the next tick.
    }
  }
}
