# Version Services

[← Back to index](README.md)

Runtime access to build version and git metadata.

**Key Points:**
- Requires `version-data.txt` TextAsset in `Resources/` (resource name: `VersionServices.VersionDataFilename`)
- `VersionEditorUtils` writes `Assets/Configs/Resources/version-data.txt` on editor load and before builds; it uses git CLI
- `VersionExternal` is always safe (reads `Application.version` directly, no load required)
- Call **`LoadVersionData()` (sync)** or **`LoadVersionDataAsync()` (async)** once at startup — `VersionInternal`, `Branch`, `Commit`, and `BuildNumber` throw `Exception("Version Data not loaded.")` until either has completed successfully
- Both methods share the same parse/assign pipeline; pick sync for the default tiny `version-data.txt` payload and async only if you've extended `VersionData` with large blobs that would noticeably stall the main thread

```csharp
using GameLovers.Services;

// Call once at startup (e.g. in a boot MonoBehaviour or init sequence).
// Pick the sync or async variant — both populate the same static state.
VersionServices.LoadVersionData();              // sync (recommended default)
// await VersionServices.LoadVersionDataAsync(); // async alternative

// Safe at any time — reads Application.version directly
string externalVersion = VersionServices.VersionExternal;   // "1.0.1"

// These require a successful load (either variant) to have completed first
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
| `VersionInternal`, `Branch`, `Commit`, `BuildNumber` | `Exception` | Version data is not loaded |
