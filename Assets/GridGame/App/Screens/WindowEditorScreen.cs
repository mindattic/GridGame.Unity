using System;
using System.Collections.Generic;
using System.Linq;
using GridGame.Meta;
using UnityEngine;
using UnityEngine.UIElements;

namespace GridGame.App
{
    /// <summary>
    /// Settings → Windows (GG-T407; owner 2026-11-17: "make the gradient totally editable just like the FF and chrono
    /// trigger games ... super flexible and easily modifiable with many different themes to choose from"). A live preview
    /// window up top, then four tabs: Presets (every themes.json preset and the profile's own), Colors (gradient mode, the
    /// four corner colours or 2-4 stops with angle/centre, each colour on R/G/B sliders with a hex readout, grain),
    /// Texture (overlay, opacity, blend, scale), Frame (style, 1-3 frame colours, accent, text) and Tone (Brightness,
    /// Contrast, Gamma for the device's display, GG-T563: kept at once, per theme, on this device). Every change applies to
    /// the whole UI at once; Save keeps it as a named custom theme in settings.json, Reset returns to what was picked,
    /// Delete removes a custom one. Leaving with unsaved changes puts the saved theme back.
    /// </summary>
    public sealed class WindowEditorScreen : HubPage
    {
        public override string Id => "windows";
        protected override string Title => "Windows";

        private static readonly string[] TabNames = { "Presets", "Colors", "Texture", "Frame", "Tone" };
        private static readonly string[] CornerNames = { "Top left", "Top right", "Bottom left", "Bottom right" };

        private WindowThemeDef draft;
        private string baseName;
        private bool dirty, pending;
        private int tab, sel, frameSel, previewTab;
        private float savedScroll;
        private ScrollView list;
        private TextField nameField;
        private string typedName;
        private readonly List<Action> live = new List<Action>();

        private SettingsSave St => App.Settings;
        private bool BaseIsCustom => St.CustomThemes.Any(t => t.Name == baseName);

        protected override void BuildBody(VisualElement body)
        {
            if (draft == null)
            {
                var cur = WindowThemes.Resolve(St.MenuTheme);
                draft = cur.Clone();
                baseName = cur.Name;
            }
            live.Clear();
            BuildPreview(body);

            Ui.Tabs(TabNames, tab, i => { tab = i; sel = 0; savedScroll = 0; Rebuild(); }, body).name = "window-tabs";
            // Takes every pixel the preview and the footer leave, at any aspect (a kit list panel, GG-T454).
            var c = Ui.ListPanel(body, null, out list, "window-editor");
            switch (tab)
            {
                case 0: BuildPresets(c); break;
                case 1: BuildColors(c); break;
                case 2: BuildTexture(c); break;
                case 3: BuildFrame(c); break;
                default: BuildTone(c); break;
            }
            float restore = savedScroll;
            list.RegisterCallback<GeometryChangedEvent>(_ => { if (restore > 0) { list.scrollOffset = new Vector2(0, restore); restore = 0; } });

            var foot = Ui.Footer(body, "window-actions-bar");
            foot.name = "window-actions";
            foot.style.justifyContent = Justify.FlexEnd;
            Ui.Pad(foot, left: Space.S16, right: Space.S16);
            var del = RowIcon.IconButton("Delete", RowKind.Close, Delete, "btn--danger", foot);
            del.name = "window-delete";
            Ui.El("", foot).style.flexGrow = 1;
            var reset = RowIcon.IconButton("Reset", RowKind.NewGamePlus, Reset, null, foot);
            reset.name = "window-reset";
            Ui.SetEnabled(reset, dirty);
            var save = RowIcon.IconButton("Save", RowKind.Save, Save, "btn--primary", foot); // GG-T751 (§26.7)
            save.name = "window-save";
            Ui.SetEnabled(del, BaseIsCustom);
        }

        public override void OnHidden()
        {
            // Unsaved edits don't outlive the editor: the saved theme comes back.
            if (tonePendingSave) { tonePendingSave = false; App.SaveSettings(); }
            if (dirty) { dirty = false; App.ApplySettings(); App.Toast("Unsaved window changes discarded"); }
        }

        private void Rebuild()
        {
            if (list != null) savedScroll = list.scrollOffset.y;
            Refresh();
        }

        // ── Live apply ──────────────────────────────────────────────────────────────

        /// <summary>The draft changed: repaint the whole UI with it (at most once a frame) and update the readouts.</summary>
        private void Changed()
        {
            dirty = true;
            if (pending || Root == null) return;
            pending = true;
            Root.schedule.Execute(() =>
            {
                pending = false;
                Art.ApplyTheme(draft);
                Ui.ApplyMenuTheme(App.Document != null ? App.Document.rootVisualElement : null);
                foreach (var a in live.ToList()) a();
            });
        }

