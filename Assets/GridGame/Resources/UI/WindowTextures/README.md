# Window textures (GG-T407)

Drop a tileable PNG here (your own or public-domain/CC0 art only, no third-party assets of unclear origin) and it shows
up in Settings → Windows → Texture as its file name. A theme in `../themes.json` names it as `"id": "file:<name>"`
(no extension). The tile repeats over each window at one texel per design px times the theme's `scale`; the blend modes
other than `normal` use its luminance (mid-gray = no change for overlay/soft-light, white for multiply, black for screen).
The built-in textures are generated in code (`App/UI/WindowThemes.cs`), so nothing here is required.
