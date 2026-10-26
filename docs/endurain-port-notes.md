<!-- Written for an AI agent working in this repo. Terse on purpose. Stable IDs (R-xx, F-xx, G-xx) so other notes can cite them. -->

# Endurain → Gravelio: design review and feature gap list

Reviewed: `/home/user/endurain-project/endurain` (backend `backend/app`, Python/FastAPI, 184 routes, ~428 module files) against `/home/user/gravelio` at commit `785c5be` (merge of the pulled work).
Not a spec. Gravelio's behavioural reference is Endurain, but schema, API and architecture are ours (see `CLAUDE.md`).

## How to use this file

1. Pick a module from **Part B** with status `MISSING` or `PARTIAL`. Port it as one vertical slice (`backend/Gravelio.Api/<Feature>/<Operation>/…Endpoint.cs`).
2. Before writing it, read every `R-xx` finding listed in that module's row. They say what **not** to copy from Endurain and which official .NET API to use instead.
3. Apply the audit rules in `CLAUDE.md` ("Rules from the Endurain audit"). Each needs a test.
4. When a module is done, change its status in Part B and add the commit id. Keep this file the single list of what is left.

Status words: `DONE` = equal or better behaviour exists and is tested; `PARTIAL` = some of it; `MISSING` = nothing; `SKIP` = deliberately not ported (reason given).
Priority: `P1` blocks real daily use or is a security/privacy gap, `P2` expected by Endurain users, `P3` nice to have.
Endurain paths below are relative to `backend/app/`.

---

## Part A — What Endurain does badly or heavily (do better when porting)

Each finding: **Where** (Endurain evidence) · **Problem** · **Gravelio decision** · **Status in Gravelio**.

### A1. Security and privacy

**R-01 Hide flags are copied onto every activity at import (snapshot semantics).**
Where: `modules/activities/activity/models.py` has 12 `hide_*` booleans + `is_hidden` + `visibility` per activity; `modules/users/users_privacy_settings/models.py` holds the same 12 as user defaults.
Problem: changing the default does not change existing activities; flag lists are repeated in the model, privacy settings, serializers and schemas, so a new flag must be added in ~5 places and a forgotten one leaks data. Laps/sets/steps/streams each have their own gate.
Decision: apply the owner's settings live at read time (done in `Privacy/PrivacyMask.cs`, default-deny for unmapped stream types). If per-activity override is added (F-A14), store it as one nullable `[Flags] enum HiddenDetails` column on `Activity` (null = inherit the user default), not 12 booleans. One gate for all child resources, modelled on Endurain's `activity/child_access_service.py` (parent visible AND guarding flag unset for non-owners).
Status: live masking DONE; per-activity override MISSING; laps/sets/steps gates N/A until those exist.

**R-02 Rate limits exist in tiers; Gravelio only limits login.**
Where: `core/rate_limit.py`: DEFAULT 120/min on every route, WRITE 30, SENSITIVE 10 (login, MFA, reset, signup, OAuth), UPLOAD 20, PROVIDER_SYNC 6. Key = hashed session token, else client IP.
Decision: `Microsoft.AspNetCore.RateLimiting` with a global partitioned limiter (user id when signed in, else client IP from forwarded headers) plus named policies `sensitive`, `write`, `upload`, `provider`. Test one 429 per policy.
Status: DONE (`Common/RateLimiting.cs`; `RateLimitTests`). The `provider` tier waits for Strava/Garmin sync.

**R-03 Step-up re-authentication with its own lockout.**
Where: `docs/integrate/account-security.md`: password change, MFA enable/disable, backup-code regeneration, API-key creation, IdP link/unlink all require `current_password` and, when MFA is on, `mfa_code`. Separate progressive lockout: 5 failures → 5 min, 10 → 30 min, 15 → 2 h, checked **before** comparing credentials.
Decision: one reusable check (password via `UserManager.CheckPasswordAsync`, MFA via `VerifyTwoFactorTokenAsync`) with its own failure counter table. Do not reuse `AccessFailedAsync`: that would lock the user out of login too.
Status: MISSING (only change-password checks the current password).

**R-04 One-time MFA code (carried over from the audit).** Done with a `UsedTotpCodes` table (unique user + code) rather than a stored time step, because Identity does not expose the matched step. See `CLAUDE.md`. Status: DONE.

**R-05 SSRF through admin-supplied URLs.**
Where: server fetches `identity_providers.issuer_url` / discovery endpoints, `server_settings.tileserver_url` (thumbnail rendering), geocoding hosts (`core/config.py` `NOMINATIM_API_HOST`, `PHOTON_API_HOST`). Egress hardening lives in their `jasil` platform; the routers trust the admin.
Decision: every outbound call goes through `IHttpClientFactory` (already a rule) with a named client whose `SocketsHttpHandler.ConnectCallback` refuses loopback, link-local and private ranges after DNS resolution (guards DNS rebinding), https only unless an explicit `AllowPrivateNetworks` option is set. One helper, used by IdP discovery, tile fetch, geocoding, Strava.
Status: DONE (`SafeHttp.AddGuardedHttpClient`, `SafeHttpTests`); every future outbound client must use it.

**R-06 Upload hardening is mostly numeric limits.**
Where: `core/file_uploads.py`: activity file 200 MiB, gzip 200 MiB, zip 500 MiB, 2 GiB uncompressed, 2000 files of one type; magic-number/MIME checks; bulk zip max 20 000 files.
Problem to avoid: trusting declared sizes in a zip header; one global body limit for all routes.
Decision: count real decompressed bytes while streaming each `ZipArchive` entry and abort at the limit; check entry count first; reject path traversal in entry names (never use the name as a path); check magic bytes (`<?xml`, FIT header `.FIT` at offset 8) in addition to the extension; `.gz` via `GZipStream` with the same byte cap.
Gravelio today: 50 MB, extension check, DTD prohibited. See also G-02.

**R-07 Secrets at rest: Fernet.** Where: `core/cryptography.py` encrypts Strava tokens/client secret, MFA secret, IdP client secret, rotated-token replacement. Decision: ASP.NET Core Data Protection (`IDataProtector` with one purpose string per kind of secret); the key ring is already on the `api-keys` volume. Never log decrypted values. Status: infrastructure DONE, no secrets stored yet.

