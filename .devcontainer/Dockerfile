FROM mcr.microsoft.com/devcontainers/base:ubuntu-24.04

# Pre-create the mount points as the dev user: a new named volume copies the owner of the directory it covers,
# so the volumes below are writable without sudo.
USER vscode
RUN mkdir -p ~/.claude ~/.config/opencode ~/.local/share/opencode ~/.local/share/pnpm ~/.nuget
