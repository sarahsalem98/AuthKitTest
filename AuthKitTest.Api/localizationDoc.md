# LocalizationKit

Reusable ASP.NET Core localization engine for .NET 8.
The package is an **engine only** — it contains zero `.resx` files. You supply your own resource files; supported cultures are **auto-detected** at startup.

---

## Installation

```bash
dotnet add package LocalizationKit
```

---

## Quick Setup

### 1. Add resource files to your project

```
YourApi/
├── Resources/
│   ├── SharedResource.cs          ← marker class
│   ├── SharedResource.resx        ← English (base/default)
│   ├── SharedResource.ar.resx     ← Arabic  (auto-detected)
│   └── SharedResource.fr.resx     ← French  (auto-detected)
```

**`Resources/SharedResource.cs`** — marker class, no logic:

```csharp
namespace YourApi.Resources;

public class SharedResource { }
```

**`.csproj`** — mark all `.resx` files as embedded resources:

```xml
<ItemGroup>
  <EmbeddedResource Include="Resources\SharedResource.resx" />
  <EmbeddedResource Include="Resources\SharedResource.ar.resx" />
  <EmbeddedResource Include="Resources\SharedResource.fr.resx" />
</ItemGroup>
```

---

### 2. Register in `Program.cs`

```csharp
using YourApi.Resources;
using SharedLocalization.Extensions;

// Minimal — cultures auto-detected from .resx files
builder.Services.AddLocalizationKit(options =>
    options.ResourcesAssembly = typeof(SharedResource).Assembly);

var app = builder.Build();

// Add after UseRouting(), before UseAuthorization()
app.UseLocalizationKit();
```

**Full options:**

```csharp
builder.Services.AddLocalizationKit(options =>
{
    options.ResourcesAssembly = typeof(SharedResource).Assembly;
    options.ResourcesPath     = "Resources";       // default
    options.ResourceBaseName  = "SharedResource";  // default
    options.DefaultCulture    = "en-US";           // default
    options.SupportedCultures = new() { "en-US", "ar-SA" }; // override auto-detect
});
```

---

### 3. Use in controllers and services

```csharp
public class CompaniesController : ControllerBase
{
    private readonly ILocalizationService _loc;

    public CompaniesController(ILocalizationService loc) => _loc = loc;

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(int id)
    {
        var company = await _service.GetByIdAsync(id);
        if (company is null)
            return NotFound(new { message = _loc.T("Error_NotFound") });

        return Ok(new {
            data    = company,
            isRtl   = _loc.IsRtl,
            culture = _loc.CurrentCulture
        });
    }
}
```

---

## `ILocalizationService` API

| Member | Description |
|---|---|
| `T(string key)` | Returns localized string. Returns the key itself if not found — never throws. |
| `T(string key, params object[] args)` | Returns localized string with `{0}`, `{1}` placeholders replaced. |
| `IsRtl` | `true` when the current request culture is right-to-left (Arabic, Urdu, Hebrew, Farsi…). |
| `CurrentCulture` | Full culture name of the current request e.g. `"ar-SA"`, `"en-US"`. |

---

## Culture Detection (per request)

| Priority | Source | Example |
|---|---|---|
| 1 | `Accept-Language` header | `Accept-Language: ar-SA` |
| 2 | Query string `lang` param | `GET /api/resource?lang=ar` |
| 3 | `DefaultCulture` from options | `"en-US"` |

The response always includes a `Content-Language` header showing the culture actually used.

Short codes are normalized automatically:

| Short code | Resolves to |
|---|---|
| `ar` | `ar-SA` |
| `en` | `en-US` |
| `zh` | `zh-CN` |
| `fr` | `fr-FR` (via .NET dynamic resolution) |
| Any valid ISO 639-1 | Resolved via `CultureInfo.GetCultures()` |

---

## `.resx` Key Naming Convention

Use `Category_Description` format:

| Category | Example keys |
|---|---|
| `Error_` | `Error_NotFound`, `Error_Unauthorized`, `Error_ValidationFailed`, `Error_DuplicateEntry`, `Error_ServerError` |
| `Success_` | `Success_Created`, `Success_Updated`, `Success_Deleted` |
| `Validation_` | `Validation_Required`, `Validation_MaxLength`, `Validation_InvalidEmail` |
| `Label_` | `Label_Name`, `Label_Email`, `Label_Status` |
| `Confirm_` | `Confirm_DeleteTitle`, `Confirm_DeleteMessage` |

Placeholders use `{0}`, `{1}`:

```xml
<data name="Validation_Required" xml:space="preserve">
  <value>{0} is required</value>
</data>
```

```csharp
_loc.T("Validation_Required", "Name") // → "Name is required"
```

---

## Optional: Auto-generate Key Constants (T4 Template)

Add `Resources/LocalizationKeys.tt` to avoid typing key strings manually.
The T4 reads `SharedResource.resx` and generates `LocalizationKeys.cs` grouped by prefix.
To regenerate: right-click `LocalizationKeys.tt` → **Run Custom Tool**.

Generated output:

```csharp
// Auto-generated from SharedResource.resx — do not edit manually
public static class LocalizationKeys
{
    public static class Error
    {
        public const string NotFound     = "Error_NotFound";
        public const string Unauthorized = "Error_Unauthorized";
        public const string ServerError  = "Error_ServerError";
    }
    public static class Success
    {
        public const string Created = "Success_Created";
        public const string Updated = "Success_Updated";
        public const string Deleted = "Success_Deleted";
    }
    public static class Validation
    {
        public const string Required     = "Validation_Required";
        public const string MaxLength    = "Validation_MaxLength";
        public const string InvalidEmail = "Validation_InvalidEmail";
    }
}
```

Usage:

```csharp
return NotFound(new { message = _loc.T(LocalizationKeys.Error.NotFound) });
```

---

## Adding a New Language

1. Drop `SharedResource.{culture}.resx` into the `Resources/` folder
2. Add it as `EmbeddedResource` in the `.csproj`
3. That is it — no `Program.cs` changes needed

`CultureScanner` detects it automatically on next startup.

---

## `IsRtl` Behaviour

Uses `CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft` — not a hardcoded language list.
Works for any RTL language: Arabic, Urdu, Hebrew, Farsi, and any future addition.
