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
  mfa_enabled: boolean;
  pending_approval: boolean;
  has_photo: boolean;
}

export interface MfaStatus {
  enabled: boolean;
  recovery_codes_left: number;
}

export interface MfaSetup {
  secret: string;
  uri: string;
}

export interface RecoveryCodes {
  recovery_codes: string[];
}

export interface Activity {
  id: number;
  name: string | null;
  description: string | null;
  distance: number;
  activity_type: number;
  start_time: string | null;
  end_time: string | null;
  timezone: string | null;
  city: string | null;
  town: string | null;
  country: string | null;
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
  /** Set by the owner or by a duplicate upload; only the owner ever sees such an activity. */
  is_hidden: boolean;
  tracker_manufacturer: string | null;
  tracker_model: string | null;
  gear_id: number | null;
  owner: Person;
}

export interface Person {
  id: number;
  name: string;
  username: string;
  city: string | null;
  has_photo: boolean;
}

export interface Follow {
  follower_id: number;
  followee_id: number;
  status: 'pending' | 'accepted';
  follower: Person;
  followee: Person;
}

export interface PersonProfile {
  person: Person;
  followers: number;
  following: number;
  outgoing: Follow | null;
  incoming: Follow | null;
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

/** ASP.NET Core's HttpValidationProblemDetails: field name -> messages. */
export interface Problem {
  title?: string;
  detail?: string;
  errors?: Record<string, string[]>;
}

/** Best human-readable message from an RFC 7807 problem response. */
export function problemMessage(err: unknown): string {
  const problem = (err as { error?: Problem })?.error;
  const reasons = Object.values(problem?.errors ?? {})
    .flat()
    .filter(Boolean);
  return reasons.length
    ? reasons.join(' ')
    : (problem?.detail ?? problem?.title ?? $localize`Something went wrong`);
}

export interface WeightEntry {
  id: number;
  date: string;
  weight: number;
  body_fat: number | null;
  source: string | null;
}

export interface StepEntry {
  id: number;
  date: string;
  steps: number;
  source: string | null;
}

export interface SleepEntry {
  id: number;
  date: string;
  sleep_start_time_local: string | null;
  sleep_end_time_local: string | null;
  total_sleep_seconds: number | null;
  deep_sleep_seconds: number | null;
  light_sleep_seconds: number | null;
  rem_sleep_seconds: number | null;
  awake_sleep_seconds: number | null;
  awake_count: number | null;
  avg_sleep_stress: number | null;
  sleep_score_overall: number | null;
  sleep_score_duration: string | null;
  sleep_score_quality: string | null;
  source: string | null;
}

export interface WaterEntry {
  id: number;
  date: string;
  amount_ml: number;
  source: string | null;
}

export interface HealthTargets {
  id: number;
  weight: number | null;
  steps: number | null;
  sleep: number | null;
  fasting: number | null;
  water_ml: number | null;
  poop_count: number | null;
}

export interface FastingEntry {
  id: number;
  fast_start_time: string;
  fast_end_time: string | null;
  target_duration_seconds: number | null;
  actual_duration_seconds: number | null;
  fasting_type: string | null;
  status: 'in_progress' | 'completed' | 'broken' | 'cancelled';
  notes: string | null;
  source: string | null;
}

export interface FastingStats {
  total_fasts: number;
  current_streak: number;
  longest_streak: number;
  avg_duration_seconds: number | null;
  total_fasting_seconds: number;
  completion_rate: number;
}

export interface PoopEntry {
  id: number;
  date_time: string;
  bristol_type: number | null;
  color: string | null;
  notes: string | null;
  source: string | null;
}

export interface ServerSettings {
  signup_enabled: boolean;
  signup_require_email_verification: boolean;
  signup_require_admin_approval: boolean;
  public_shareable_links: boolean;
  public_shareable_links_user_info: boolean;
  sso_auto_redirect: boolean;
  local_login_enabled: boolean;
}

export interface ServerSettingsRead extends ServerSettings {
  email_configured: boolean;
}

export interface PublicSettings {
  signup_enabled: boolean;
  public_shareable_links: boolean;
  local_login_enabled: boolean;
  sso_auto_redirect: boolean;
  sso_providers: { slug: string; name: string }[];
}

/** An identity provider as an admin sees it; the client secret is never sent back, only whether one is stored. */
export interface SsoProvider {
  id: number;
  name: string;
  slug: string;
  issuer: string;
  client_id: string;
  has_secret: boolean;
  scopes: string;
  enabled: boolean;
  auto_create_users: boolean;
}

export interface MySso {
  slug: string;
  name: string;
  linked: boolean;
}

/** What a share link shows an anonymous visitor: no account, gear, device or visibility. */
export interface PublicActivity {
  id: number;
  name: string | null;
  description: string | null;
  activity_type: number;
  distance: number;
  start_time: string | null;
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
  owner_name: string | null;
}

export interface SignupResult {
  pending_email_verification: boolean;
  pending_approval: boolean;
}

export interface Notification {
  id: number;
  type: number;
  actor_id: number;
  actor_name: string;
  read: boolean;
  created_at: string;
  activity_id: number | null;
  activity_name: string | null;
}

export interface UnreadCount {
  count: number;
}

export interface StatsBucket {
  start: string;
  end: string;
  distance: number;
  duration: number;
  elevation_gain: number;
  calories: number;
  count: number;
}

export interface StatsType {
  activity_type: number;
  name: string;
  distance: number;
  duration: number;
  elevation_gain: number;
  calories: number;
  count: number;
}

export interface ActivityStats {
  total_distance: number;
  total_duration: number;
  total_elevation_gain: number;
  total_calories: number;
  activity_count: number;
  breakdown: StatsBucket[];
  type_breakdown: StatsType[];
}

export interface PrivacySettings {
  default_activity_visibility: number;
  hide_activity_start_time: boolean;
  hide_activity_location: boolean;
  hide_activity_map: boolean;
  hide_activity_hr: boolean;
  hide_activity_power: boolean;
  hide_activity_cadence: boolean;
  hide_activity_elevation: boolean;
  hide_activity_speed: boolean;
  hide_activity_pace: boolean;
  hide_activity_gear: boolean;
  hide_activity_laps: boolean;
}

export interface Goal {
  id: number;
  interval: string;
  activity_group: string;
  metric: string;
  target: number;
}

export interface GoalProgress {
  goal_id: number;
  interval: string;
  activity_group: string;
  metric: string;
  target: number;
  total: number;
  percent: number;
  start: string;
  end: string;
}

/** The owner-only part of an activity. `hidden_details` null means it follows the owner's privacy settings. */
export interface ActivityPrivateDetails {
  private_notes: string | null;
  workout_feeling: number | null;
  workout_rpe: number | null;
  hidden_details: string[] | null;
  is_hidden: boolean;
}

/** A lap; whatever the owner hides from this viewer is null. */
export interface ActivityLap {
  index: number;
  start_time: string | null;
  start_offset: number;
  start_lat: number | null;
  start_lon: number | null;
  end_lat: number | null;
  end_lon: number | null;
  total_elapsed_time: number;
  total_timer_time: number | null;
  distance: number | null;
  average_hr: number | null;
  max_hr: number | null;
  average_cad: number | null;
  max_cad: number | null;
  average_power: number | null;
  max_power: number | null;
  elevation_gain: number | null;
  elevation_loss: number | null;
  average_speed: number | null;
  max_speed: number | null;
  calories: number | null;
}

export interface HrZone {
  zone: number;
  percent: number;
  seconds: number;
  /** The zone's heart-rate limits; only the owner gets them. */
  range: string | null;
}

export interface DailyStats {
  date: string;
  sleep: { total_sleep_seconds: number | null; resting_heart_rate: number | null } | null;
  weight: { weight: number; bmi: number | null } | null;
  steps: number | null;
  fasting: { status: string; fast_start_time: string } | null;
  water_ml: number | null;
  poop_count: number;
}

export interface ImportJob {
  id: number;
  status: 'queued' | 'running' | 'completed' | 'failed' | 'cancelled';
  file_name: string;
  total_files: number;
  processed_files: number;
  imported: number;
  skipped: number;
  hidden_duplicates: number;
  failed: number;
  failures: { file: string; error: string }[];
  error: string | null;
  created_at: string;
  started_at: string | null;
  finished_at: string | null;
}

export interface Session {
  id: number;
  device: string;
  ip_address: string | null;
  created_at: string;
  last_seen_at: string;
  current: boolean;
}

export interface ApiKey {
  id: number;
  name: string;
  prefix: string;
  created_at: string;
  expires_at: string | null;
  last_used_at: string | null;
  revoked_at: string | null;
}

/** The response that makes a key; `key` is shown once and never returned again. */
export interface CreatedApiKey {
  id: number;
  name: string;
  key: string;
  prefix: string;
  created_at: string;
  expires_at: string | null;
}

export interface ImportDataResult {
  gears: number;
  goals: number;
  health_entries: number;
  activities_imported: number;
  activities_skipped: number;
}