        /// <summary>A structural change (mode, stop count, texture, style): apply it and rebuild the editor.</summary>
        private void ChangedAndRebuild()
        {
            dirty = true;
            Art.ApplyTheme(draft);
            Ui.ApplyMenuTheme(App.Document != null ? App.Document.rootVisualElement : null);
            Rebuild();
        }

        // ── Preview ─────────────────────────────────────────────────────────────────

        private void BuildPreview(VisualElement body)
        {
            var box = Ui.El("col", body);
            box.name = "window-preview";
            Ui.Tabs(new[] { "Items", "Magic", "Equip" }, previewTab, i => { previewTab = i; Rebuild(); }, box);
            var w = Ui.Window($"{draft.Name}{(dirty ? " *" : "")}", box);
            var line = Ui.Text("Sample text: the quick brown fox. ", "small", w);
            line.style.whiteSpace = WhiteSpace.Normal;
            var stats = Ui.El("row", w);
            Ui.Text("HP 340/340", "small grow", stats);
            Ui.Text("1,250 G", "small gold", stats);
            Ui.Text("A dim caption line", "small dim", w);
            var row = Ui.El("row", w);
            row.style.justifyContent = Justify.FlexEnd;
            row.style.marginTop = Space.S8;
            RowIcon.IconButton("Cancel", RowKind.Close, null, null, row); // the sample dialog wears the real one's icons (GG-T751)
            RowIcon.IconButton("Confirm", RowKind.Confirm, null, "btn--primary", row);
        }

        // ── Presets ─────────────────────────────────────────────────────────────────

        private void BuildPresets(VisualElement c)
        {
            Ui.Section("Presets", c);
            Grid(c, WindowThemes.Presets.Select(WindowThemes.Tuned), "preset");
            Ui.Section(St.CustomThemes.Count > 0 ? $"Yours ({St.CustomThemes.Count}/{WindowThemeCatalog.MaxCustom})" : "Yours: edit any theme, then Save", c);
            if (St.CustomThemes.Count > 0) Grid(c, St.CustomThemes, "custom");

            var row = Ui.El("row", c);
            row.style.marginTop = Space.S16;
            row.style.alignItems = Align.Center;
            // A rebuild (any structural edit, a resize) keeps what the player typed.
            nameField = new TextField { name = "window-name-field", maxLength = WindowThemeCatalog.MaxNameLength, value = typedName ?? NextName() };
            nameField.RegisterValueChangedCallback(e => typedName = e.newValue);
            nameField.style.flexGrow = 1; nameField.style.flexShrink = 1;
            nameField.style.fontSize = TypeScale.Base;
            nameField.style.minHeight = Size.Target;
            nameField.style.marginRight = Space.TargetHalfGap;
            var input = nameField.Q(className: "unity-text-field__input") ?? nameField.Q(className: "unity-base-text-field__input");
            if (input != null)
            {
                input.style.backgroundColor = Palette.Ink.WithA(0.9f);
                input.style.color = Palette.White;
                input.style.paddingLeft = input.style.paddingRight = Space.S16;
            }
            row.Add(nameField);
            RowIcon.IconButton("Save as new", RowKind.Save, () => SaveAsNew(nameField.value), null, row).name = "window-save-new";
        }

        /// <summary>A wrapping grid of theme tiles: each shows its gradient, texture and frame, named over it.</summary>
        private void Grid(VisualElement c, IEnumerable<WindowThemeDef> themes, string kind)
        {
            int cols = Fit.Roomy ? 4 : 3;
            var grid = Ui.El("row", c);
            grid.style.flexWrap = Wrap.Wrap;
            grid.style.paddingLeft = grid.style.paddingRight = Space.TargetHalfGap;
            foreach (var t in themes)
            {
                var cell = Ui.El(null, grid);
                cell.style.width = Length.Percent(100f / cols);
                cell.style.paddingLeft = cell.style.paddingRight = Space.TargetHalfGap;
                cell.style.paddingTop = cell.style.paddingBottom = Space.TargetHalfGap;
                var tile = Tile(t, cell, t.Name == baseName);
                tile.name = $"{kind}-{t.Name.ToLowerInvariant().Replace(' ', '-')}";
                var theme = t;
                Ui.Tappable(tile, () => Pick(theme), "ui.select");
            }
        }