**R-08 Signed capability URLs for images.** Where: `core/signing.py` (itsdangerous, 24 h) for thumbnails and media so `<img src>` works. Decision: skip. The SPA is same origin and the cookie is `SameSite=Strict`, so `<img>` requests carry the cookie; serve media through an authorized endpoint. Only public share links (F-A18) need anonymous access, and those check the activity's visibility on every request instead of embedding a token. Status: SKIP.

**R-09 API keys.** Where: `modules/auth/api_keys`: key `endurain_<43 base64url>` (256-bit random), stored as SHA-256 hash + short prefix, shown once, scope allow-list currently only `activities:upload`, `X-API-Key` header, `?api_key=` query delivery off by default (`ALLOW_API_KEY_QUERY_PARAM`), revoke/delete.
Decision: copy the model, drop the query-parameter option entirely (keys leak into logs). Custom `AuthenticationHandler<TOptions>` (documented in "Authentication handlers" on learn.microsoft.com); look up by hash (no timing leak on a high-entropy key); the handler produces a principal with a `scope` claim; upload endpoint accepts cookie **or** key via `AuthenticationSchemes`. XSRF middleware must skip requests authenticated by key (no cookie, no CSRF). Update `last_used_at` at most once per hour to avoid a write per request. Name prefix: `gravelio_`.
Status: MISSING.

**R-10 Sessions are rows with refresh-token families.**
Where: `modules/auth/sessions`: refresh-token rotation, reuse detection by family, device/OS/browser/IP per session, list and revoke own or (admin) any user's sessions, CSRF token hash per session.
Problem: re-implements what cookie auth + Identity already give (see `CLAUDE.md` auth rules); ~2 000 lines.
Decision: keep cookie auth. For the "list and revoke my devices" feature only, set `CookieAuthenticationOptions.SessionStore` to an `ITicketStore` backed by a `UserSession` table (id, user, user-agent, IP, created, last seen); revoking deletes the row; the cookie then carries only the session key. Parse the user agent with a small BCL-only helper, not a package. Keep `SecurityStampValidator` revocation as is.
Status: DONE (`Sessions/`; `SessionTests`). The refresh-token family part is not needed with cookie sessions.

**R-11 Identity providers (SSO).**
Where: `modules/auth/identity_providers` (5 500 lines): admin-configurable providers (name, slug, client id/secret, issuer, endpoints, scopes, icon, `auto_create_users`, `sync_user_info`, `user_mapping` JSON), PKCE, `oauth_states` table, link tokens for linking an existing account, mobile redirect schemes allow-list (`ALLOWED_REDIRECT_SCHEMES`), `sso_auto_redirect`, `local_login_enabled`.
Decision: `Microsoft.AspNetCore.Authentication.OpenIdConnect` (handles PKCE, state, nonce, discovery). Providers live in the DB, so register schemes at runtime with `IAuthenticationSchemeProvider.AddScheme` and an `IOptionsMonitorCache<OpenIdConnectOptions>` entry (the documented pattern for dynamic schemes). Do not hand-write the OAuth state table. Gate: external login only signs in a user whose IdP subject is already linked, or creates one when `auto_create_users` is on; never link by email alone (account takeover). Use `UserManager.AddLoginAsync` for links.
Status: DONE (`SsoTests` runs the real handler against a fake provider). Not built: mobile redirect schemes, user-info sync and claim mapping.

**R-12 Strava/Garmin credentials.** Where: `users_integrations` stores Strava client id/secret per user (bring-your-own app), tokens, and `garminconnect_token` JSON. Decision: same shape but one row per (user, provider) with the whole blob protected (R-07). On Strava unlink delete every Strava-derived activity and gear (Strava API agreement; Endurain does this). Status: MISSING.

**R-13 Garmin depends on an unofficial library.** Where: `modules/garmin` uses python-garminconnect (reverse-engineered web login incl. MFA, a 65 s blocking login thread, MFA code handed across workers through a state store with 90 s TTL). No .NET equivalent; the login can break whenever Garmin changes its site.
Decision: do not port as is. Give Garmin users file ingestion instead (API-key upload from Gadgetbridge or any sync tool, F-A09). Revisit only with the official Garmin Health/Activity API (needs a partner agreement). Status: SKIP for now. P3.

### A2. Entities and data model

**R-14 Provider ids as columns everywhere.** Where: `activities.strava_activity_id`, `strava_gear_id`, `garminconnect_activity_id`, `garminconnect_gear_id`, `activities_streams.strava_activity_stream_id`, `gear.strava_gear_id`, `health_sleep.garminconnect_sleep_id`, plus a `users_integrations` row with provider-specific columns. Each new provider means a migration on five tables.
Decision: `ExternalId(Id, UserId, Provider, Kind, ExternalKey, EntityId)` with a unique index on `(UserId, Provider, Kind, ExternalKey)` — that index is the duplicate guard (use `TrySaveUniqueAsync`). One `UserIntegration(UserId, Provider, ProtectedState, SyncGear, …)` row per provider.
Gravelio today: `HealthSleep.GarminconnectSleepId` was copied across (`Data/AppDb.cs`). Replace it with `ExternalId` when the first provider is ported.

**R-15 Free-text enums.** Where: `source`, `fasting_type`, `status`, `gender`, `units`, `currency`, goal `interval`/`activity_type`/`goal_type`, `access_type` are plain strings; only followers have DB `CheckConstraint`s.
Decision: new enums as C# enums stored as strings (`HasConversion<string>()`) with a check constraint, validated by FluentValidation. Existing string-constant classes (`FollowStatuses`, `FastingStatuses`) stay until touched.

**R-16 Derived values stored and goes stale.** Where: `health_weight.bmi` (wrong after a height change), `health_sleep.sleep_score_*` (wrong after a formula change), `activities.pace/average_speed`. HR zones are correctly computed at read from current max HR.
Decision: compute BMI at read from the day's weight and the user's height. Keep sleep scores stored but write a `ScoringVersion` so a backfill can find old rows. Keep activity metrics stored (they come from the file).
Gravelio today: `HealthWeight.Bmi` stored, sleep scores stored without a version.

**R-17 Units mixed in one table.** Where: `gear.initial_kms` and `gear_components.expected_kms` are kilometres while `activities.distance` is metres. Gravelio already stores meters everywhere (`Gear.InitialDistance`, `GearComponent.ExpectedDistance`). Status: DONE. Keep it that way for goals (`goal_distance` is already metres in Endurain).

