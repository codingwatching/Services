# Version Services

[← Back to index](README.md)

Runtime access to build version and git metadata.

**Key Points:**
- Requires `version-data.txt` TextAsset in `Resources/` (resource name: `VersionServices.VersionDataFilename`)
- `VersionEditorUtils` writes `Assets/Configs/Resources/version-data.txt` on editor load and before builds; it uses git CLI
- Call `LoadVersionDataAsync()` once at startup — **all properties except `VersionExternal` throw** `NullReferenceException` if called before data is loaded
- `VersionExternal` is always safe (reads `Application.version` directly, no async requirement)

```csharp
using GameLovers.Services;

// Call once at startup (e.g. in a boot MonoBehaviour or init sequence)
await VersionServices.LoadVersionDataAsync();

// Safe at any time — reads Application.version directly
string externalVersion = VersionServices.VersionExternal;   // "1.0.1"

// These require LoadVersionDataAsync() to have completed first
string internalVersion = VersionServices.VersionInternal;   // "1.0.1-42.main.abc123"
string branch          = VersionServices.Branch;            // "main"
string commit          = VersionServices.Commit;            // "abc123"
string buildNumber     = VersionServices.BuildNumber;       // "42"

// Check if app is outdated against a remote version string
bool outdated = VersionServices.IsOutdatedVersion("1.2.0");
```

## Error Reference

| Call | Exception | Condition |
|------|-----------|-----------|
| Any accessor except `VersionExternal` | `NullReferenceException` | `LoadVersionDataAsync()` not called |
