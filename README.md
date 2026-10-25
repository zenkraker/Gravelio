# Gravelio

A self-hosted fitness tracker: import GPX, TCX and FIT activity files and see them on a map with charts. Built with .NET 10, PostgreSQL and Angular 22.

Gravelio is derived from [Endurain](https://github.com/endurain-project/endurain) and, like it, licensed under the AGPL-3.0 (see `LICENSE`). It is not affiliated with Endurain, and "Endurain" is a registered trademark of its owner.

## Run it

```bash
cp .env.example .env     # then set DB_PASSWORD and ADMIN_PASSWORD (12+ characters)
docker compose up -d --build
```

Open http://localhost:8080 and sign in as `admin` with `ADMIN_PASSWORD`. That password is only read while the database has no users, so change it in the app afterwards.

## HTTPS

Put a TLS-terminating reverse proxy (Caddy, Traefik, ...) in front of port 8080 and keep `SECURE_COOKIES=true`. The proxy must send `X-Forwarded-For` and `X-Forwarded-Proto`. For a plain-http trial on your own machine, set `SECURE_COOKIES=false`.

## Backups

Back up the `db-data` volume (or a `pg_dump`) and, separately, the `api-keys` volume. The keys sign the login cookies, so a database dump alone can't be used to forge sessions; if the keys are lost, everyone just has to sign in again.

## Status

Gravelio is a work in progress. Today: sign-in, user management, importing, listing and viewing activities, gear tracking with components and per-activity defaults, weight/body-composition and daily step readings, following other users with their activity feed, and per-user activity privacy (default visibility and hide flags). Not yet ported from Endurain: anonymous public profiles and activity links, sleep/water/fasting health trackers and health targets, notifications, server settings and email, MFA and API keys, and the Strava and Garmin integrations.

## Develop

Commands, architecture rules and conventions are in [`CLAUDE.md`](.claude/CLAUDE.md).