        private static VisualElement Tile(WindowThemeDef t, VisualElement parent, bool selected)
        {
            var tile = Ui.El("center", parent);
            tile.style.height = Size.Cell;
            tile.style.backgroundImage = new StyleBackground(WindowThemes.Swatch(t));
            var sides = WindowThemes.FrameSides(t, WindowThemes.DerivedAccent(t)) ?? new[] { Palette.Text, Palette.Text, Palette.TextDim, Palette.TextDim };
            float bw = selected ? Size.Shard : Size.Bar;
            tile.style.borderTopWidth = tile.style.borderBottomWidth = tile.style.borderLeftWidth = tile.style.borderRightWidth = bw;
            var gold = Palette.Neutral;
            tile.style.borderTopColor = selected ? gold : sides[0];
            tile.style.borderLeftColor = selected ? gold : sides[1];
            tile.style.borderRightColor = selected ? gold : sides[2];
            tile.style.borderBottomColor = selected ? gold : sides[3];
            tile.style.borderTopLeftRadius = tile.style.borderTopRightRadius = tile.style.borderBottomLeftRadius = tile.style.borderBottomRightRadius = Space.S16;
            WindowThemes.DressWith(tile, t);
            var l = Ui.Text(t.Name, "small", tile);
            l.style.unityTextAlign = TextAnchor.MiddleCenter;
            l.style.whiteSpace = WhiteSpace.Normal;
            l.style.alignSelf = Align.Stretch; // wraps against the tile's width, not its own shrink-wrapped one
            // A dark plate under white text: the swatch behind the name runs from near-white to saturated, so no one text colour clears 4.5:1 on all of them.
            l.style.color = Palette.White;
            l.style.backgroundColor = Palette.Ink.WithA(0.8f);
            l.style.paddingLeft = l.style.paddingRight = Space.S16;
            l.style.borderTopLeftRadius = l.style.borderTopRightRadius = l.style.borderBottomLeftRadius = l.style.borderBottomRightRadius = Space.S16;
            return tile;
        }

        private void Pick(WindowThemeDef t)
        {
            draft = WindowThemes.Resolve(t.Name).Clone();
            baseName = t.Name;
            dirty = false;
            St.MenuTheme = t.Name;
            App.ApplySettings(); App.SaveSettings();
            Rebuild();
        }

        private string NextName()
        {
            for (int i = 1; ; i++)
            {
                string n = "Custom " + i;
                if (!WindowThemes.IsPreset(n) && !St.CustomThemes.Any(t => string.Equals(t.Name, n, StringComparison.OrdinalIgnoreCase))) return n;
            }
        }

        private void Save()
        {
            if (BaseIsCustom)
            {
                var copy = draft.Clone();
                copy.Name = baseName;
                if (!Valid(copy)) return;
                int i = St.CustomThemes.FindIndex(t => t.Name == baseName);
                St.CustomThemes[i] = copy;
                Commit(copy, $"Saved {copy.Name}");
            }
            else SaveAsNew(NextName());
        }

        private void SaveAsNew(string name)
        {
            name = (name ?? "").Trim();
            if (name.Length == 0) name = NextName();
            if (WindowThemes.IsPreset(name)) { App.Toast($"{name} is a preset; pick another name"); App.Audio.Sfx("ui.error"); return; }
            if (St.CustomThemes.Any(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase))) { App.Toast($"You already have a theme named {name}"); App.Audio.Sfx("ui.error"); return; }
            if (St.CustomThemes.Count >= WindowThemeCatalog.MaxCustom) { App.Toast($"All {WindowThemeCatalog.MaxCustom} slots are full: delete one first"); App.Audio.Sfx("ui.error"); return; }
            var copy = draft.Clone();
            copy.Name = name;
            if (!Valid(copy)) return;
            St.CustomThemes.Add(copy);
            typedName = null; // the next save offers a fresh name
            Commit(copy, $"Saved {name}");
        }

        private bool Valid(WindowThemeDef t)
        {
            var p = WindowThemeCatalog.Validate(t);
            if (p.Count == 0) return true;
            App.Toast("Can't save: " + p[0]);
            App.Audio.Sfx("ui.error");
            return false;
        }

        private void Commit(WindowThemeDef t, string toast)
        {
            draft = t.Clone();
            baseName = t.Name;
            dirty = false;
            St.MenuTheme = t.Name;
            App.ApplySettings(); App.SaveSettings();
            App.Toast(toast);
            App.Audio.Sfx("ui.confirm");
            Rebuild();
        }

        private void Reset()
        {
            var t = WindowThemes.Resolve(baseName);
            draft = t.Clone();
            dirty = false;
            Art.ApplyTheme(draft);
            Ui.ApplyMenuTheme(App.Document != null ? App.Document.rootVisualElement : null);
            Rebuild();
        }

        private void Delete()
        {
            if (!BaseIsCustom) return;
            string gone = baseName;
            St.CustomThemes.RemoveAll(t => t.Name == gone);
            var t0 = WindowThemes.Resolve(WindowThemes.DefaultName);
            draft = t0.Clone(); baseName = t0.Name; dirty = false;
            St.MenuTheme = t0.Name;
            App.ApplySettings(); App.SaveSettings();
            App.Toast($"Deleted {gone}");
            Rebuild();
        }

        // ── Colors ──────────────────────────────────────────────────────────────────

