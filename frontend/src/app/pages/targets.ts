import { HttpClient, httpResource } from '@angular/common/http';
import { Component, computed, effect, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSnackBar } from '@angular/material/snack-bar';
import { HealthTargets, problemMessage } from '../core/api';
import { AuthService } from '../core/auth';
import { displayToKg, kgToDisplay, weightUnitLabel } from '../core/format';
import { OUTLINE_FIELDS } from '../core/outline-fields';

type TargetKey = 'weight' | 'steps' | 'sleepHours' | 'water' | 'fastingHours' | 'poop';
const empty = { weight: '', steps: '', sleepHours: '', water: '', fastingHours: '', poop: '' };

@Component({
  selector: 'app-targets',
  providers: [OUTLINE_FIELDS],
  imports: [MatButtonModule, MatCardModule, MatFormFieldModule, MatInputModule],
  template: `
    <h1 i18n>Health targets</h1>

    <mat-card appearance="outlined">
      <mat-card-content>
        <p i18n>Personal goals. They do not change your recorded data.</p>
        @if (targets.error()) {
          <p class="error" role="alert" i18n>Could not load your targets.</p>
        }
        <form novalidate (submit)="save($event)">
          <mat-form-field>
            <mat-label>{{ weightLabel() }}</mat-label>
            <input
              matInput
              type="number"
              #weight
              [value]="model().weight"
              (input)="set('weight', weight.value)"
            />
          </mat-form-field>
          <mat-form-field>
            <mat-label i18n>Steps target</mat-label>
            <input
              matInput
              type="number"
              #steps
              [value]="model().steps"
              (input)="set('steps', steps.value)"
            />
          </mat-form-field>
          <mat-form-field>
            <mat-label i18n>Sleep target (hours)</mat-label>
            <input
              matInput
              type="number"
              #sleepHours
              [value]="model().sleepHours"
              (input)="set('sleepHours', sleepHours.value)"
            />
          </mat-form-field>
          <mat-form-field>
            <mat-label i18n>Water target (ml)</mat-label>
            <input
              matInput
              type="number"
              #water
              [value]="model().water"
              (input)="set('water', water.value)"
            />
          </mat-form-field>
          <mat-form-field>
            <mat-label i18n>Fasting target (hours)</mat-label>
            <input
              matInput
              type="number"
              #fastingHours
              [value]="model().fastingHours"
              (input)="set('fastingHours', fastingHours.value)"
            />
          </mat-form-field>
          <mat-form-field>
            <mat-label i18n>Bowel movements target</mat-label>
            <input
              matInput
              type="number"
              #poop
              [value]="model().poop"
              (input)="set('poop', poop.value)"
            />
          </mat-form-field>
          @if (error()) {
            <p class="error" role="alert">{{ error() }}</p>
          }
          <button mat-flat-button type="submit" i18n>Save targets</button>
        </form>
      </mat-card-content>
    </mat-card>
  `,
  styles: `
    form {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(220px, 1fr));
      gap: 8px 16px;
      align-items: start;
      padding-top: 8px;
    }
    button,
    .error {
      grid-column: 1 / -1;
      justify-self: start;
    }
    .error {
      color: var(--mat-sys-error);
      margin: 0;
    }
  `,
})
export class Targets {
  private http = inject(HttpClient);
  private snack = inject(MatSnackBar);
  private auth = inject(AuthService);

  protected readonly error = signal('');
  private readonly units = computed(() => this.auth.user()?.units ?? 'metric');
  protected readonly weightLabel = computed(
    () => $localize`Target weight (${weightUnitLabel(this.units())})`,
  );

  protected readonly targets = httpResource<HealthTargets>(() => '/api/v1/health/targets');
  protected readonly model = signal({ ...empty });

  constructor() {
    effect(() => {
      const t = this.targets.value();
      if (t) {
        this.model.set({
          weight: t.weight == null ? '' : String(kgToDisplay(t.weight, this.units())),
          steps: t.steps == null ? '' : String(t.steps),
          sleepHours: t.sleep == null ? '' : String(t.sleep / 3600),
          water: t.water_ml == null ? '' : String(t.water_ml),
          fastingHours: t.fasting == null ? '' : String(t.fasting / 3600),
          poop: t.poop_count == null ? '' : String(t.poop_count),
        });
      }
    });
  }

  protected set(key: TargetKey, value: string): void {
    this.model.update((m) => ({ ...m, [key]: value }));
  }

  protected async save(event: Event): Promise<void> {
    event.preventDefault();
    this.error.set('');
    const v = this.model();
    const number = (value: string) => (value === '' ? null : Number(value));
    const hours = (value: string) => (value === '' ? null : Math.round(Number(value) * 3600));
    try {
      await firstValueFrom(
        this.http.put('/api/v1/health/targets', {
          weight: v.weight === '' ? null : displayToKg(Number(v.weight), this.units()),
          steps: number(v.steps),
          sleep: hours(v.sleepHours),
          water_ml: number(v.water),
          fasting: hours(v.fastingHours),
          poop_count: number(v.poop),
        }),
      );
      this.targets.reload();
      this.snack.open($localize`Targets saved`, undefined, { duration: 4000 });
    } catch (err) {
      this.error.set(problemMessage(err));
    }
  }
}
