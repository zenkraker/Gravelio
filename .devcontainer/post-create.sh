#!/usr/bin/env bash
# Runs once per container build. Everything the agents write lands in the named volumes in devcontainer.json, never on the host.
set -euo pipefail
cd "$(dirname "$0")/.."

# Token-efficiency tooling. Versions are pinned so a rebuild is reproducible; bump them on purpose.
npm install -g @colbymchenry/codegraph@1.6.2 opencode-ai@1.19.4
npx --yes get-pnpm   # official pnpm installer; pnpm then switches to the version pinned in frontend/package.json

# Claude Code: ponytail plugin, installed into CLAUDE_CONFIG_DIR (the volume). Skipped when already there.
claude plugin marketplace add DietrichGebert/ponytail || true
claude plugin install ponytail@ponytail || true

# opencode: codegraph MCP server (Claude Code already gets it from the project's .mcp.json) and the ponytail plugin.
codegraph install --yes --target opencode || true
opencode plugin add @dietrichgebert/ponytail || true

dotnet tool restore
dotnet restore Gravelio.slnx
pnpm --dir frontend install
codegraph init || true   # builds .codegraph/ (gitignored); the file watcher keeps it fresh afterwards