        /// <summary>An option row (one that cycles a setting): named for automation, led by the Settings emblem (GG-T751, §26.7).</summary>
        private static void Option(VisualElement row, string name) { row.name = name; row.KindIcon(RowKind.Settings); }

        private void BuildColors(VisualElement c)
        {
            var g = draft.Gradient;
            Option(Ui.MenuRow("Gradient", ModeLabel(g.Mode), () => { CycleMode(); ChangedAndRebuild(); }, c), "window-mode");
            if (g.Mode == WindowThemeCatalog.Corners)
            {
                // Four chips laid out like the corners they colour (FF7's window config).
                var grid = Ui.El("row", c);
                grid.style.flexWrap = Wrap.Wrap;
                grid.style.paddingLeft = grid.style.paddingRight = Space.TargetHalfGap;
                for (int i = 0; i < 4; i++)
                {
                    int k = i;
                    Chip(grid, CornerNames[i], () => g.Corners[k], 2, sel == i, () => { sel = k; Rebuild(); }, "window-corner-" + i);
                }
                sel = Mathf.Clamp(sel, 0, 3);
                ColorEditor(c, CornerNames[sel], () => g.Corners[sel], v => g.Corners[sel] = v);
            }
            else
            {
                if (g.Mode == WindowThemeCatalog.Linear)
                    Slider(c, "Angle", "window-angle", 0, 359, Mathf.RoundToInt(g.Angle), v => $"{v}°", v => { g.Angle = v; Changed(); });
                else
                {
                    g.Center ??= new[] { 0.5f, 0.5f };
                    Slider(c, "Center X", "window-center-x", 0, 100, Mathf.RoundToInt(g.Center[0] * 100), v => $"{v}%", v => { g.Center[0] = v / 100f; Changed(); });
                    Slider(c, "Center Y", "window-center-y", 0, 100, Mathf.RoundToInt(g.Center[1] * 100), v => $"{v}%", v => { g.Center[1] = v / 100f; Changed(); });
                }
                Option(Ui.MenuRow("Stops", g.Stops.Count.ToString(), () => { CycleStops(); ChangedAndRebuild(); }, c), "window-stops");
                var row = Ui.El("row", c);
                row.style.flexWrap = Wrap.Wrap;
                row.style.paddingLeft = row.style.paddingRight = Space.TargetHalfGap;
                sel = Mathf.Clamp(sel, 0, g.Stops.Count - 1);
                for (int i = 0; i < g.Stops.Count; i++)
                {
                    int k = i;
                    Chip(row, $"Stop {i + 1}", () => g.Stops[k].Color, g.Stops.Count, sel == i, () => { sel = k; Rebuild(); }, "window-stop-" + i);
                }
                var s = g.Stops[sel];
                int lo = sel == 0 ? 0 : Mathf.CeilToInt(g.Stops[sel - 1].At * 100), hi = sel == g.Stops.Count - 1 ? 100 : Mathf.FloorToInt(g.Stops[sel + 1].At * 100);
                Slider(c, "Position", "window-stop-at", lo, Math.Max(lo, hi), Mathf.RoundToInt(s.At * 100), v => $"{v}%", v => { s.At = v / 100f; Changed(); });
                ColorEditor(c, $"Stop {sel + 1}", () => s.Color, v => s.Color = v);
            }
            Slider(c, "Grain", "window-grain", 0, 30, Mathf.RoundToInt(g.Grain * 100), v => $"{v}%", v => { g.Grain = v / 100f; Changed(); });
        }

        private static string ModeLabel(string m) => m == WindowThemeCatalog.Corners ? "4 corners" : m == WindowThemeCatalog.Radial ? "Radial" : "Linear";

        private void CycleMode()
        {
            var g = draft.Gradient;
            sel = 0;
            if (g.Mode == WindowThemeCatalog.Linear)
            {
                g.Mode = WindowThemeCatalog.Radial;
                g.Center ??= new[] { 0.5f, 0.5f };
            }
            else if (g.Mode == WindowThemeCatalog.Radial)
            {
                // Seed the corners from the stops: first at the top left, last at the bottom right, the middle between.
                var st = g.Stops;
                string a = st[0].Color, z = st[st.Count - 1].Color;
                string m1 = st.Count > 2 ? st[1].Color : Mix(a, z), m2 = st.Count > 3 ? st[2].Color : m1;
                g.Corners = new[] { a, m1, m2, z };
                g.Mode = WindowThemeCatalog.Corners;
            }
            else
            {
                var k = g.Corners;
                g.Stops = new List<WindowStopDef> { new WindowStopDef(0f, k[0]), new WindowStopDef(0.5f, Mix(k[1], k[2])), new WindowStopDef(1f, k[3]) };
                g.Mode = WindowThemeCatalog.Linear;
            }
            g.Stops ??= new List<WindowStopDef> { new WindowStopDef(0f, "#8696A6"), new WindowStopDef(1f, "#242E3C") };
        }

