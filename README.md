# Library Collage for Jellyfin

Generates the cover images of your Jellyfin libraries automatically: a tilted collage of the library's posters, darkened, with the library name on top.

![Preview](docs/preview.jpg)

Requires **Jellyfin 12.2**.

## Installation

1. In Jellyfin open **Dashboard → Plugins → Repositories** and add:
   ```
   https://raw.githubusercontent.com/digital-workshop/jellyfin-plugin-library-collage/main/manifest.json
   ```
2. Install **Library Collage** from the catalog and restart Jellyfin.
3. Open the plugin settings, pick your libraries, and click **Save & generate now**.

## Features

- **Per-library selection.** Movie libraries are on by default.
- **Layouts:** tilted posters or a straight grid.
- **Poster selection:** random from the top rated (default), random, newest, or top rated.
- **Text styles:** Poster (condensed, upper case, default), Classic (bold, centered), or Netflix (bottom left with gradient). Long names shrink or wrap automatically. Text can be turned off.
- **Adjustable darkening** (default 50 %).
- **Hand-made images are safe.** A library with an image that the plugin did not create is skipped unless you tick *Overwrite anyway*.
- **Scheduled task** *Bibliotheks-Collagen erzeugen*, weekly by default. Change the schedule under **Dashboard → Scheduled Tasks**.
- **Preview** button per library, which shows the result without changing anything.

Small libraries repeat their posters to fill the collage. Libraries with fewer posters than the configured minimum (default 4) are skipped.

## Releasing

Push a tag such as `v1.0.0`. The *Release* workflow tests and builds the plugin, creates a GitHub release with the zip, and adds the version to `manifest.json`.

## Building locally

```
dotnet build -c Release
dotnet test
```

The tests render sample images into `samples/`.

## License

GPL-3.0. Bundled fonts: Bebas Neue and Montserrat (SIL Open Font License, see `Jellyfin.Plugin.LibraryCollage/Rendering/Fonts`).
