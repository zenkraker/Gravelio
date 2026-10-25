// Shapes of the Gravelio API (snake_case, as sent over the wire).

export interface User {
  id: number;
  name: string;
  username: string;
  email: string;
  access_type: 'regular' | 'admin';
  active: boolean;
  city: string | null;
  birthdate: string | null;
  preferred_language: string;
  gender: 'male' | 'female' | 'unspecified';
  units: 'metric' | 'imperial';
  height: number | null;
  max_heart_rate: number | null;
  first_day_of_week: string;
  timezone: string;
  currency: 'euro' | 'dollar' | 'pound';
}

export interface Activity {
  id: number;
  name: string | null;
  description: string | null;
  distance: number;
  activity_type: number;
  start_time: string;
  end_time: string;
  timezone: string | null;
  total_elapsed_time: number;
  total_timer_time: number;
  elevation_gain: number | null;
  elevation_loss: number | null;
  pace: number | null;
  average_speed: number | null;
  max_speed: number | null;
  average_power: number | null;
  max_power: number | null;
  normalized_power: number | null;
  average_hr: number | null;
  max_hr: number | null;
  average_cad: number | null;
  max_cad: number | null;
  calories: number | null;
  visibility: number;
  tracker_manufacturer: string | null;
  tracker_model: string | null;
  gear_id: number | null;
}

export interface Paged<T> {
  items: T[];
  total: number;
  page: number;
  page_size: number;
}

export interface Gear {
  id: number;
  nickname: string;
  brand: string | null;
  model: string | null;
  gear_type: number;
  active: boolean;
  initial_distance: number;
  purchase_value: number | null;
  created_at: string;
  total_distance: number;
  activity_count: number;
}

export interface GearComponent {
  id: number;
  gear_id: number;
  type: string;
  brand: string | null;
  model: string | null;
  purchase_date: string;
  retired_date: string | null;
  active: boolean;
  expected_distance: number | null;
  purchase_value: number | null;
  used_distance: number;
}

export interface DefaultGearSlot {
  activity_type: number;
  gear_type: number;
  gear_id: number | null;
}

export type Waypoint = { time: string } & Record<string, number | string>;

export interface ActivityStream {
  stream_type: number;
  waypoints: Waypoint[];
}

export const StreamType = {
  Hr: 1,
  Power: 2,
  Cadence: 3,
  Elevation: 4,
  Speed: 5,
  Pace: 6,
  Map: 7,
  Temperature: 8,
} as const;

export interface Problem {
  title?: string;
  detail?: string;
  errors?: { name: string; reason: string }[];
}

/** Best human-readable message from an RFC 7807 problem response. */
export function problemMessage(err: unknown): string {
  const problem = (err as { error?: Problem })?.error;
  const reasons = problem?.errors?.map((e) => e.reason).filter(Boolean);
  return reasons?.length
    ? reasons.join(' ')
    : (problem?.detail ?? problem?.title ?? $localize`Something went wrong`);
}