        private void CycleStops()
        {
            var st = draft.Gradient.Stops;
            if (st.Count >= WindowThemeCatalog.MaxStops)
            {
                // Back to two: keep the ends.
                draft.Gradient.Stops = new List<WindowStopDef> { st[0], st[st.Count - 1] };
                sel = 0;
                return;
            }
            // A new stop in the middle of the widest gap, in the colour already there.
            int gap = 1;
            for (int i = 1; i < st.Count; i++) if (st[i].At - st[i - 1].At > st[gap].At - st[gap - 1].At) gap = i;
            var s = new WindowStopDef((st[gap].At + st[gap - 1].At) / 2f, Mix(st[gap - 1].Color, st[gap].Color));
            st.Insert(gap, s);
            sel = gap;
        }

        private static string Mix(string a, string b) =>
            WindowThemes.HexOf(Color.Lerp(WindowThemes.Hex(a, Palette.Gray), WindowThemes.Hex(b, Palette.Gray), 0.5f));

        // ── Texture ─────────────────────────────────────────────────────────────────

        private void BuildTexture(VisualElement c)
        {
            var ids = new List<string> { null };
            ids.AddRange(WindowThemes.TextureIds);
            int cols = Fit.Roomy ? 4 : 3;
            var grid = Ui.El("row", c);
            grid.style.flexWrap = Wrap.Wrap;
            grid.style.paddingLeft = grid.style.paddingRight = Space.TargetHalfGap;
            var x = draft.Texture;
            foreach (var id in ids)
            {
                var cell = Ui.El(null, grid);
                cell.style.width = Length.Percent(100f / cols);
                cell.style.paddingLeft = cell.style.paddingRight = cell.style.paddingTop = cell.style.paddingBottom = Space.TargetHalfGap;
                // Each choice previews this theme's gradient wearing that texture.
                var probe = draft.Clone();
                probe.Name = WindowThemes.TextureLabel(id);
                probe.Texture = id == null ? null : new WindowTextureDef
                {
                    Id = id, Opacity = x != null ? Mathf.Max(0.3f, x.Opacity) : DefaultOpacity(id), Blend = x?.Blend ?? DefaultBlend(id), Scale = x?.Scale ?? 1f,
                };
                var tile = Tile(probe, cell, (x?.Id) == id);
                tile.name = "window-tex-" + (id ?? "none").Replace(':', '-');
                string pick = id;
                Ui.Tappable(tile, () =>
                {
                    if (pick == null) draft.Texture = null;
                    else if (draft.Texture == null) draft.Texture = new WindowTextureDef { Id = pick, Opacity = DefaultOpacity(pick), Blend = DefaultBlend(pick), Scale = 1f };
                    else draft.Texture.Id = pick;
                    ChangedAndRebuild();
                }, "ui.select");
            }
            if (x == null) return;
            Slider(c, "Opacity", "window-tex-opacity", 0, 100, Mathf.RoundToInt(x.Opacity * 100), v => $"{v}%", v => { x.Opacity = v / 100f; Changed(); });
            Option(Ui.MenuRow("Blend", BlendLabel(x.Blend), () =>
            {
                int i = Array.IndexOf(WindowThemeCatalog.Blends, x.Blend);
                x.Blend = WindowThemeCatalog.Blends[(i + 1) % WindowThemeCatalog.Blends.Length];
                ChangedAndRebuild();
            }, c), "window-tex-blend");
            Slider(c, "Scale", "window-tex-scale", 25, 400, Mathf.RoundToInt(x.Scale * 100), v => $"{v}%", v => { x.Scale = v / 100f; Changed(); });
        }

        /// <summary>The blend each texture reads best in when first picked: stars glow (screen), lines and weave shade
        /// (multiply), the rest push both ways (overlay).</summary>
        private static string DefaultBlend(string id) => id == "stars" ? "screen" : id == "scanlines" ? "multiply" : id != null && id.StartsWith(WindowThemeCatalog.FilePrefix) ? "normal" : "overlay";
        private static float DefaultOpacity(string id) => id == "stars" ? 0.9f : 0.5f;
        private static string BlendLabel(string b) => string.IsNullOrEmpty(b) ? "Overlay" : char.ToUpperInvariant(b[0]) + b.Substring(1);

        // ── Frame ───────────────────────────────────────────────────────────────────

        private static readonly string[] FrameColorNames1 = { "Frame" }, FrameColorNames2 = { "Light edge", "Dark edge" }, FrameColorNames3 = { "Light edge", "Sides", "Dark edge" };