**R-18 Goals table: five nullable columns, "exactly one set".** Where: `users_goals` has `goal_calories`, `goal_activities_number`, `goal_distance`, `goal_elevation`, `goal_duration` plus `goal_type`; a pydantic validator enforces one. `activity_type` is a string group (`run, bike, swim, walk, strength, cardio`) separate from the 30+ numeric activity type ids.
Decision: `Goal(Id, UserId, Interval, ActivityGroup, Metric, Target)`; the group→activity-type-id set is a static map in code next to `ActivityTypes`. Compute "today/this week" in the user's time zone with `TimeProvider` + `TimeZoneInfo` (Endurain fixed a UTC-midnight bug here: `users/users_goals/utils.py`, `user_local_today`). Test with a non-UTC user around local midnight. Status: MISSING. P2.

**R-19 Two overlapping "goal" concepts.** `health_targets` (weight, steps, sleep, fasting, water, poop) and `users_goals` (activity goals). Keep both (different data), but share the progress DTO shape so the dashboard renders both the same way.

**R-20 Server settings duplicate per-user settings.** `server_settings.units/currency/num_records_per_page` and `users.units/currency`. Two sources of truth. Decision: server value is only the default for **new** users; the per-user value always wins; page size is a request parameter capped at 100 (already `PagedRequest`). Status: Gravelio has neither global value.

**R-21 Laps table is 38 columns, including device-specific dynamics.** Where: `activity_laps` (vertical oscillation, stance time, fractional cadence, step length…). Decision: core columns (start, coordinates, elapsed, timer, distance, avg/max HR/cadence/power, ascent/descent, trigger) + one `jsonb Extras`. Laps start/end coordinates are masked together with the map (audit rule 2). `activity_sets` and `activity_workout_steps` likewise: only for strength/workout files; P3.

**R-22 Reference data in a table.** `activity_exercise_titles` (FIT exercise category/name → title) is static. Decision: static class generated from `Garmin.FIT.Sdk` `Profile` enums, no table.

**R-23 Job state on domain rows.** Where: `activities.location_next_lookup_at`, `thumbnail_next_attempt_at`, `version`, `import_info` JSON. Problem: retry scheduling stored on the business entity, forcing migrations when the job logic changes.
Decision: retries belong to the job (R-26); the activity only keeps `City/Town/Country` nullable. A backfill query "activities with a GPS stream and null location, not tried in the last day" replaces the next-attempt columns, with the last attempt tracked in a small `LocationLookup(ActivityId, LastAttemptAt)` table only if rate limits require it.

**R-24 Duplicate handling.** Where: `activity_ingestion` + `activities.dedup_key`: (1) same provider id → skip; (2) plain files → content hash + start time → skip; (3) different source, same start time → store **hidden** (`is_hidden`) and notify `DUPLICATE_ACTIVITY` so the user decides.
Gravelio: unique `(UserId, StartTime)` → 409 on any collision. Problem: a phone recording and a watch recording of one run are two valid files with the same start; the second is refused and the user loses it. Also two genuinely different files starting in the same second collide.
Decision: keep the unique index only for exact re-uploads (add `ContentHash` and make the index `(UserId, ContentHash)`), and store a start-time collision as hidden + notification. Needs `Activity.IsHidden` and the `DuplicateActivity` notification type. Status: PARTIAL. P2.

**R-25 Optimistic concurrency.** Where: `core/etag.py` + `activities.version`; `PATCH` requires `If-Match` so two tabs cannot silently overwrite each other.
Decision: Npgsql `xmin` as the EF concurrency token (`UseXminAsConcurrencyToken` / `IsRowVersion()` on `xmin`); emit it as `ETag`; on `If-Match` mismatch or `DbUpdateConcurrencyException` answer 412. No `version` column to maintain. Do it for activities and gears first. Status: DONE for activities and gears (`ETags`, `ConcurrencyTests`); `If-Match` is optional per request.

### A3. Architecture and logic

**R-26 Do not port the platform layer.** Where: `jasil` (their private platform library: event bus, outbox, durable jobs with lease/retry/dead-letter, versioned payloads, provider registries, storage provider, state provider) + per-module `router → service → crud → integration_service → contracts → serializers → model_registry → subscriber_registry`, import-linter contracts to police boundaries. Result: ~72 000 lines for what Gravelio does in ~6 000.
Decision (already the rule in `CLAUDE.md`): endpoints use `AppDb` directly; background work is a `BackgroundService` reading a `Channel<T>` (System.Threading.Channels); derived work is idempotent and every derived job has a scheduled backfill — that last idea is Endurain's best one: a lost event is repaired by the next backfill, so no outbox is needed. Add a durable `ImportJob` table only for bulk import (R-27).

**R-27 Bulk import as a durable job.** Where: `activity_ingestion/models.py` `ActivityIngestionJob` (status, files total/done/failed, `idempotency_key`, `request_fingerprint`, cursor, `activity_ids`), one running import per user, failed files kept for inspection, folder import from `data/activity_files/bulk_import/<user id>/`.
Decision: port the job row and progress; accept a ZIP upload only first (R-06 limits), folder import later and only from a configured root with the user id as the folder name, resolved with `Path.GetFullPath` and checked to stay under the root. The idempotency key protects a retried POST from starting a second import. Status: DONE for the ZIP upload (`ImportJobTests`); the folder import is not built.

**R-28 Scheduler.** Where: `core/scheduler.py` APScheduler + `lock.try_acquire` so only one replica runs a job. Decision: `BackgroundService` with `PeriodicTimer`; for multi-replica guard each job with `pg_try_advisory_lock` (Npgsql, session-level). Jobs Endurain runs: expired token cleanup (`auth/scheduled_jobs.py`), orphan-blob sweep (`activities/orphan_sweep.py`, never deletes blobs younger than 1 h), geocoding backfill, thumbnail backfill, Strava hourly sync, Garmin 1 h/4 h sync. Gravelio needs: expired reset/sign-up token cleanup, notification pruning, and any future backfill. Status: DONE for the scheduler (`PeriodicJob`) and the geocoding backfill; cleanup of expired sessions and TOTP codes happens on write, and Identity's tokens are stateless, so no other job is needed yet.

