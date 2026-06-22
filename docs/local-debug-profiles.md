# Local Debug Profiles

## Goal

This file documents the current local debug entry points for `src/Szaipa.Web` on both macOS and Windows.

## Launch profiles

Defined in `src/Szaipa.Web/Properties/launchSettings.json`:

- `Szaipa.Web HTTP`
  - Cross-platform default
  - URL: `http://127.0.0.1:5057`
  - Legacy data sources forced off
  - Live database access forced off

- `Szaipa.Web HTTPS Opt-In`
  - Cross-platform profile for machines that want HTTPS locally
  - URLs:
    - `https://127.0.0.1:5058`
    - `http://127.0.0.1:5057`
  - HTTPS redirection forced on
  - Legacy data sources still forced off

- `IIS Express`
  - Kept only as a Windows convenience profile
  - Not required for normal cross-platform development

## Recommendation

- Use `Szaipa.Web HTTP` as the normal Mac and Windows debug profile during the migration phase.
- Only use the HTTPS profile when the machine already trusts local development certificates.
- Keep legacy source access disabled in launch profiles and enable it only through deliberate local overrides after the read-only scaffolding pass starts.