        private void BuildFrame(VisualElement c)
        {
            var f = draft.Frame ??= new WindowFrameDef();
            Option(Ui.MenuRow("Frame style", StyleLabel(f.Style), () =>
            {
                int i = Array.IndexOf(WindowThemeCatalog.FrameStyles, f.Style);
                f.Style = WindowThemeCatalog.FrameStyles[(i + 1) % WindowThemeCatalog.FrameStyles.Length];
                ChangedAndRebuild();
            }, c), "window-frame-style");
            int n = f.Colors?.Length ?? 0;
            Option(Ui.MenuRow("Frame colours", n == 0 ? "Style's own" : n == 1 ? "Solid" : n == 2 ? "Bevel" : "Bevel, 3 tones", () =>
            {
                f.Colors = n == 0 ? new[] { "#FFFFFF" } : n == 1 ? new[] { "#F2F2F6", "#6A6A72" } : n == 2 ? new[] { f.Colors[0], "#B0B0B8", f.Colors[1] } : null;
                frameSel = 0;
                ChangedAndRebuild();
            }, c), "window-frame-colors");
            Option(Ui.MenuRow("Accent", draft.Accent == null ? "From the gradient" : "Custom", () =>
            {
                draft.Accent = draft.Accent == null ? WindowThemes.HexOf(WindowThemes.DerivedAccent(draft)) : null;
                ChangedAndRebuild();
            }, c), "window-accent");
            Option(Ui.MenuRow("Text colour", draft.Text == null ? "Default" : "Custom", () =>
            {
                draft.Text = draft.Text == null ? "#FFFFFF" : null;
                ChangedAndRebuild();
            }, c), "window-text");

            // The colours this frame has, as chips; the selected one goes on the sliders.
            var targets = new List<(string label, Func<string> get, Action<string> set)>();
            var names = n == 1 ? FrameColorNames1 : n == 2 ? FrameColorNames2 : FrameColorNames3;
            for (int i = 0; i < n; i++) { int k = i; targets.Add((names[i], () => f.Colors[k], v => f.Colors[k] = v)); }
            if (draft.Accent != null) targets.Add(("Accent", () => draft.Accent, v => draft.Accent = v));
            if (draft.Text != null) targets.Add(("Text", () => draft.Text, v => draft.Text = v));
            if (targets.Count == 0) return;
            frameSel = Mathf.Clamp(frameSel, 0, targets.Count - 1);
            var row = Ui.El("row", c);
            row.style.flexWrap = Wrap.Wrap;
            row.style.paddingLeft = row.style.paddingRight = Space.TargetHalfGap;
            for (int i = 0; i < targets.Count; i++)
            {
                int k = i;
                Chip(row, targets[i].label, targets[i].get, Math.Min(3, targets.Count), frameSel == i, () => { frameSel = k; Rebuild(); }, "window-frame-color-" + i);
            }
            var t = targets[frameSel];
            ColorEditor(c, t.label, t.get, t.set);
        }

        private static string StyleLabel(string s) => string.IsNullOrEmpty(s) ? "Classic" : char.ToUpperInvariant(s[0]) + s.Substring(1);

        // ── Tone (GG-T563) ──────────────────────────────────────────────────────────

        private const int ToneSteps = 100, ToneMid = ToneSteps / 2;

        /// <summary>Brightness, Contrast and Gamma for this device's display (owner 2026-11-21: "adjusted based on the
        /// devices on quirks when it comes to nits"). Each bar starts at 0.5 (the theme as authored, marked at the centre)
        /// and goes either way. Kept at once, apart from Save: a preset's goes in settings.json's tone map (themes.json stays
        /// pristine), a custom theme's into the theme itself.</summary>
        private void BuildTone(VisualElement c)
        {
            Ui.Section("Display", c);
            string Fmt(int v) => (v / (float)ToneSteps).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
            int Pos(float v) => Mathf.RoundToInt(v * ToneSteps);
            var tone = draft.Tone;
            Slider(c, "Brightness", "window-brightness", 0, ToneSteps, Pos(tone.Brightness), Fmt, v => SetTone(b: v / (float)ToneSteps), mark: ToneMid);
            Slider(c, "Contrast", "window-contrast", 0, ToneSteps, Pos(tone.Contrast), Fmt, v => SetTone(k: v / (float)ToneSteps), mark: ToneMid);
            Slider(c, "Gamma", "window-gamma", 0, ToneSteps, Pos(tone.Gamma), Fmt, v => SetTone(g: v / (float)ToneSteps), mark: ToneMid);
            var row = Ui.El("row", c);
            row.style.justifyContent = Justify.FlexEnd;
            row.style.marginTop = Space.S8;
            row.style.paddingRight = Space.S16;
            var reset = RowIcon.IconButton("Reset", RowKind.NewGamePlus, () => { SetTone(WindowTone.Neutral, WindowTone.Neutral, WindowTone.Neutral); Rebuild(); }, null, row);
            reset.name = "window-tone-reset";
            void ShowReset() => Ui.SetEnabled(reset, !draft.Tone.IsNeutral);
            ShowReset();
            live.Add(ShowReset);
        }