**R-29 WebSocket manager keeps one socket per user.** Where: `modules/websocket/manager.py`: `active_connections: dict[int, WebSocket]` — a second tab or device replaces the first, so the first stops receiving; it is also process-local, and needs a one-time "ticket" endpoint because browsers cannot set headers on a WebSocket.
Decision: ASP.NET Core SignalR (`Microsoft.AspNetCore.SignalR`, in the shared framework) with the existing cookie (same origin, no ticket needed) and `Clients.User(id)`; it supports many connections per user. Push `notification.created`, `activity.created`. Backplane only if API replicas > 1. The SPA currently polls `unread-count` after each navigation (`shell.ts`). Status: MISSING. P3.

**R-30 Repeated exception wrappers.** Where: nearly every Endurain function ends with `except Exception → log → HTTPException(500, "Internal Server Error")` (e.g. `users_goals/utils.py`, `users_profile/router.py`). It hides stack traces' origin and repeats in hundreds of places.
Decision: one global handler: `AddProblemDetails()` + `UseExceptionHandler()` (or FastEndpoints `UseDefaultExceptionHandler()`) that logs with the trace id and returns RFC 9457 problem details. See G-03.

**R-31 Time handling.** Endurain does local-date filtering in SQL (`activity/query.py` `local_start_time_expression`, `local_date_range_conditions`) so "today", date-range filters and summaries follow the activity's own time zone, and goal windows follow the user's. Gravelio's stats take a `date` from the client and use the user's zone (`ActivityStats.Today`). Keep: filters on activities by date range must compare the activity-local date (`start_time AT TIME ZONE timezone`), not UTC. Test an activity at 23:30 local that is next-day UTC.

**R-32 Keyset pagination for the feed.** Where: `GET /activities/feed?cursor=` (opaque cursor) while other lists are offset-paged. Offset paging on a feed duplicates or skips rows when a new activity arrives between pages. Gravelio feed (`Activities/FollowingFeed`) is offset-paged. Decision: cursor = `(StartTime, Id)` of the last row, `ORDER BY StartTime DESC, Id DESC`. Status: PARTIAL.

**R-33 Public profile and sharing are separate switches.** `public_shareable_links` and `public_shareable_links_user_info` (owner name/photo on a public link) — anonymous reads need both an admin switch and the activity's own visibility `public` and not hidden, and answer 404 for everything else (so ids cannot be probed). Keep that exact rule; minimal DTO (audit rule 1).

**R-34 Strava bulk export parser is language-dependent.** `docs/features/importing.md`: "only works with English .csv files". The parser reads columns by English header names. Decision: detect missing required headers and answer 422 with the list of expected names; do not guess translations. Add a fixture with a non-English header to the tests. Check `Activities/Import/StravaExport.cs` before extending it.

**R-35 Emails and UI are translated; ours are not.** Endurain ships 30 UI locales (`frontend/src/i18n/locales`: bg ca cs da de el en es et fi fr gl hr hu it lt lv nb nl pl pt-PT ro sk sl sr sv tr uk zh-Hans zh-Hant) and translated email templates (`core/email_templates.py`, `core/i18n`). Gravelio builds once per locale with `@angular/localize` but only English text exists. Email text is English only.

### A4. Things Endurain gets right (keep)

- Hide-flag gate in one place for every child resource and "answer as if it did not exist" for anyone not allowed (`child_access_service.py`).
- Heart-rate zones computed on read from current max HR, not stored.
- Local-day correctness for goals and summaries.
- Every derived task has a scheduled backfill.
- Strava unlink deletes Strava data.
- Duplicate policy that never destroys user data (hidden + notification).
- ETag/If-Match on edits.
- Profile export/import as one ZIP (data + files), with secrets stripped before export (`users_profile/router.py` removes `password`, `mfa_secret`, `access_type`, `active`, `mfa_enabled`, `email_verified`, `pending_admin_approval`).

---

## Part B — Feature inventory: Endurain vs Gravelio

Gravelio routes below are under `/api/v1`. "E:" is the Endurain route or module; "G:" is Gravelio's counterpart.

### B1. Authentication and account security

| ID    | Feature (Endurain)                                                                                                         | Gravelio                                                                                                                               | Status  | Pri | Notes                                                                                                                                                                                |
| ----- | -------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------- | ------- | --- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| F-A01 | Local login, logout (`/auth/login`, `/auth/logout`)                                                                        | `POST /auth/login`, `/auth/logout` (Identity cookie)                                                                                   | DONE    |     | Different mechanism by design (JWT+refresh vs cookie).                                                                                                                               |
| F-A02 | Token refresh `/auth/refresh`, rotation, reuse detection                                                                   | n/a                                                                                                                                    | SKIP    |     | Cookie sliding expiry replaces it (R-10).                                                                                                                                            |
| F-A03 | Sessions: list/revoke own (`/profile/sessions`), admin list/revoke per user (`/sessions/user/{id}`)                        | `UserSessions` + `SessionTicketStore`; `/users/me/sessions`, admin `/users/{id}/sessions`                                              | DONE    | P2  | R-10.                                                                                                                                                                                |
| F-A04 | Progressive login lockout                                                                                                  | Identity lockout 5 tries / 15 min                                                                                                      | PARTIAL | P3  | Identity options only; Endurain escalates.                                                                                                                                           |
| F-A05 | Step-up auth for sensitive changes + own lockout                                                                           | `MfaCodes.StepUpAsync` for MFA changes and password change; failures share Identity's lockout                                          | PARTIAL | P3  | R-03. Still missing: step-up on API-key creation and IdP link (neither exists yet) and Endurain's separate escalating lockout.                                                       |
| F-A06 | MFA TOTP: status, setup, enable, disable, verify at login                                                                  | `/users/me/mfa*`, `/auth/mfa/verify`, Security page                                                                                    | DONE    |     | One-time codes via `UsedTotpCodes`. No QR code yet: the page shows the key as text (a QR library would be a new third-party dependency).                                             |
| F-A07 | MFA backup codes: status, regenerate, use at login                                                                         | recovery codes through Identity, same routes                                                                                           | DONE    |     |                                                                                                                                                                                      |
| F-A08 | API keys: list, create, revoke, delete; `X-API-Key`                                                                        | `/users/me/api-keys*`, key section on the Security page, accepted by `POST /activities/upload`                                         | DONE    |     | R-09. One use (upload), so no scope list; header only.                                                                                                                               |
| F-A09 | Identity providers (OIDC SSO): admin CRUD, templates, login, link/unlink, auto-create, mapping, auto-redirect, mobile PKCE | `Sso/`: admin CRUD, login, link/unlink, auto-create, auto-redirect (OIDC code flow + PKCE)                                             | DONE    | P2  | Mobile PKCE redirect schemes and user-info sync/mapping are not built; add them when the Android app needs them.                                                                     |
| F-A10 | Sign-up with email verification                                                                                            | `POST /auth/signup`, `/auth/verify-email`                                                                                              | DONE    |     |                                                                                                                                                                                      |
| F-A11 | Sign-up admin approval (flag `pending_admin_approval`, approve route `PUT /users/{id}/approve`, admin notification)        | `User.PendingApproval`, `PUT /users/{id}/approve`, admin notification, login says "waiting for approval" only after the right password | DONE    | P2  | No approve endpoint, no admin notification, and "waiting for approval" is indistinguishable from "deactivated". Add `PendingApproval` bool + `Approve` endpoint + notification type. |
| F-A12 | Password reset by email (`/password-reset/request`, `/confirm`)                                                            | `POST /auth/forgot-password`, `/auth/reset-password`                                                                                   | DONE    |     |                                                                                                                                                                                      |
| F-A13 | Configurable password policy (server settings `password_type`, lengths)                                                    | fixed 8 / 12 (admins)                                                                                                                  | PARTIAL | P3  | Options pattern would need a DB-backed `IOptionsMonitor`; low value.                                                                                                                 |
| F-A14 | Local login can be disabled when SSO is on (`local_login_enabled`)                                                         | `LocalLoginEnabled` (admins keep passwords)                                                                                            | DONE    | P3  | Needs F-A09.                                                                                                                                                                         |
| F-A15 | Rate-limit tiers                                                                                                           | global read/write limits per user or IP, `sensitive`, `upload`                                                                         | DONE    | P1  | R-02.                                                                                                                                                                                |

