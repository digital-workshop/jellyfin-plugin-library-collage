# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

Jellyfin plugin that generates library cover images: a tilted (or grid) collage of the library's posters, darkened, with the library name drawn on top. Targets **Jellyfin 12.2 / .NET 10** only.

## Commands

```
dotnet build -c Release
dotnet test                                                    # also renders sample JPEGs into samples/ (gitignored)
dotnet test --filter "FullyQualifiedName~Render_ProducesFullHdJpeg"   # single test
```

On this machine `dotnet` may not be on the Bash PATH. Prefix with `export PATH="/c/Program Files/dotnet:$PATH";`.

To look at rendering changes, run the tests and open the images in `samples/`. Images under `docs/` are used by the README and copied in by hand from `samples/`.

## Branches and releasing

New work goes on `dev`; `main` is what stable users get. The workflow (`.github/workflows/release.yml`) runs on annotated tags. It tests, builds, zips only the plugin DLL, creates a GitHub release and prepends the version to the manifests on `main`:

- **Beta:** tag `vX.Y.Z.N` with N ≠ 0 on `dev`, e.g. `v1.0.10.1` (the last stable version plus a build number). This creates a pre-release and updates `manifest-beta.json` only. Testers add `.../main/manifest-beta.json` as a repository in Jellyfin.
- **Stable:** merge `dev` into `main`, then tag `vX.Y.Z` there. This updates `manifest.json` and `manifest-beta.json`. Its version (`X.Y.Z.0`) is higher than every beta before it, so testers move back to stable automatically.

Jellyfin only accepts four numeric version parts, so suffixes like `-beta` are not possible.

Only stable releases go into the *Changelog* section of `README.md`, with one entry that sums up everything since the last stable release. Betas are never listed there.

Two things follow from this:
- The bot commits to `main` after every release, so `git pull --rebase origin main` before pushing to `main`.
- The annotated tag's message becomes the changelog in the Jellyfin catalog.

`TARGET_ABI` in the workflow must match the Jellyfin server version.

## Architecture

- **`Rendering/CollageRenderer`** is pure SkiaSharp with no Jellyfin types. It takes poster file paths, a title and `CollageOptions`, and returns JPEG bytes. Anything purely visual (layouts, text styles, label box) lives here and is covered by the tests.
  - Fonts are embedded resources: Bebas Neue and Montserrat ExtraBold, both under OFL.
  - `GetCellCount` tells callers how many posters the layout uses.
- **`Services/CollageService`** is the Jellyfin side.
  - Picks posters per library: `SelectionMode`, item types derived from `CollectionType`.
  - Decides whether it may overwrite the current image (`ImageState`).
  - Renders the collage and writes it.
  - Used by the scheduled task (`Tasks/GenerateCollagesTask`, weekly) and by the API controller.
- **`Services/ManagedImageStore`** remembers path and SHA-256 of every image the plugin wrote, in `managed-images.json` in the plugin data folder.
  - It lives outside `PluginConfiguration` on purpose: the settings page posts the whole config back and would otherwise overwrite it with stale data.
  - An image that is not in this store counts as "Own image" and is only replaced when the per-library `ForceOverwrite` is set.
- **`Api/LibraryCollageController`** (admin only) serves the settings page: library list with status, preview (renders without saving), generate one library, queue the task.
- **`Configuration/configPage.html`** is the embedded settings page (plain JS, `ApiClient`/`Dashboard` globals). `EnableInMainMenu` puts it in the dashboard sidebar.
  - Per-library settings, including an optional `TextStyle` override, live in `PluginConfiguration.Libraries`.
  - A library missing from that list defaults to enabled when it is a movies library.

### SkiaSharp

The plugin references `SkiaSharp` 3.119.4 with `ExcludeAssets=runtime;native`, so at runtime it uses the server's copy. Plugins load in a `PluginLoadContext` that falls back to the host. Keep the version in sync with Jellyfin's `Directory.Packages.props`.

The test project references SkiaSharp plus `SkiaSharp.NativeAssets.Linux.NoDependencies`. Without the Linux natives, CI on ubuntu fails.

## Jellyfin 12.2 behaviour that shaped the code

- **Where the image is stored.** Jellyfin's `CollectionFolderImageProvider` regenerates library images that sit in the item's internal metadata folder on every library scan. Its date check never matches because of DB timestamp precision. The plugin therefore writes `poster.jpg` into the library's own folder (`folder.Path`, under `root/default/<Library>`) and sets the image path directly instead of calling `IProviderManager.SaveImage`. Jellyfin treats that file as stored with media and leaves it alone.
- **Taking back overwritten collages.** A PNG in the metadata folder for a library that has a store entry counts as Jellyfin's auto-image replacing an earlier collage. It is treated as `Managed`, so the plugin may replace it.
- **Dashboard in-memory copies.** Dashboard → Libraries reads `GetUserRootFolder().Children`, which can be separate in-memory instances of the same library. After writing, the plugin updates those instances too.
- **Stale images on Dashboard → Libraries.** That page requests images without a `tag`, so browsers show cached copies. When it looks stale, first check what the server actually returns: image endpoints need no auth, e.g. `curl http://<server>/Items/<id>/Images/Primary`.