        private bool tonePendingSave;

        /// <summary>Moves the draft's tone (any value left null stays) and keeps it for the theme it came from, then repaints
        /// the UI live. Not an edit to Save: it doesn't mark the theme changed.</summary>
        private void SetTone(float? b = null, float? k = null, float? g = null)
        {
            var t = draft.Tone;
            var tone = new WindowTone(b ?? t.Brightness, k ?? t.Contrast, g ?? t.Gamma);
            draft.Tone = tone;
            int i = St.CustomThemes.FindIndex(x => x.Name == baseName);
            if (i >= 0) St.CustomThemes[i].Tone = tone;
            else if (tone.IsNeutral) St.ThemeTones.Remove(baseName);
            else St.ThemeTones[baseName] = WindowToneDef.From(tone);
            if (Root == null) return;
            if (!pending)
            {
                pending = true;
                Root.schedule.Execute(() =>
                {
                    pending = false;
                    Art.ApplyTheme(draft);
                    Ui.ApplyMenuTheme(App.Document != null ? App.Document.rootVisualElement : null);
                    foreach (var a in live.ToList()) a();
                });
            }
            // settings.json once the bar settles, not on every pointer move.
            if (!tonePendingSave)
            {
                tonePendingSave = true;
                Root.schedule.Execute(() => { tonePendingSave = false; App.SaveSettings(); }).ExecuteLater(400);
            }
        }

        // ── Widgets ─────────────────────────────────────────────────────────────────

        /// <summary>A colour chip: filled with its colour, its name and hex over it; gold-framed when selected.</summary>
        private void Chip(VisualElement parent, string label, Func<string> get, int perRow, bool selected, Action onTap, string name)
        {
            var cell = Ui.El(null, parent);
            cell.style.width = Length.Percent(100f / Mathf.Max(1, perRow));
            cell.style.paddingLeft = cell.style.paddingRight = cell.style.paddingTop = cell.style.paddingBottom = Space.TargetHalfGap;
            var chip = Ui.El("center", cell);
            chip.name = name;
            chip.style.minHeight = Size.Target;
            float bw = selected ? Size.Shard : Size.Hairline;
            chip.style.borderTopWidth = chip.style.borderBottomWidth = chip.style.borderLeftWidth = chip.style.borderRightWidth = bw;
            var edge = selected ? Palette.Neutral : Palette.Text;
            chip.style.borderTopColor = chip.style.borderBottomColor = chip.style.borderLeftColor = chip.style.borderRightColor = edge;
            chip.style.borderTopLeftRadius = chip.style.borderTopRightRadius = chip.style.borderBottomLeftRadius = chip.style.borderBottomRightRadius = Space.S16;
            var l = Ui.Text(label, "small", chip);
            l.style.unityTextAlign = TextAnchor.MiddleCenter;
            l.style.whiteSpace = WhiteSpace.Normal;
            l.style.alignSelf = Align.Stretch; // wraps against the tile's width, not its own shrink-wrapped one
            var hex = Ui.Text("", "small", chip);
            void Show() { chip.style.backgroundColor = WindowThemes.Hex(get(), Palette.Gray); hex.text = get(); }
            Show();
            live.Add(Show);
            Ui.Tappable(chip, onTap, "ui.select");
        }

        /// <summary>FF-style colour config: a swatch with its hex, then R, G and B sliders 0-255.</summary>
        private void ColorEditor(VisualElement c, string label, Func<string> get, Action<string> set)
        {
            var head = Ui.El("row", c);
            head.style.alignItems = Align.Center;
            head.style.marginTop = Space.S8;
            head.style.paddingLeft = Space.S16;
            Ui.Text(label, "menu-label grow", head);
            var hex = Ui.Text("", "h2 gold", head);
            hex.name = "window-hex";
            var sw = Ui.El(null, head);
            sw.style.width = Size.Target; sw.style.height = Size.Plate;
            sw.style.marginLeft = Space.S16;
            sw.style.borderTopWidth = sw.style.borderBottomWidth = sw.style.borderLeftWidth = sw.style.borderRightWidth = Size.Hairline;
            sw.style.borderTopColor = sw.style.borderBottomColor = sw.style.borderLeftColor = sw.style.borderRightColor = Palette.Text;
            sw.style.borderTopLeftRadius = sw.style.borderTopRightRadius = sw.style.borderBottomLeftRadius = sw.style.borderBottomRightRadius = Space.S8;
            void Show() { hex.text = get(); sw.style.backgroundColor = WindowThemes.Hex(get(), Palette.Gray); }
            Show();
            live.Add(Show);
            int Channel(int shift) => WindowThemeCatalog.TryHex(get(), out int v) ? (v >> shift) & 0xFF : 0;
            void SetChannel(int shift, int value)
            {
                WindowThemeCatalog.TryHex(get(), out int v);
                v = (v & ~(0xFF << shift)) | ((value & 0xFF) << shift);
                set(WindowThemeCatalog.Hex((v >> 16) & 0xFF, (v >> 8) & 0xFF, v & 0xFF));
                Changed();
            }
            Slider(c, "R", "window-r", 0, 255, Channel(16), v => v.ToString(), v => SetChannel(16, v), Palette.Enemy);
            Slider(c, "G", "window-g", 0, 255, Channel(8), v => v.ToString(), v => SetChannel(8, v), Palette.Good);
            Slider(c, "B", "window-b", 0, 255, Channel(0), v => v.ToString(), v => SetChannel(0, v), Palette.Hero);
        }