### B2. Users and profile

| ID    | Feature                                                            | Gravelio                                                                                                      | Status  | Pri | Notes                                                                                                                                                                                 |
| ----- | ------------------------------------------------------------------ | ------------------------------------------------------------------------------------------------------------- | ------- | --- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| F-U01 | Own profile read/update (`GET/PUT /profile`)                       | `GET/PUT /users/me`                                                                                           | DONE    |     |                                                                                                                                                                                       |
| F-U02 | Change own password                                                | `PUT /users/me/password`                                                                                      | DONE    |     | Revokes other sessions.                                                                                                                                                               |
| F-U03 | Admin user CRUD, list paged                                        | `GET/POST /users`, `PUT/DELETE /users/{id}`                                                                   | DONE    |     |                                                                                                                                                                                       |
| F-U04 | Admin lookups: by id, username, email, "username contains"         | list only                                                                                                     | PARTIAL | P3  | `GET /users` has no search; add `q` like gears/people.                                                                                                                                |
| F-U05 | Profile photo upload/delete, avatar everywhere                     | `Users/UploadPhoto`, `GetPhoto`, `DeletePhoto`; `JpegPhoto.Clean`; avatars in the toolbar, people and profile | DONE    | P2  | Store under data volume, re-encode with a BCL-compatible imaging step (decode check, size cap, strip EXIF); serve through an authorized endpoint. No new image package unless needed. |
| F-U06 | Privacy settings (default visibility, 12 hide flags)               | `/users/me/privacy`: default visibility + 11 flags (incl. laps)                                               | DONE    | P2  | Workout sets/steps have no data yet; add their flags with that feature.                                                                                                               |
| F-U07 | "Existing activities visibility" bulk update (`PATCH /activities`) | none                                                                                                          | MISSING | P3  | One `ExecuteUpdateAsync`.                                                                                                                                                             |
| F-U08 | Goals: list, results, create, update, delete                       | `/goals`, `/goals/progress`, Goals page                                                                       | DONE    |     | One target per period + activity group + measure (unique index). Duration counts moving time. Periods use the activity's local date.                                                  |
| F-U09 | Default gear per activity type                                     | `GET/PUT /gears/defaults`                                                                                     | DONE    |     | Row per slot instead of 15 columns.                                                                                                                                                   |
| F-U10 | Integrations settings (Strava/Garmin link state)                   | none                                                                                                          | MISSING | P2  | R-12, R-14.                                                                                                                                                                           |
| F-U11 | Profile export (ZIP) and import                                    | `DataTransfer/`                                                                                               | DONE    | P2  | Streaming `ZipArchive` response; secrets stripped; import applies R-06 limits and the same dedupe as upload. Needed for GDPR-style "download my data".                                |
| F-U12 | Delete user cascades data and files                                | `DELETE /users/{id}` (FK cascades, including the picture row)                                                 | DONE    | P2  | Files (photo, media, retained source files) have no cleanup yet because those features do not exist; add the cleanup when they land.                                                  |
| F-U13 | Search users (SearchView)                                          | `GET /people?q=`                                                                                              | DONE    |     |                                                                                                                                                                                       |
| F-U14 | Own-account deletion by the user                                   | none                                                                                                          | MISSING | P3  | Endurain's only delete route is the admin one (`DELETE /users/{id}`, scope `users:write`); decide product-wise.                                                                       |

### B3. Activities

