import { HttpClient, httpResource } from '@angular/common/http';
import { Component, inject, input, linkedSignal, output, signal } from '@angular/core';
import { FormField, form, required, submit } from '@angular/forms/signals';
import { firstValueFrom } from 'rxjs';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Activity, ActivityPrivateDetails, problemMessage } from '../core/api';
import { ActivityTypes } from '../core/activity-types';
import { OUTLINE_FIELDS } from '../core/outline-fields';

/** What can be hidden from other viewers, by the names the API uses. */
const HIDE_OPTIONS = [
  { id: 'start_time', label: $localize`Start time` },
  { id: 'location', label: $localize`Location` },
  { id: 'map', label: $localize`Map` },
  { id: 'hr', label: $localize`Heart rate` },
  { id: 'power', label: $localize`Power` },
  { id: 'cadence', label: $localize`Cadence` },
  { id: 'elevation', label: $localize`Elevation` },
  { id: 'speed', label: $localize`Speed` },
  { id: 'pace', label: $localize`Pace` },
  { id: 'gear', label: $localize`Gear` },
];

/** The owner's edit form for one activity: its name, type and notes, how it felt, and what to hide from others. */
@Component({
  selector: 'app-activity-edit',
  providers: [OUTLINE_FIELDS],
  imports: [
    FormField,
    MatButtonModule,
    MatCheckboxModule,
    MatExpansionModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
  ],
  template: `
    <mat-expansion-panel>
      <mat-expansion-panel-header>
        <mat-panel-title i18n>Edit details</mat-panel-title>
      </mat-expansion-panel-header>
      <form novalidate (submit)="save($event)">
        <mat-form-field class="wide">
          <mat-label i18n>Name</mat-label>
          <input matInput [formField]="f.name" />
        </mat-form-field>
        <mat-form-field>
          <mat-label i18n>Activity type</mat-label>
          <mat-select [formField]="f.type">
            @for (t of types.options(); track t.id) {
              <mat-option [value]="'' + t.id">{{ t.name }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
        <mat-form-field class="wide">
          <mat-label i18n>Description</mat-label>
          <textarea matInput rows="2" [formField]="f.description"></textarea>
        </mat-form-field>
        <mat-form-field class="wide">
          <mat-label i18n>Private notes (only you can read these)</mat-label>
          <textarea matInput rows="3" [formField]="f.notes"></textarea>
        </mat-form-field>
        <mat-form-field>
          <mat-label i18n>How it felt (0–100)</mat-label>
          <input matInput type="number" [formField]="f.feeling" />
        </mat-form-field>
        <mat-form-field>
          <mat-label i18n>Perceived exertion (10–100)</mat-label>
          <input matInput type="number" [formField]="f.rpe" />
        </mat-form-field>

        <mat-checkbox
          class="wide"
          [checked]="isHidden()"
          (change)="isHidden.set($event.checked)"
          i18n
        >
          Hide this activity from everyone. It is also left out of statistics, goals and gear
          totals.
        </mat-checkbox>

        <fieldset class="wide">
          <legend i18n>Hide from other people</legend>
          <mat-checkbox [checked]="useDefaults()" (change)="useDefaults.set($event.checked)" i18n>
            Use my privacy settings
          </mat-checkbox>
          <div class="hide-options">
            @for (o of hideOptions; track o.id) {
              <mat-checkbox
                [checked]="hidden().includes(o.id)"
                [disabled]="useDefaults()"
                (change)="toggle(o.id, $event.checked)"
              >
                {{ o.label }}
              </mat-checkbox>
            }
          </div>
        </fieldset>

        @if (error()) {
          <p class="error wide" role="alert">{{ error() }}</p>
        }
        <div class="wide">
          <button mat-flat-button type="submit" [disabled]="f().submitting()" i18n>Save</button>
        </div>
      </form>
    </mat-expansion-panel>
  `,
  styles: `
    form {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(260px, 1fr));
      gap: 8px 16px;
      padding-top: 16px;
    }
    .wide {
      grid-column: 1 / -1;
    }
    fieldset {
      border: 1px solid var(--mat-sys-outline-variant);
      border-radius: var(--mat-sys-corner-medium);
    }
    legend {
      font: var(--mat-sys-label-large);
      padding: 0 8px;
    }
    .hide-options {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(160px, 1fr));
    }
    .error {
      color: var(--mat-sys-error);
      margin: 0;
    }
  `,
})
export class ActivityEdit {
  readonly activity = input.required<Activity>();
  /** The version the activity was loaded at; a save of a stale one is refused instead of overwriting a newer edit. */
  readonly etag = input<string | null>(null);
  readonly saved = output<void>();

  private http = inject(HttpClient);
  private snack = inject(MatSnackBar);
  protected readonly types = inject(ActivityTypes);
  protected readonly hideOptions = HIDE_OPTIONS;

  private readonly details = httpResource<ActivityPrivateDetails>(
    () => `/api/v1/activities/${this.activity().id}/private`,
  );

  // Rebuilt whenever the activity or its private details are (re)loaded, so the form always starts from what is stored.
  private readonly model = linkedSignal(() => {
    const a = this.activity();
    const d = this.details.value();
    return {
      name: a.name ?? '',
      type: String(a.activity_type),
      description: a.description ?? '',
      notes: d?.private_notes ?? '',
      feeling: d?.workout_feeling == null ? '' : String(d.workout_feeling),
      rpe: d?.workout_rpe == null ? '' : String(d.workout_rpe),
    };
  });
  protected readonly f = form(this.model, (p) => {
    required(p.name, { message: $localize`Enter a name` });
  });
  // No list stored means the activity follows the owner's privacy settings.
  protected readonly useDefaults = linkedSignal(() => this.details.value()?.hidden_details == null);
  protected readonly isHidden = linkedSignal(() => this.details.value()?.is_hidden ?? false);
  protected readonly hidden = linkedSignal<string[]>(
    () => this.details.value()?.hidden_details ?? [],
  );
  protected readonly error = signal('');

  protected toggle(id: string, on: boolean): void {
    this.hidden.update((all) => (on ? [...all, id] : all.filter((x) => x !== id)));
  }

  private ifMatch(): { headers: Record<string, string> } | Record<string, never> {
    const etag = this.etag();
    return etag ? { headers: { 'If-Match': etag } } : {};
  }

  protected async save(event: Event): Promise<void> {
    event.preventDefault();
    this.error.set('');
    await submit(this.f, async () => {
      try {
        const a = this.activity();
        const m = this.model();
        await firstValueFrom(
          this.http.put(
            `/api/v1/activities/${a.id}`,
            {
              name: m.name,
              description: m.description || null,
              activity_type: Number(m.type),
              visibility: a.visibility,
            },
            this.ifMatch(),
          ),
        );
        await firstValueFrom(
          this.http.put(`/api/v1/activities/${a.id}/private`, {
            private_notes: m.notes || null,
            workout_feeling: m.feeling === '' ? null : Number(m.feeling),
            workout_rpe: m.rpe === '' ? null : Number(m.rpe),
            hidden_details: this.useDefaults() ? null : this.hidden(),
            is_hidden: this.isHidden(),
          }),
        );
        this.details.reload();
        this.saved.emit();
        this.snack.open($localize`Activity saved`, undefined, { duration: 4000 });
      } catch (err) {
        this.error.set(problemMessage(err));
      }
    });
  }
}
