import { User } from './api';

type Units = User['units'];

const KM_PER_MILE = 1.609344;
const FT_PER_M = 3.28084;

export function formatDistance(meters: number, units: Units): string {
  return units === 'imperial'
    ? `${(meters / 1000 / KM_PER_MILE).toFixed(2)} mi`
    : `${(meters / 1000).toFixed(2)} km`;
}

export function formatElevation(meters: number | null, units: Units): string {
  if (meters == null) return '—';
  return units === 'imperial' ? `${Math.round(meters * FT_PER_M)} ft` : `${meters} m`;
}

export function formatDuration(seconds: number): string {
  const s = Math.round(seconds);
  const h = Math.floor(s / 3600);
  const m = Math.floor((s % 3600) / 60);
  const sec = s % 60;
  const mmss = `${String(m).padStart(h ? 2 : 1, '0')}:${String(sec).padStart(2, '0')}`;
  return h ? `${h}:${mmss}` : mmss;
}

/** Pace is stored in seconds per meter. */
export function formatPace(secPerMeter: number | null, units: Units): string {
  if (!secPerMeter) return '—';
  const perUnit = secPerMeter * (units === 'imperial' ? KM_PER_MILE * 1000 : 1000);
  return `${formatDuration(perUnit)} /${units === 'imperial' ? 'mi' : 'km'}`;
}

/** Speed is stored in meters per second. */
export function formatSpeed(mps: number | null, units: Units): string {
  if (!mps) return '—';
  const kmh = mps * 3.6;
  return units === 'imperial' ? `${(kmh / KM_PER_MILE).toFixed(1)} mph` : `${kmh.toFixed(1)} km/h`;
}

export function speedValue(mps: number, units: Units): number {
  return units === 'imperial' ? (mps * 3.6) / KM_PER_MILE : mps * 3.6;
}

/** Running-like activities read better as pace than speed. */
export const PACE_TYPES = new Set([1, 2, 3, 11, 12, 31, 34, 40, 44]);

/** Shows an instant in the timezone the activity happened in. */
export function formatDateTime(iso: string, timeZone: string | null): string {
  return new Intl.DateTimeFormat(undefined, {
    dateStyle: 'medium',
    timeStyle: 'short',
    timeZone: timeZone ?? 'UTC',
  }).format(Date.parse(iso));
}

export function elevationValue(meters: number, units: Units): number {
  return units === 'imperial' ? meters * FT_PER_M : meters;
}

/** Seconds per kilometre (or mile) from the stored seconds per metre. */
export function paceSeconds(secPerMeter: number, units: Units): number {
  return secPerMeter * (units === 'imperial' ? KM_PER_MILE * 1000 : 1000);
}
