import {
  elevationValue,
  distanceUnitLabel,
  displayToMeters,
  formatDuration,
  metersToDisplay,
} from './format';

type Units = 'metric' | 'imperial';

export const GOAL_INTERVALS = [
  { id: 'daily', label: $localize`Daily` },
  { id: 'weekly', label: $localize`Weekly` },
  { id: 'monthly', label: $localize`Monthly` },
  { id: 'yearly', label: $localize`Yearly` },
] as const;

export const GOAL_GROUPS = [
  { id: 'run', label: $localize`Run` },
  { id: 'bike', label: $localize`Bike` },
  { id: 'swim', label: $localize`Swim` },
  { id: 'walk', label: $localize`Walk and hike` },
  { id: 'strength', label: $localize`Strength` },
  { id: 'cardio', label: $localize`Cardio` },
] as const;

export const GOAL_METRICS = [
  { id: 'distance', label: $localize`Distance` },
  { id: 'duration', label: $localize`Moving time` },
  { id: 'elevation', label: $localize`Elevation gain` },
  { id: 'calories', label: $localize`Calories` },
  { id: 'activities', label: $localize`Activities` },
] as const;

function labelOf(list: readonly { id: string; label: string }[], id: string): string {
  return list.find((x) => x.id === id)?.label ?? id;
}

export const intervalLabel = (id: string) => labelOf(GOAL_INTERVALS, id);
export const groupLabel = (id: string) => labelOf(GOAL_GROUPS, id);
export const metricLabel = (id: string) => labelOf(GOAL_METRICS, id);

/** The unit a target is typed in. The API stores meters, seconds, kcal and counts. */
export function targetUnit(metric: string, units: Units): string {
  switch (metric) {
    case 'distance':
      return distanceUnitLabel(units);
    case 'elevation':
      return units === 'imperial' ? $localize`ft` : $localize`m`;
    case 'duration':
      return $localize`minutes`;
    case 'calories':
      return $localize`kcal`;
    default:
      return '';
  }
}

/** Stored value -> what the form shows. */
export function targetToDisplay(metric: string, stored: number, units: Units): number {
  switch (metric) {
    case 'distance':
      return metersToDisplay(stored, units);
    case 'elevation':
      return Math.round(elevationValue(stored, units));
    case 'duration':
      return Math.round(stored / 60);
    default:
      return stored;
  }
}

/** What the form holds -> stored value, at least 1 so it passes the API's positive-target rule. */
export function displayToTarget(metric: string, shown: number, units: Units): number {
  let stored: number;
  switch (metric) {
    case 'distance':
      stored = displayToMeters(shown, units);
      break;
    case 'elevation':
      stored = shown / elevationValue(1, units);
      break;
    case 'duration':
      stored = shown * 60;
      break;
    default:
      stored = shown;
  }
  return Math.max(1, Math.round(stored));
}

/** A total or target for a table cell, in the user's units. */
export function formatGoalAmount(metric: string, stored: number, units: Units): string {
  switch (metric) {
    case 'distance':
      return `${metersToDisplay(stored, units)} ${distanceUnitLabel(units)}`;
    case 'elevation':
      return `${Math.round(elevationValue(stored, units))} ${targetUnit('elevation', units)}`;
    case 'duration':
      return formatDuration(stored);
    case 'calories':
      return `${Math.round(stored)} kcal`;
    default:
      return String(Math.round(stored));
  }
}