        /// <summary>A labelled slider: − and + buttons (one step) either side of a track that follows a drag or a tap.
        /// <paramref name="mark"/> draws a tick at that value (the neutral centre of a two-way bar, GG-T563).</summary>
        private void Slider(VisualElement c, string label, string name, int min, int max, int value, Func<int, string> fmt, Action<int> onChange, Color? tint = null, int? mark = null)
        {
            var row = Ui.El("row", c);
            row.name = name;
            row.style.alignItems = Align.Center;
            row.style.minHeight = Size.Target;
            row.style.paddingLeft = Space.S16;
            var l = Ui.Text(label, "small", row);
            l.style.width = label.Length <= 1 ? Size.Icon : Size.Label;
            l.style.flexShrink = 0;
            int cur = Mathf.Clamp(value, min, max);
            var valueLabel = Ui.Text(fmt(cur), "small gold", null);
            VisualElement fill = null;
            void Set(int v, bool notify)
            {
                v = Mathf.Clamp(v, min, max);
                if (v == cur && notify) return;
                cur = v;
                valueLabel.text = fmt(v);
                fill.style.width = Length.Percent(max > min ? (v - min) * 100f / (max - min) : 100f);
                if (notify) onChange(v);
            }
            var minus = Ui.Button("−", () => Set(cur - 1, true), "btn--small btn--icon", row);
            minus.name = name + "-down";
            var track = Ui.El(null, row);
            track.name = name + "-track";
            track.style.flexGrow = 1; track.style.flexShrink = 1;
            track.style.height = Size.Target;
            track.style.justifyContent = Justify.Center;
            track.style.marginLeft = track.style.marginRight = Space.TargetHalfGap;
            var bar = Ui.El(null, track);
            bar.pickingMode = PickingMode.Ignore;
            bar.style.height = Size.Crack;
            bar.style.backgroundColor = Palette.Ink.WithA(0.85f);
            bar.style.borderTopLeftRadius = bar.style.borderTopRightRadius = bar.style.borderBottomLeftRadius = bar.style.borderBottomRightRadius = Size.Bar;
            bar.style.overflow = Overflow.Hidden;
            fill = Ui.El(null, bar);
            fill.pickingMode = PickingMode.Ignore;
            fill.style.height = Length.Percent(100);
            fill.style.backgroundColor = tint ?? Palette.Neutral;
            if (mark.HasValue && max > min)
            {
                var tick = Ui.El(null, track);
                tick.name = name + "-mark";
                tick.pickingMode = PickingMode.Ignore;
                tick.style.position = Position.Absolute;
                tick.style.left = Length.Percent((Mathf.Clamp(mark.Value, min, max) - min) * 100f / (max - min));
                tick.style.top = tick.style.bottom = Space.S8;
                tick.style.width = Size.Hairline;
                tick.style.marginLeft = -Size.Hairline / 2f;
                tick.style.backgroundColor = Palette.Text;
            }
            void FromPointer(Vector2 local)
            {
                float w = track.contentRect.width;
                if (w <= 0) return;
                Set(Mathf.RoundToInt(min + Mathf.Clamp01(local.x / w) * (max - min)), true);
            }
            track.RegisterCallback<PointerDownEvent>(e => { track.CapturePointer(e.pointerId); FromPointer(e.localPosition); e.StopPropagation(); });
            track.RegisterCallback<PointerMoveEvent>(e => { if (track.HasPointerCapture(e.pointerId)) { FromPointer(e.localPosition); e.StopPropagation(); } });
            track.RegisterCallback<PointerUpEvent>(e => { if (track.HasPointerCapture(e.pointerId)) track.ReleasePointer(e.pointerId); });
            var plus = Ui.Button("+", () => Set(cur + 1, true), "btn--small btn--icon", row);
            plus.name = name + "-up";
            row.Add(valueLabel);
            valueLabel.style.width = Size.Label;
            valueLabel.style.flexShrink = 0;
            valueLabel.style.unityTextAlign = TextAnchor.MiddleRight;
            Set(cur, false);
        }
    }
}
