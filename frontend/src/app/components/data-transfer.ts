import { HttpClient } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSnackBar } from '@angular/material/snack-bar';
import { ImportDataResult, problemMessage } from '../core/api';

/** Download everything you own as one zip, or restore such a zip into this account. */
@Component({
  selector: 'app-data-transfer',
  imports: [MatButtonModule, MatCardModule, MatIconModule, MatProgressBarModule],
  template: `
    <h2 i18n>Your data</h2>
    <mat-card appearance="outlined">
      <mat-card-content>
        <p i18n>
          Export your activities, gear, goals and health records as a zip file. Importing a file
          adds what this account does not have yet and skips the rest, so it is safe to import the
          same file twice. Imported activities get your default visibility, and your profile,
          privacy settings and password are never changed.
        </p>
        <input
          #file
          class="cdk-visually-hidden"
          type="file"
          accept=".zip"
          aria-label="Export file to import"
          i18n-aria-label
          tabindex="-1"
          (change)="import(file)"
        />
        <div class="actions">
          <a mat-stroked-button href="/api/v1/users/me/export" download>
            <mat-icon aria-hidden="true">download</mat-icon><span i18n>Export my data</span>
          </a>
          <button mat-stroked-button type="button" [disabled]="importing()" (click)="file.click()">
            <mat-icon aria-hidden="true">upload</mat-icon><span i18n>Import an export</span>
          </button>
        </div>
        @if (importing()) {
          <mat-progress-bar mode="indeterminate" aria-label="Importing" i18n-aria-label />
        }
      </mat-card-content>
    </mat-card>
  `,
  styles: `
    .actions {
      display: flex;
      flex-wrap: wrap;
      gap: 8px;
    }
  `,
})
export class DataTransfer {
  private http = inject(HttpClient);
  private snack = inject(MatSnackBar);
  protected readonly importing = signal(false);

  protected async import(input: HTMLInputElement): Promise<void> {
    const file = input.files?.item(0);
    if (!file) return;
    const body = new FormData();
    body.append('file', file);
    this.importing.set(true);
    try {
      const r = await firstValueFrom(
        this.http.post<ImportDataResult>('/api/v1/users/me/import', body),
      );
      this.snack.open(
        $localize`Imported ${r.activities_imported} activities (${r.activities_skipped} already here), ${r.gears} gear, ${r.goals} goals and ${r.health_entries} health records.`,
        $localize`Dismiss`,
        { duration: 10000 },
      );
    } catch (err) {
      this.snack.open(problemMessage(err), $localize`Dismiss`, { duration: 10000 });
    } finally {
      input.value = '';
      this.importing.set(false);
    }
  }
}
