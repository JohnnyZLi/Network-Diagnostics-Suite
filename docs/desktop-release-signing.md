# Desktop release signing

The normal `Desktop app` workflow builds unsigned development artifacts for every pull request and supported runtime. macOS development artifacts are packaged as valid `.app` bundles, but Gatekeeper will still require explicit approval because they are not signed or notarized.

The manual `Signed desktop release` workflow produces signed and notarized Apple Silicon and Intel DMGs. It uses the protected `desktop-release` GitHub environment and does not run during pull requests.

## Apple requirements

- Apple Developer Program membership
- a **Developer ID Application** certificate
- an app-specific password for the Apple ID used with notarization
- the Apple Developer team ID

Export the Developer ID Application certificate and private key from Keychain Access as a password-protected `.p12` file, then base64-encode the file without line wrapping.

macOS:

```bash
base64 -i DeveloperIDApplication.p12 | tr -d '\n'
```

## Required environment secrets

Configure these secrets on the protected `desktop-release` environment:

- `APPLE_CERTIFICATE_P12_BASE64` — base64-encoded `.p12` contents
- `APPLE_CERTIFICATE_PASSWORD` — password used when exporting the `.p12`
- `APPLE_SIGNING_IDENTITY` — full certificate identity, normally `Developer ID Application: Name (TEAMID)`
- `APPLE_ID` — Apple ID used for notarization
- `APPLE_APP_PASSWORD` — app-specific password for that Apple ID
- `APPLE_TEAM_ID` — ten-character Apple Developer team ID

Require manual approval for the `desktop-release` environment so repository code cannot access release credentials without an explicit reviewer decision.

## Running a signed release

1. Open **Actions → Signed desktop release**.
2. Choose **Run workflow** from the intended release commit.
3. Enter a version such as `0.1.0`.
4. Approve the protected environment deployment.
5. Download both signed artifacts after Apple Silicon and Intel jobs complete.
6. Verify each `.dmg.sha256` before publishing.

The workflow:

1. publishes the self-contained app;
2. runs the bootstrap smoke test;
3. creates a standard macOS app bundle;
4. imports the certificate into a temporary keychain;
5. signs the app with hardened runtime and timestamping;
6. submits the app to Apple's notary service and staples the ticket;
7. creates and signs a DMG;
8. notarizes and staples the DMG;
9. uploads the DMG and SHA-256 file;
10. removes the temporary keychain and certificate file.

Never store the exported `.p12`, its password, the app-specific password, or the unencoded certificate in the repository or ordinary Actions variables.
