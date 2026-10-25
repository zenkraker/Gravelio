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

Docker (repo root; copy `.env.example` to `.env` first):

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

- Endpoints MUST use FastEndpoints: one endpoint class per operation, request/response DTOs, and a FluentValidation `Validator<TRequest>` per request. No minimal APIs, MVC controllers or DataAnnotations validation.
- One API project (`backend/Gravelio.Api`), organised by feature folder (`Auth/`, `Users/`, `Activities/`). No layer projects, repositories or service wrappers over EF Core: endpoints use `AppDb` directly. All entities and `AppDb` live in `Data/AppDb.cs`; the schema is owned by EF Core migrations.
- Routes are under `/api/v1`. JSON on the wire is snake_case (`JsonNamingPolicy.SnakeCaseLower`), and errors are RFC 7807 problem details.
- Auth is ASP.NET Core Identity with cookie authentication:
  - Use `UserManager`/`SignInManager` for login, lockout, password rules, two-factor and passkeys, configured through `IdentityOptions`. Never hand-roll tokens, password hashing, lockout or password policy.
  - The auth cookie is httpOnly, Secure and SameSite=Strict. Unsafe requests require the antiforgery token (`IAntiforgery`, header `X-XSRF-TOKEN`).
  - Password minimums: 8 characters for regular users and 12 for admins, plus upper, lower, digit and punctuation. Admins get the extra length through a custom `IPasswordValidator<User>`.
  - Security-sensitive changes (password, role, deactivation) call `UpdateSecurityStampAsync` so other sessions are revoked.
  - Login is rate-limited per IP with `Microsoft.AspNetCore.RateLimiting`.
- Users only ever see their own activities. Queries filter by the signed-in user's id, and another user's resource returns 404, not 403. Admin-only routes require the `admin` role.
- The first admin is seeded on an empty database from `Admin:Password`, or generated and logged once.
- Configuration is bound with the options pattern plus `ValidateDataAnnotations().ValidateOnStart()`, so bad config fails at startup, not on first use. Docker Compose passes unset variables as empty strings, so optional string options must treat blank as unset.
- `Database.MigrateAsync()` runs at startup only in Development. Production applies an EF migration bundle as a one-shot step before the API container starts.
- Deployment is two images, built from the repo root with `docker/api.Dockerfile` (targets `migrate` and `api`) and `docker/web.Dockerfile`: the API image (the API serves no static files), and a web image where nginx serves the Angular build and reverse-proxies `/api` to the API. Keeping one origin is what lets the SameSite=Strict auth cookie work. nginx also sets the CSP, so a new external origin (tiles, fonts, APIs) needs `docker/nginx.conf` updated.
- Observability uses OpenTelemetry for logs, metrics and traces, with ASP.NET Core, HttpClient and Npgsql instrumentation. OTLP export happens only when `OTEL_EXPORTER_OTLP_ENDPOINT` is set; otherwise logs go to the console only.
- Data access:
  - Async all the way, passing the endpoint's `CancellationToken` into EF Core. Never `.Result`, `.Wait()` or `Task.Run` around synchronous code.
  - Reads use `AsNoTracking()` and project to DTOs in the query.
  - List endpoints are paged.
- The XSRF middleware must run after `UseAuthentication` and before `UseAuthorization`: authorization short-circuits 401s, and the SPA gets its first XSRF token from the 401 response of `GET /users/me`.
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
- UI components come from Angular Material and the Angular CDK. Build a custom widget only when neither provides one.
- Every user-facing string is translatable with `@angular/localize` (`i18n` attributes in templates, `$localize` in code). The app builds once per locale.

- `src/app/core/api.ts` mirrors the API's snake_case DTOs. Change both sides together.
- All HTTP goes through `HttpClient`. Auth is the API's httpOnly cookie, so the SPA never handles tokens. Antiforgery uses Angular's built-in XSRF support (`withXsrfConfiguration`, `X-XSRF-TOKEN`). A 401 sends the user to `/login`.
- Units are stored metric (meters, seconds, m/s, s/m pace) and converted only for display, via `core/format.ts` and the user's `units`.
