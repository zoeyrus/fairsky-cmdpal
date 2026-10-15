# Migration from WorldWeather to FairSky

## Safe first pass

- Preserve a copy of the working WorldWeather 0.0.4.0 source and installed MSIX.
- Create a new Git repository named `fairsky-cmdpal` **without renaming the internal MSIX Identity yet**.
- Change the `DisplayName` in `WorldWeatherCommandsProvider.cs` to `FairSky`.
- Change the visible `DisplayName` and `Description` in `Package.appxmanifest` (where present) to FairSky.
- Keep the package Identity Name and Publisher unchanged, along with the extension GUID and application registration, to preserve update continuity.
- Update the manifest version to `0.0.5.0`; rebuild, sign with the same development certificate, install and verify Dock entries.
- Search for hard-coded `WorldWeather` strings; change only user-facing ones in this pass. Paths used for saved configuration should NOT be renamed without migration logic.

## Later technical rename

- Rename namespace, .csproj, root class, executable references and generated build paths as a separately tested change.
- Check `Program.cs`, `Package.appxmanifest`, `.sln`, any .props, and publisher/COM registration references before building.
- Don't generate a new extension GUID without planning a migration of Dock references.
- If changing MSIX package identity, treat it as a new app and plan how to migrate saved user configuration and pinned items.

## Before going public

- Confirm any third-party licensing notices from Microsoft's extension template are preserved.
- Replace any personal email address in source User-Agent with a suitable public project contact (MET Norway expects a truthful, contactable User-Agent).
- Audit tracked files with `git status`, `git ls-files` and search for `.pfx`, passwords, emails, tokens and local paths.
- Add tested installation instructions for end users and decide on the production signing and distribution route.
