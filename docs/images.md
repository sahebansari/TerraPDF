# Images

TerraPDF supports **PNG** and **JPEG** image embedding with automatic aspect-ratio
preservation.

---

## Basic Usage

### Fill available width

The image scales to fill the full width of its container slot while keeping the
original aspect ratio. Height is calculated automatically.

```csharp
container.Image("path/to/photo.jpg");
container.Image("path/to/diagram.png");
```

### Fixed width

Constrains the image to a specific width in PDF points. Height is still computed
from the aspect ratio. Useful for logos and icons that should not fill the page.

```csharp
container.Image("logo.png", 120);      // 120 pt wide
container.Image("thumbnail.jpg", 60);
```

---

## Positioning Fixed-Width Images

Because `Image()` returns `IContainer`, wrap it with an alignment decorator to
control horizontal position:

```csharp
// Centred logo
container.AlignCenter().Image("logo.png", 150);

// Right-aligned stamp
container.AlignRight().Image("stamp.png", 80);

// Left-aligned (default, no wrapper needed)
container.Image("icon.png", 32);
```

---

## Combining with Other Decorators

Images participate in the full decorator chain:

```csharp
// Logo inside a padded, bordered box
container
    .Border(1, Color.Grey.Lighten2)
    .Padding(8)
    .AlignCenter()
    .Image("logo.png", 100);

// Full-width banner with a bottom accent bar
page.Header().Column(col =>
{
    col.Item().Image("banner.jpg");
    col.Item().Background(Color.Blue.Darken2).Padding(3);
});
```

---

## Supported Formats

| Format | Extensions |
|--------|------------|
| PNG | `.png` |
| JPEG | `.jpg`, `.jpeg` |

> Files are read from the file-system path supplied at render time.
> Use `AppContext.BaseDirectory` to resolve paths relative to the executable:
> ```csharp
> string logo = Path.Combine(AppContext.BaseDirectory, "logo.png");
> container.Image(logo, 120);
> ```

---

## Checking File Existence

When the image file may not be present (e.g. optional branding), guard with a
file check and provide a text fallback:

```csharp
if (File.Exists(logoPath))
    container.Image(logoPath, 100);
else
    container.Text("CompanyName").Bold().FontSize(18);
```