| ID     | Feature                                                                                              | Gravelio                                                                                                           | Status  | Pri | Notes                                                                                                                                                                             |
| ------ | ---------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------ | ------- | --- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| F-A01a | Upload GPX / TCX / FIT                                                                               | `POST /activities/upload`                                                                                          | DONE    |     | Multi-session FIT supported.                                                                                                                                                      |
| F-A02a | `.gz` wrapped files                                                                                  | `SupportedExtensions` = gpx, tcx, fit                                                                              | MISSING | P3  | R-06.                                                                                                                                                                             |
| F-A03a | Duplicate detection (3 tiers)                                                                        | same file again: 409 (`DedupKey`); same start, different file: stored hidden + notification                        | DONE    |     | R-24. Provider ids do not exist yet; the Strava import skips same-start activities.                                                                                               |
| F-A04a | Bulk import ZIP + job progress                                                                       | `Imports/`: `POST /activities/import/zip`, `/activities/import/jobs`, `BulkImportJob`                              | DONE    | P2  | R-27.                                                                                                                                                                             |
| F-A05a | Bulk import from server folder                                                                       | none                                                                                                               | MISSING | P3  | R-27.                                                                                                                                                                             |
| F-A06a | Strava bulk export import (activities, gear, photos)                                                 | `POST /activities/import/strava` (activities + gear)                                                               | PARTIAL | P2  | Photos not imported (no media yet). R-34.                                                                                                                                         |
| F-A07a | Nike Run Club import                                                                                 | documentation only in Endurain                                                                                     | SKIP    |     | Convert to GPX/FIT first.                                                                                                                                                         |
| F-A08a | List own activities with filters: type, date range, name, sort                                       | `GET /activities` with `type`, `from`, `to`, `q`, `gear_id`, `sort`, `order`; filter form on the Activities page   | DONE    |     | Dates use the activity's local day (custom `timezone()` DbFunction).                                                                                                              |
| F-A09a | Get activity                                                                                         | `GET /activities/{id}`                                                                                             | DONE    |     |                                                                                                                                                                                   |
| F-A10a | Edit activity: name, description, type, visibility                                                   | `PUT /activities/{id}`                                                                                             | DONE    |     |                                                                                                                                                                                   |
| F-A11a | Edit extras: `private_notes`, `workout_feeling`, `workout_rpe`, `is_hidden`, per-activity hide flags | `/activities/{id}/private`; edit form on the activity page                                                         | DONE    |     |                                                                                                                                                                                   |
| F-A12a | Optimistic concurrency on edit                                                                       | `xmin` ETag + `If-Match` on activity and gear edits (`ETags`)                                                      | DONE    | P2  | R-25.                                                                                                                                                                             |
| F-A13a | Delete activity (+ cleans blobs)                                                                     | `DELETE /activities/{id}`                                                                                          | DONE    |     | Blob cleanup when blobs exist.                                                                                                                                                    |
| F-A14a | Per-activity hide overrides                                                                          | `Activity.HiddenDetails` (null = follow the user's settings)                                                       | DONE    |     | R-01. Laps/sets do not exist yet.                                                                                                                                                 |
| F-A15a | Streams: all, by type (`/streams/{type}`) with per-type masking                                      | `GET /activities/{id}/streams` (all, masked)                                                                       | DONE    |     | Single-type route is optional.                                                                                                                                                    |
| F-A16a | Heart-rate zones computed on read                                                                    | `GET /activities/{id}/hr-zones` (`HrZones`)                                                                        | DONE    | P2  | From `User.MaxHeartRate`; zone boundaries (60/70/… % of max HR) as in Endurain `modules/activities/activity_streams/hr_zones.py`.                                                 |
| F-A17a | Laps (`/{id}/laps`)                                                                                  | `ActivityLaps`; `GET /activities/{id}/laps`, `/public/activities/{id}/laps`; TCX and FIT parsers; in export/import | DONE    | P2  | Parsers drop laps today. R-21. Mask coordinates with the map.                                                                                                                     |
| F-A18a | Workout sets, workout steps, exercise titles                                                         | none                                                                                                               | MISSING | P3  | R-21, R-22.                                                                                                                                                                       |
| F-A19a | Media (photos): list, upload, delete                                                                 | none                                                                                                               | MISSING | P3  | Needs the image helper from F-U05.                                                                                                                                                |
| F-A20a | Reverse geocoding city/town/country (background + backfill)                                          | `GeocodingJob` (Nominatim, opt-in), `ActivityLocationAttempts`, masked like the map                                | DONE    | P2  | R-05, R-23. Provider: Nominatim (rate limit 1 req/s, set a User-Agent) via `IHttpClientFactory`; run in a `BackgroundService`. Hidden when `HideActivityLocation` (audit rule 2). |
| F-A21a | Map thumbnails rendered server-side from tile server                                                 | none                                                                                                               | MISSING | P3  | Heavy (tile fetch + stitching). Alternative: render the route client-side only. Decide before building.                                                                           |
| F-A22a | Summary: week / month / year / lifetime with type breakdown                                          | `GET /activities/stats`                                                                                            | DONE    |     |                                                                                                                                                                                   |
| F-A23a | Following feed                                                                                       | `GET /activities/feed`                                                                                             | PARTIAL | P3  | R-32 keyset.                                                                                                                                                                      |
| F-A24a | Another user's activities                                                                            | `GET /users/{id}/activities`                                                                                       | DONE    |     | Masked.                                                                                                                                                                           |
| F-A25a | Activities by gear                                                                                   | `gear_id` filter; link on the gear page                                                                            | DONE    |     |                                                                                                                                                                                   |
| F-A26a | Public share link (anonymous read of one activity + streams)                                         | `GET /public/activities/{id}` and `/streams`, `/share/:id` page, copy-link button                                  | DONE    |     | Laps and media do not exist yet. R-33.                                                                                                                                            |
| F-A27a | Activity types list                                                                                  | `GET /activities/types`                                                                                            | DONE    |     |                                                                                                                                                                                   |
| F-A28a | Refresh/re-import activities from providers (`/refresh`)                                             | none                                                                                                               | MISSING | P3  | After Strava sync exists.                                                                                                                                                         |
| F-A29a | Retain the source file per activity; export it                                                       | none                                                                                                               | MISSING | P3  | Endurain keeps the original under a blob area for export.                                                                                                                         |
| F-A30a | Markdown description editor                                                                          | plain text                                                                                                         | SKIP    |     | Avoids an XSS surface. Revisit with a sanitising renderer.                                                                                                                        |

### B4. Gears

| ID    | Feature                                                      | Gravelio                                           | Status  | Pri |
| ----- | ------------------------------------------------------------ | -------------------------------------------------- | ------- | --- |
| F-G01 | Gear CRUD, filters (nickname, type), paged                   | `/gears` CRUD, `q`, `gear_type`, `active`          | DONE    |     |
| F-G02 | Components CRUD, component types                             | `/gears/{id}/components`, `/gears/component-types` | DONE    |     |
| F-G03 | Component wear (distance since purchase)                     | `used_distance`                                    | DONE    |     |
| F-G04 | Default gear per activity type                               | see F-U09                                          | DONE    |     |
| F-G05 | Gear sync from Strava / Garmin (provider gear ids)           | Strava export maps gear by name only               | PARTIAL | P3  |
| F-G06 | Import Strava bikes/shoes (`/strava/import/bikes`, `/shoes`) | via export import                                  | PARTIAL | P3  |

### B5. Health

| ID    | Feature                                             | Gravelio                                  | Status  | Pri |
| ----- | --------------------------------------------------- | ----------------------------------------- | ------- | --- |
| F-H01 | Weight CRUD (+ body composition)                    | `/health/weight`                          | DONE    |     |
| F-H02 | Steps CRUD                                          | `/health/steps`                           | DONE    |     |
| F-H03 | Sleep CRUD + scoring                                | `/health/sleep`, `Health/SleepScoring.cs` | DONE    |     |
| F-H04 | Water CRUD (day total)                              | `/health/water`                           | DONE    |     |
| F-H05 | Targets get/put                                     | `/health/targets`                         | DONE    |     |
| F-H06 | Fasting CRUD, active, stats, complete               | `/health/fasting*`                        | DONE    |     |
| F-H07 | Bowel movements CRUD                                | `/health/poop`                            | DONE    |     |
| F-H08 | Daily stats `/health/stats/daily`                   | `GET /health/stats/daily` + "Today" card  | DONE    | P2  |
| F-H09 | Health dashboard, resting heart rate view           | none                                      | MISSING | P3  |
| F-H10 | Garmin health sync (body composition, sleep, steps) | none                                      | MISSING | P3  | (R-13) |
| F-H11 | Derived values: BMI at read                         | BMI stored                                | PARTIAL | P3  | R-16   |

### B6. Social and notifications

| ID    | Feature                                                                                                       | Gravelio                                                                                 | Status  | Pri |
| ----- | ------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------- | ------- | --- |
| F-N01 | Followers/following lists, relationship, follow request, accept, reject/cancel, remove follower               | `/users/{id}/followers`, `/following`, `/relationship`, `/follow-requests*`              | DONE    |     |
| F-N02 | Notifications: list paged, unread count, mark read, mark all                                                  | `/notifications*`                                                                        | DONE    |     |
| F-N03 | Notification get by id                                                                                        | none                                                                                     | MISSING | P3  |
| F-N04 | Notification types NEW_ACTIVITY, DUPLICATE_ACTIVITY, GARMIN_TOKEN_EXPIRED, ADMIN_NEW_SIGN_UP_APPROVAL_REQUEST | new activity, follow request, follow accepted, duplicate, admin sign-up approval request | DONE    | P2  | Garmin token expiry waits for the Garmin integration (out of scope). | Missing: new activity from someone you follow (Garmin does not exist). |
| F-N05 | Live push over WebSocket                                                                                      | polling                                                                                  | MISSING | P3  | R-29                                                                 |

### B7. Server settings (admin)

| ID    | Feature                                                                                                   | Gravelio                                                                              | Status  | Pri | Notes                                                                                                                                                                     |
| ----- | --------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------- | ------- | --- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| F-S01 | Sign-up flags (enabled, email verification, admin approval)                                               | `GET/PUT /server-settings`                                                            | DONE    |     | `GET /server-settings/public` for the login page.                                                                                                                         |
| F-S02 | Public shareable links (+ show owner info)                                                                | `PublicShareableLinks`, `PublicShareableLinksUserInfo` in server settings             | DONE    |     | Off by default.                                                                                                                                                           |
| F-S03 | Default units / currency / records per page                                                               | none                                                                                  | MISSING | P3  | R-20: defaults for new users only.                                                                                                                                        |
| F-S04 | Tile server URL, attribution, API key, map background colour; tile map templates (`/tile_maps_templates`) | `TILE_URL` / `TILE_ATTRIBUTION` env (API `Tiles:*`, nginx CSP from the same variable) | DONE    | P2  | Chosen by the operator in the environment, not in the admin UI, because the CSP must agree with it. Tile templates, an API key and a map background colour are not built. |
| F-S05 | Login page photo upload/delete                                                                            | none                                                                                  | MISSING | P3  |                                                                                                                                                                           |
| F-S06 | SSO flags (enabled, auto redirect, local login)                                                           | `ServerSettings.SsoAutoRedirect`, `LocalLoginEnabled`                                 | DONE    | P2  | F-A09                                                                                                                                                                     |
| F-S07 | Email configuration view and test email (`/email`, `/email/test`)                                         | `POST /server-settings/email/test` + button                                           | DONE    | P2  | Add `POST /server-settings/email/test` sending to the admin's own address; rate limit `sensitive`.                                                                        |
| F-S08 | Password policy setting                                                                                   | none                                                                                  | MISSING | P3  | F-A13                                                                                                                                                                     |

### B8. Integrations

| ID    | Feature                                                                                                         | Gravelio                                                           | Status  | Pri | Notes                                                                                                                          |
| ----- | --------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------ | ------- | --- | ------------------------------------------------------------------------------------------------------------------------------ |
| F-I01 | Strava OAuth link (BYO client id/secret), state, activities sync (hourly), gear sync, unlink with data deletion | none (file import only)                                            | MISSING | P2  | R-12, R-14, R-28. Official REST API; `IHttpClientFactory` + Polly-style resilience via `Microsoft.Extensions.Http.Resilience`. |
| F-I02 | Garmin Connect link with MFA, activities, gear, health sync                                                     | none                                                               | SKIP    | P3  | R-13.                                                                                                                          |
| F-I03 | RunnerUp / Gadgetbridge auto-upload                                                                             | API keys (F-A08) + `POST /activities/upload` with `X-API-Key`      | DONE    | P2  | Nothing more to build: point RunnerUp or Gadgetbridge at the upload endpoint with a key.                                       |
| F-I04 | Android client                                                                                                  | `frontend/android` (Capacitor shell loading the deployed instance) | DONE    |     | Endurain relies on third-party clients.                                                                                        |

### B9. Platform and cross-cutting

| ID    | Feature                                                                                           | Gravelio                             | Status  | Pri | Notes                                                                                         |
| ----- | ------------------------------------------------------------------------------------------------- | ------------------------------------ | ------- | --- | --------------------------------------------------------------------------------------------- |
| F-P01 | Problem-details errors                                                                            | Microsoft problem details everywhere | DONE    |     | G-03.                                                                                         |
| F-P02 | Request id / correlation                                                                          | OpenTelemetry trace id               | DONE    |     | Return `traceId` in problem details.                                                          |
| F-P03 | Background scheduler + locks                                                                      | `PeriodicJob` + advisory lock        | DONE    | P2  | R-28.                                                                                         |
| F-P04 | Orphan blob sweep, retained files                                                                 | none                                 | MISSING | P3  | Only after blobs exist.                                                                       |
| F-P05 | Migrations at start                                                                               | EF migrations (Development) + bundle | DONE    |     |                                                                                               |
| F-P06 | i18n: 30 UI locales + translated emails                                                           | English only                         | MISSING | P3  | R-35.                                                                                         |
| F-P07 | Docs site (`docs/`, mkdocs: getting started, reverse proxy, scaling, API reference, SSO, privacy) | `README.md`                          | MISSING | P3  | Generate API docs from `FastEndpoints.Swagger`.                                               |
| F-P08 | Federation / ActivityPub hooks                                                                    | flags in frontend config only        | SKIP    |     | Roadmap item in Endurain, not implemented there.                                              |
| F-P09 | Passkeys                                                                                          | none                                 | MISSING | P3  | Roadmap in Endurain. .NET 10 Identity has passkey support built in; do it with the MFA slice. |

### B10. Frontend pages (Endurain `frontend/src/views` vs `frontend/src/app/pages`)

| Endurain view                                                                     | Gravelio page                                                | Status                                                           |
| --------------------------------------------------------------------------------- | ------------------------------------------------------------ | ---------------------------------------------------------------- |
| LoginView, SignUpView, EmailVerificationView, ResetPasswordView                   | login, signup, verify-email, forgot-password, reset-password | DONE                                                             |
| HomeView (dashboard: recent activities, summary, goals)                           | none (activities list is the home)                           | MISSING                                                          |
| ActivitiesView, ActivityDetailView                                                | activities, activity-detail                                  | DONE (no filters, laps, media, edit dialog for hide flags/notes) |
| SummaryView                                                                       | stats                                                        | DONE                                                             |
| GearsView, GearDetailView                                                         | gears, gear-detail                                           | DONE                                                             |
| Health* (Dashboard, Weight, Steps, Sleep, Water, Fasting, Poop, RestingHeartRate) | weight, steps, sleep, water, targets, fasting, poop          | PARTIAL (no dashboard, no resting HR)                            |
| NotificationsView                                                                 | notifications                                                | DONE                                                             |
| SearchView, UserProfileView                                                       | people, person                                               | DONE                                                             |
| ProfileSettingsView                                                               | profile (includes activity privacy section)                  | PARTIAL (no photo, no language list)                             |
| SecuritySettingsView (MFA, sessions, API keys)                                    | none                                                         | MISSING                                                          |
| GoalsSettingsView                                                                 | none                                                         | MISSING                                                          |
| IntegrationsSettingsView                                                          | none                                                         | MISSING                                                          |
| ImportSettingsView                                                                | upload on activities page; Strava import                     | PARTIAL                                                          |
| IdentityProvidersSettingsView                                                     | none                                                         | MISSING                                                          |
| UsersSettingsView, UserDetailView                                                 | users                                                        | PARTIAL (no detail view with sessions)                           |
| ServerSettingsView                                                                | server-settings                                              | PARTIAL                                                          |
| StravaCallbackView                                                                | none                                                         | MISSING                                                          |
| NotFoundView, MenuMobileView                                                      | `**` redirect; top nav                                       | PARTIAL                                                          |

---

## Part C — Problems found in Gravelio itself during this sweep

Read from code at `785c5be`. Each is small; none was fixed by this note.

**G-01 (fixed) `DateTime.UtcNow` used twice** (`ActivityStats.Today`, `ActivityStatsValidator`): both now take a `TimeProvider`.

**G-02 (fixed) Body size limit was global.** `Program.cs:43-44` sets Kestrel `MaxRequestBodySize` and multipart limit to the largest upload (~50 MB) for every route, including anonymous `POST /auth/login`. Anyone can make the server accept and parse 50 MB JSON bodies on the login route, limited only by the login rate limit. Fix: default 1 MB for JSON, and set the large limit only on the upload endpoints (`Options(o => o.WithMetadata(new RequestSizeLimitAttribute(...)))` plus `DisableRequestSizeLimit`-free per-endpoint form limits).

**G-03 (fixed) No handler for unhandled exceptions.** `Program.cs` only has `Errors.UseProblemDetails()` (validation). In Production an exception returns an empty 500 and no trace id. Fix: `AddProblemDetails()` + `UseExceptionHandler()` (or `UseDefaultExceptionHandler()`), add a test that forces an error.

**G-04 Signup approval and deactivation share one flag.** `Auth/Signup/SignupEndpoint.cs:57` sets `Active = !RequireAdminApproval`. A user waiting for approval looks the same as a banned user, and no admin notification or approve action exists (F-A11).

**G-05 Duplicate check is start-time only** (`UploadActivity`, `ImportStrava`): see R-24.

**G-06 `HealthSleep.GarminconnectSleepId`, `HealthWeight.Bmi` copied from Endurain** (R-14, R-16).

---

## Part D — Suggested port order

1. **Security and base**: G-02, G-03, G-01, R-02 rate-limit tiers (P1).
2. **MFA + recovery codes (+ passkeys)** F-A06/F-A07/F-P09, with the one-time-step test (R-04). Then step-up F-A05.
3. **API keys** F-A08, then the upload endpoint accepts them (unlocks F-I03).
4. **Activity list filters** F-A08a, activities by gear F-A25a, extras F-A11a/F-A14a with `IsHidden`, duplicate policy R-24, ETag R-25.
5. **Public share links**: F-S02 + F-A26a, with the minimal-DTO and hide tests.
6. **Goals** F-U08, health daily stats F-H08, dashboard.
7. **Profile export/import** F-U11, **photo** F-U05, **bulk import job** F-A04a.
8. **Scheduler + geocoding + backfills** F-P03, F-A20a (needs R-05 egress guard first).
9. **Laps** F-A17a, **sessions list** F-A03, **SSO** F-A09, **Strava sync** F-I01.
10. Rest (P3).

Out of scope unless asked: Garmin Connect (R-13), federation, server-side thumbnails (decide client-side map first), the event bus/outbox (R-26).
