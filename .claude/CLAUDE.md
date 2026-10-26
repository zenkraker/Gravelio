# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

Gravelio is a self-hosted fitness tracker: a .NET 10 + Angular 22 rewrite of Endurain (https://github.com/endurain-project/endurain, Python/FastAPI + Vue). The original is the behavioural reference for any feature being ported, not a schema or API to stay compatible with.

## Commands

Backend (repo root; needs PostgreSQL reachable at `ConnectionStrings:Default`):

```bash
dotnet build Gravelio.slnx
dotnet test Gravelio.slnx                                                          # integration tests need Docker (Testcontainers)
dotnet test Gravelio.slnx --filter "FullyQualifiedName~ImportTests.Tcx_is_parsed"   # single test
cd backend/Gravelio.Api && dotnet run                                              # http://localhost:5129, Development env
dotnet tool restore                                                                # once: installs dotnet-ef from dotnet-tools.json
cd backend/Gravelio.Api && dotnet ef migrations add <Name> -o Data/Migrations       # applied on startup in Development only
cd backend/Gravelio.Api && dotnet ef migrations bundle                             # production migration artifact
dotnet format Gravelio.slnx --verify-no-changes                                    # formatting/analyzer gate
```

Frontend (`frontend/`, Node ≥ 24.15; pnpm pinned in `package.json` `packageManager`, and pnpm switches to that version itself):

```bash
npx get-pnpm             # once per machine (official installer; not Corepack, which Node 25+ no longer bundles)
pnpm install
pnpm start               # ng serve, proxies /api to :5129 (proxy.conf.json)
pnpm test                # ng test (Vitest via @angular/build:unit-test)
pnpm build               # outputs to dist/frontend/browser (served by nginx in the web image)
pnpm exec ng lint        # angular-eslint gate, including template accessibility rules
pnpm exec prettier --check .   # formatting gate (.prettierrc)
pnpm e2e                 # Playwright + axe against the running app (needs the API up; `pnpm start` is launched for you)
E2E_BASE_URL=http://localhost:8080 E2E_ADMIN_PASSWORD=… pnpm e2e   # same suite against the Docker stack
```

Android app (`frontend/android`, Capacitor; needs Android Studio and its SDK, JDK 21):

```bash
export GRAVELIO_SERVER_URL=https://gravelio.example   # every cap command reads it (PowerShell: $env:GRAVELIO_SERVER_URL='...')
pnpm cap:sync                                         # build the web app and write the instance URL into android/
pnpm cap:open                                         # open the project in Android Studio
cd android && ./gradlew assembleDebug                 # Windows: gradlew.bat assembleDebug
# APK: android/app/build/outputs/apk/debug/app-debug.apk
```

Docker (repo root; copy `.env.example` to `.env` and set `DB_PASSWORD` and `ADMIN_PASSWORD` first). CI (`.github/workflows/ci.yml`) runs every gate above plus the e2e suite against this stack:

```bash
docker compose up -d --build   # db + one-shot EF migration bundle + api + nginx web on :8080
```

## Working rules

- Use the `ponytail` skill for every coding task and `ponytail-review` before calling a change done.
- Use the `codegraph` skill to find callers, dependencies and impact before changing shared code.
- Don't reinvent the wheel. Before writing any infrastructure code (auth, hashing, validation, rate limiting, caching, problem details, logging, config, JSON, data access), find the official Microsoft package or built-in API that covers it on learn.microsoft.com and use that. Cite the docs URL in the PR or commit body when you introduce a new mechanism.
- Prefer, in this order: the BCL or ASP.NET Core shared framework, official `Microsoft.*` NuGet packages, then a third-party package. Mandated exceptions: FastEndpoints (including its FluentValidation and `FastEndpoints.Swagger` for OpenAPI), Testcontainers, and the OpenTelemetry .NET packages. Any other third-party package needs a reason no Microsoft option exists. Libraries already chosen: `Garmin.FIT.Sdk` (FIT) and `GeoTimeZone` (timezone from lat/lon).
- Package versions live only in `Directory.Packages.props` (Central Package Management). `.csproj` files carry no `Version` attributes.
- Code is AGPL-3.0, as a derivative of Endurain. Don't use the Endurain name or logos: it is a registered trademark.

## Backend invariants

- Each endpoint is a vertical slice in its own folder under the feature (`Gears/CreateGear/`), one file per part: `CreateGearRequest.cs`, `CreateGearResponse.cs`, `CreateGearValidator.cs` (FluentValidation) and `CreateGearEndpoint.cs`. A slice holds only what that operation needs; nothing in it is shared by reading another slice's types. Code that two or more slices need goes in the feature folder (`Gears/GearQueries.cs`), and code that features share goes in a common folder, never into one slice. Create and update get their own request and validator even when the shape matches. Slice files keep the feature's namespace (`Gravelio.Api.Gears`), not one per folder.
- Endpoints MUST use FastEndpoints: one endpoint class per operation, request/response DTOs, and a FluentValidation `Validator<TRequest>` for every request that carries more than route ids (id-only requests share `IdRequest`; a body id is checked by an ownership lookup that answers 404). Optional numbers use the plain range rule (`RuleFor(x => x.Price).InclusiveBetween(...)`), which skips null. No minimal APIs, MVC controllers or DataAnnotations validation.
- One API project (`backend/Gravelio.Api`), organised by feature folder (`Auth/`, `Users/`, `Activities/`). No layer projects, repositories or service wrappers over EF Core: endpoints use `AppDb` directly. All entities and `AppDb` live in `Data/AppDb.cs`; the schema is owned by EF Core migrations.
- Routes are under `/api/v1`. JSON on the wire is snake_case (`JsonNamingPolicy.SnakeCaseLower`).
- Every error response in the whole codebase is ASP.NET Core's official problem-details implementation (https://learn.microsoft.com/aspnet/core/fundamentals/error-handling#problem-details): `AddProblemDetails`, `UseExceptionHandler` and `UseStatusCodePages` turn unhandled exceptions and bare 401/403/404/429 responses into `application/problem+json`, and FastEndpoints' validation and `ThrowError` failures are emitted as `HttpValidationProblemDetails` through `Errors.ResponseBuilder`. Never use FastEndpoints' own `Errors.UseProblemDetails()`, and never write an ad-hoc error body (`Results.Json(new { error })`, a custom DTO). A client mistake that Kestrel reports as `BadHttpRequestException` keeps its own 4xx through `BadHttpRequestExceptionHandler`, not a 500. A new exception-to-status mapping is an `IExceptionHandler`.
- Request-size limits belong only to the endpoints that accept uploads: such an endpoint calls `AllowFileUploads()` and `Options(o => o.WithMetadata(UploadLimits.For(maxBytes)))` in its `Configure`. Never set a global `MaxRequestBodySize` or `FormOptions` limit; every other route keeps the framework defaults.
- Auth is ASP.NET Core Identity with cookie authentication:
  - Use `UserManager`/`SignInManager` for login, lockout, password rules, two-factor and passkeys, configured through `IdentityOptions`. Never hand-roll tokens, password hashing, lockout or password policy.
  - The auth cookie is httpOnly, Secure and SameSite=Strict. Unsafe requests require the antiforgery token (`IAntiforgery`, header `X-XSRF-TOKEN`).
  - Password minimums: 8 characters for regular users and 12 for admins, plus upper, lower, digit and punctuation. Admins get the extra length through a custom `IPasswordValidator<User>`.
  - Security-sensitive changes (password, role, deactivation) call `UpdateSecurityStampAsync` so other sessions are revoked.
  - Login is rate-limited per IP with `Microsoft.AspNetCore.RateLimiting`.
- Return errors with `ThrowError(message, statusCode)` (it always throws, so no `return` after it). An endpoint that makes several `UserManager` calls (one admin edit) wraps them in `db.Database.BeginTransactionAsync` and commits only at the end, so a rejected step never leaves a half-applied change.
- Users only ever see their own data. Look a record up through the `OwnedBy(userId, ...)` extensions in `Requests.cs` (activities, gears, components), never with a hand-written filter; another user's resource returns 404, not 403. A missing resource named in the route is `Send.NotFoundAsync`; one named in the body is `ThrowError(r => r.Prop, msg, 404)`. Admin-only routes join `AdminUsersGroup` (it holds the `admin` role requirement once).
- The first admin is seeded on an empty database from `Admin:Password` (12+ characters). It is required, never generated: a generated password would have to be logged. `DB_PASSWORD` and `ADMIN_PASSWORD` are required by `docker-compose.yml` and empty in `.env.example`.
- Configuration is bound with the options pattern plus `ValidateDataAnnotations().ValidateOnStart()`, so bad config fails at startup, not on first use. Docker Compose passes unset variables as empty strings, so optional string options must treat blank as unset.
- `Database.MigrateAsync()` runs at startup only in Development. Production applies an EF migration bundle as a one-shot step before the API container starts. Login-cookie keys are kept on the `api-keys` volume (`DataProtection:KeysPath`), not in the database.
- Deployment is two images, built from the repo root with `docker/api.Dockerfile` (targets `migrate` and `api`) and `docker/web.Dockerfile`: the API image (the API serves no static files), and a web image where nginx serves the Angular build and reverse-proxies `/api` to the API. Keeping one origin is what lets the SameSite=Strict auth cookie work. nginx also sets the CSP, so a new external origin (tiles, fonts, APIs) needs `docker/nginx.conf` updated. nginx re-resolves the API's address (`resolver` + variable `proxy_pass`) and takes the real client IP from `X-Forwarded-For` for private-network senders, so the per-IP login limit works behind another proxy. The database password reaches Npgsql as `PGPASSWORD`, never inside the connection string.
- Observability uses OpenTelemetry for logs, metrics and traces, with ASP.NET Core, HttpClient and Npgsql instrumentation. OTLP export happens only when `OTEL_EXPORTER_OTLP_ENDPOINT` is set; otherwise logs go to the console only.
- Data access:
  - Async all the way, passing the endpoint's `CancellationToken` into EF Core. Never `.Result`, `.Wait()` or `Task.Run` around synchronous code.
  - Reads use `AsNoTracking()` and project to DTOs in the query.
  - A check-then-insert is never enough on its own: back it with a unique index and save with `db.TrySaveUniqueAsync`, turning `false` into a 409, as activity upload and default gear do.
  - A failed `IdentityResult` becomes `ThrowError(UserRules.Describe(r), UserRules.StatusOf(r))`: 409 for a taken username or email, 400 otherwise.
  - List endpoints are paged: the request derives from `PagedRequest`, its validator from `PagedValidator<T>`, and the answer is `PagedResult<T>` built with `ToPageAsync`. Only a list bounded by its parent (a gear's components) may be unpaged.
- The XSRF middleware must run after `UseAuthentication` and before `UseAuthorization`: authorization short-circuits 401s, and the SPA gets its first XSRF token from the 401 response of `GET /users/me`.
- Client IP and scheme come only from ASP.NET Core's forwarded-headers middleware, which believes `X-Forwarded-*` just from the `TrustedProxies` networks (private ranges by default). Never read `X-Forwarded-For` yourself: a stranger could forge it and walk around the per-IP login limit. `ClientIpTests` covers both directions.
- Never read `HttpContext` after the request completes or from background work. Background work creates its own scope through `IServiceScopeFactory`.
- Outbound HTTP (e.g. Strava, Garmin) goes only through `IHttpClientFactory`.
- Background and scheduled work runs in `BackgroundService` implementations.
- Code reads the current time from an injected `TimeProvider`, never `DateTime.UtcNow`, so tests can control it.
- Logging uses `ILogger` message templates (`LogInformation("Imported {ActivityId}", id)`), never string interpolation.
- Integration tests use `WebApplicationFactory<Program>` against a real PostgreSQL started by Testcontainers. Never use the EF in-memory provider or SQLite as a stand-in. Don't declare `public partial class Program`; .NET 10 generates it.
- The activity import pipeline is `ActivityFileParser` (GPX/TCX/FIT → `ParsedActivity` of `TrackPoint`s), then `ActivityBuilder` (metrics + streams):
  - Parsers only extract data. All metric maths lives in `Computation` and the builder.
  - Device totals (the FIT session) win over values derived from points. The exception is heart rate, which is always recomputed from the stream, excluding zeros.
  - Distance is summed per segment, so gaps between segments never count.
  - Timezone comes from the first GPS point, except for virtual activity types.
  - Uploaded XML is parsed with DTDs prohibited.
- Streams are stored as jsonb arrays in the Endurain waypoint shape (`{"time", "<key>": value}`, map points as `{"time","lat","lon"}`). Stream type ids (1 hr … 8 temperature) and activity type ids (`ActivityTypes`, ported from Endurain) are fixed contracts shared with the frontend.

## Rules from the Endurain audit (apply when porting a module)

Endurain's original code has these flaws; do not carry them over. Each needs a test in the PR that ports the module.

- **Public (anonymous) endpoints return a dedicated minimal DTO**, never the user or activity entity or the signed-in DTO, and only for data the owner has made public. A public user lookup returns id, display name and photo for a user who has a public activity: no email, birthdate, height, heart rate, role or status. The test asserts the sensitive fields are absent.
- **Hiding something hides everything derived from it.** `hide_map` and `hide_location` also blank lap start/end coordinates, geocoded place names and thumbnails. Every stream type maps to a hide flag, and a type with no mapping is hidden (default-deny, never default-visible). The test hides the map and checks laps, then loops over all stream types.
- **A one-time MFA code works once.** Store the last accepted TOTP time step per user and reject any code at or before it (RFC 6238 §5.2). Identity's authenticator provider does not do this. The test submits the same valid code twice.
- **Forged client IPs are ignored** (see the forwarded-headers rule above).

## Angular

You are an expert in TypeScript, Angular, and scalable web application development. You write functional, maintainable, performant, and accessible code following Angular and TypeScript best practices.

### TypeScript Best Practices

- Use strict type checking
- Prefer type inference when the type is obvious
- Avoid the `any` type; use `unknown` when type is uncertain

### Angular Best Practices

- Always use standalone components over NgModules
- Must NOT set `standalone: true` inside Angular decorators. It's the default in Angular v20+.
- Do NOT set `changeDetection: ChangeDetectionStrategy.OnPush` explicitly. `OnPush` is the default in Angular v22+.
- Use signals for state management
- Implement lazy loading for feature routes
- Do NOT use the `@HostBinding` and `@HostListener` decorators. Put host bindings inside the `host` object of the `@Component` or `@Directive` decorator instead
- Use `NgOptimizedImage` for all static images.
  - `NgOptimizedImage` does not work for inline base64 images.

### Accessibility Requirements

- It MUST pass all AXE checks.
- It MUST follow all WCAG AA minimums, including focus management, color contrast, and ARIA attributes.

### Components

- Keep components small and focused on a single responsibility
- Use `input()` and `output()` functions instead of decorators
- Use `model()` for two-way bound properties with `[(prop)]` syntax instead of pairing `input()` with `output()`
- Use `computed()` for derived state
- Use `linkedSignal()` for state derived from multiple reactive sources that must stay synchronized
- Prefer inline templates for small components
- Prefer Signal Forms (`@angular/forms/signals`) for new forms. They are stable in Angular v22+ and provide signal-based state, type-safe field access, and schema-based validation
- When not using Signal Forms, prefer Reactive forms instead of Template-driven ones
- Do NOT use `ngClass`, use `class` bindings instead
- Do NOT use `ngStyle`, use `style` bindings instead
- Do NOT import `CommonModule`, import only the directives and pipes the template uses, such as `AsyncPipe` or `DatePipe`
- When using external templates/styles, use paths relative to the component TS file.

### State Management

- Use signals for local component state
- Use `computed()` for derived state
- Keep state transformations pure and predictable
- Do NOT use `mutate` on signals, use `update` or `set` instead

### Templates

- Keep templates simple and avoid complex logic
- Use native control flow (`@if`, `@for`, `@switch`) instead of `*ngIf`, `*ngFor`, `*ngSwitch`
- Use the async pipe to handle observables
- Do not assume globals like (`new Date()`) are available.

### Services

- Design services around a single responsibility
- Use the `providedIn: 'root'` option for singleton services
- Prefer the `@Service` decorator over `@Injectable({providedIn: 'root'})` for new singleton services (Angular v22+)
- Use the `inject()` function instead of constructor injection

### Gravelio-specific frontend invariants

- pnpm is the only package manager (`cli.packageManager` in `angular.json`, `packageManager` in `package.json`). Never use npm or yarn or commit their lockfiles. Upgrade pnpm with `pnpm self-update`, which updates the pin. Install scripts are denied by default; a new dependency's build script goes into `allowBuilds` in `pnpm-workspace.yaml` only after checking that it's actually needed.
- Build, serve and test only through the Angular CLI builders (`@angular/build`, which wraps esbuild, Rolldown, Vite and Vitest). Configure them in `angular.json`; never add a `vite.config`, webpack config, Karma or Jest.
- Before writing Angular code, use the `angular-cli` MCP server (registered in `.mcp.json` as `pnpm exec ng mcp`, which needs Node ≥ 24.15 and pnpm on PATH): `get_best_practices` for version-matched rules and `search_documentation` for API lookups.

- End-to-end tests use `@playwright/test` against the running app, and every page gets an `@axe-core/playwright` scan. Any AXE violation fails the test.
- After a UI change, run the stack and look at the pages in a real browser (`pnpm start` + the API, then Playwright screenshots and the axe suite) before calling it done. The e2e specs must cover each new page.
- UI components come from Angular Material and the Angular CDK. Build a custom widget only when neither provides one.
- Every user-facing string is translatable with `@angular/localize` (`i18n` attributes in templates, `$localize` in code). The app builds once per locale.

- The Android app is a Capacitor WebView that loads the deployed instance (`frontend/capacitor.config.ts`), not the bundled assets talking to a remote API: the auth cookie is SameSite=Strict and Angular reads the XSRF token from `document.cookie`, so the SPA and `/api` must stay on one origin. `GRAVELIO_SERVER_URL` is required for every `cap` command; the native project is committed, the copied web assets and generated config are not.
- `src/app/core/api.ts` mirrors the API's snake_case DTOs. Change both sides together.
- All HTTP goes through `HttpClient`. Auth is the API's httpOnly cookie, so the SPA never handles tokens. Antiforgery uses Angular's built-in XSRF support (`withXsrfConfiguration`, `X-XSRF-TOKEN`). A 401 sends the user to `/login`.
- Units are stored metric (meters, seconds, m/s, s/m pace) and converted only for display, via `core/format.ts` and the user's `units`.

### Design language: "Gravel"

Every Angular component and page must follow this to pass review. The theme lives in `frontend/src/material-theme.scss` (the single source) and `src/styles.css`.

- **Official components only.** Use `@angular/material` and `@angular/cdk` for every control: `mat-table` (never a native `<table>`), `mat-checkbox`, `mat-progress-bar`, `mat-form-field` + `matInput`/`mat-select`, `mat-card`, `mat-dialog`, `mat-snack-bar`, `mat-paginator`, `mat-button` variants. A native element or hand-rolled widget needs a reason Material has no equivalent (today only Leaflet and Chart.js qualify). Screen-reader-only text uses CDK's `.cdk-visually-hidden`, never a local copy.
- **Colour comes from tokens.** Style only with `--mat-sys-*` variables (`--mat-sys-primary`, `-surface-container`, `-on-surface-variant`, `-outline-variant`, `-error`, ...). No hex, rgb or named colours in components. The palette is clay-orange primary, moss tertiary; the app follows the system light/dark setting (`color-scheme: light dark`), so every screen must be checked in both.
- **Canvas and map colours** (Chart.js, Leaflet) can't read `light-dark()` variables: resolve them with `themeColor()` from `core/theme.ts`, including text and grid colours.
- **Shape.** Rounded corners from the `--mat-sys-corner-*` tokens (12/16/24/32px), never a literal `border-radius` except `var(--mat-sys-corner-full)` for pills. Active nav items and buttons are pills.
- **Type.** System fonts only (the nginx CSP allows no external font origin); headings use the theme's brand face. Take sizes from `font: var(--mat-sys-title-large)` and the other `--mat-sys-*` type tokens. Numbers that line up in columns inherit tabular figures from `body`.
- **Form fields are outlined.** A page with `mat-form-field` lists `providers: [OUTLINE_FIELDS]` (`core/outline-fields.ts`). Never provide it in `appConfig`: that pulls the form-field code into the initial bundle (it pushed it past the 500 kB budget).
- **Links.** Card titles link in `--mat-sys-primary`, underlined only on hover and focus (global rule in `styles.css`). No default browser blue underline anywhere.
- **Surfaces.** Cards are `mat-card appearance="outlined"`; activity cards add the `.topo` class for the contour-line texture. Page chrome uses `--mat-sys-surface-container`.
- **Accessibility is part of the look.** Text and controls keep WCAG AA contrast in light and dark; tables get an `aria-label`; icon-only or repeated buttons get a specific `aria-label`.
- **Check it visually.** After a UI change, look at the page in light and dark and run the Playwright + axe suite before calling it done (see the rule above).

<!-- CODEGRAPH_START -->
## CodeGraph

In repositories indexed by CodeGraph (a `.codegraph/` directory exists at the repo root), reach for it BEFORE grep/find or reading files when you need to understand or locate code:

- **MCP tool** (when available): `codegraph_explore` answers most code questions in one call — the relevant symbols' verbatim source plus the call paths between them, including dynamic-dispatch hops grep can't follow. Name a file or symbol in the query to read its current line-numbered source. If it's listed but deferred, load it by name via tool search.
- **Shell** (always works): `codegraph explore "<symbol names or question>"` prints the same output.

If there is no `.codegraph/` directory, skip CodeGraph entirely — indexing is the user's decision.
<!-- CODEGRAPH_END -->
