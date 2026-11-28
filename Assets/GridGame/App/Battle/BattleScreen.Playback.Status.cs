using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GridGame.Core;
using GridGame.Core.Battle;
using GridGame.Core.Content;
using GridGame.Meta;
using MindAttic.Media.Capture;
using MindAttic.Media.Capture.Unity;
using MindAttic.Telemetry.Unity;
using UnityEngine;
using UnityEngine.UIElements;

namespace GridGame.App
{
    public sealed partial class BattleScreen
    {
        // ── Billboard (FF VI-style spell window) ──

        private VisualElement billboard;

        public int BillboardLines => billboard?.childCount ?? 0;

        /// <summary>Strokes on the pincer/outline layer right now (automation: the pause harness pauses mid-chain).</summary>
        public int LineStrokes => board == null ? 0 : board.Lines.Segs.Count + board.Lines.Outlines.Count + board.Lines.Rings.Count + board.Lines.ShapeList.Count;

        /// <summary>A top-of-board window naming every spell, ability and combo as it happens; lines fade after 1.6 s.</summary>
        /// <summary>The billboard steps out of the way: when the action is in the top two rows it moves to the board's foot.</summary>
        // ── Where a unit is NOW in playback (GG-T98) ──
        // State is the end of the whole command by the time anything plays, so a beat must never read a unit's
        // position from it: a unit that moved later in the same command (an enemy turn, knockback, a chain link)
        // is already on its final tile there. The board view's UnitView.Tile advances one event at a time
        // (StepAnim, Displaced, Teleport, spawns), so it is the position as of the beat being played.

        private Coord ViewTile(Unit u) => board.Units.TryGetValue(u.Id, out var v) ? v.Tile : u.Pos;

        private Vector2 ViewCenter(Unit u) =>
            board.Units.TryGetValue(u.Id, out var v) ? BoardView.CenterOf(v.Tile, v.W, v.H) : BoardView.CenterOf(u.Pos, u.W, u.H);

        private IEnumerable<Coord> ViewTiles(Unit u) => FootprintTiles(new UnitFootprint { UnitId = u.Id, At = ViewTile(u), W = u.W, H = u.H });

        private static IEnumerable<Coord> FootprintTiles(UnitFootprint f)
        {
            for (int y = 0; y < Mathf.Max(1, f.H); y++)
                for (int x = 0; x < Mathf.Max(1, f.W); x++)
                    yield return new Coord(f.At.X + x, f.At.Y + y);
        }

        /// <summary>The billboard no longer sits over the board (GG-T405), so there is nothing on the board to dodge.</summary>
        private void DodgeBillboard(IEnumerable<Coord> tiles) => PlaceBillboard();

        /// <summary>Most lines the announcement overlay holds at once (§9.0, GG-T1893): one, so a newer line replaces the older
        /// and the overlay never stacks over the top board row's units and HP bars.</summary>
        private const int BillboardMaxLines = 1;

        /// <summary>The overlay's window opacity (GG-T1893): the top row's units still show through the line.</summary>
        private const float BillboardOpacity = 0.8f;

        /// <summary>Marks a billboard row that came from ShowBanner, so an empty banner takes only those down.</summary>
        private const string BannerRowClass = "billboard__banner";

        /// <summary>The billboard is the announcement overlay (§9.0, GG-T1824): it hangs from the board's top edge in Overlay
        /// coordinates, across the content column, never over the timeline, one line tall (the newest
        /// replacing, <see cref="BillboardMaxLines"/>), fading on its own; the battle log keeps them all.</summary>
        private void PlaceBillboard()
        {
            if (billboard == null) return;
            Rect r = bannerViewport != null ? bannerViewport.worldBound : new Rect(float.NaN, 0, 0, 0);
            // Before the first layout pass the band's worldBound is all zeros: a billboard shown at battle start (a wanderer or
            // recruit callout) then landed at y = 0 over the title bar. Fall back to the band's own row until it is laid out (GG-T409).
            bool laid = !float.IsNaN(r.y) && r.height > 0f;
            billboard.style.top = laid ? r.y - App.Stage.worldBound.y : BannerTop;
            billboard.style.left = billboard.style.right = Fit.Inset + Fit.Edge;
            billboard.style.height = StyleKeyword.Auto;
        }

        /// <summary>Takes ShowBanner's lines off the overlay at once (an empty banner: the drag left the pincer, control
        /// came back); the billboard's own lines keep their 1.6 s.</summary>
        private void ClearBannerLines()
        {
            if (billboard == null) return;
            foreach (var row in BillboardRows(billboard))
                if (row.ClassListContains(BannerRowClass)) row.RemoveFromHierarchy();
            if (BillboardRows(billboard).Count == 0) billboard.style.display = DisplayStyle.None;
        }

        private void Billboard(string text, Texture2D icon, Color color, bool banner = false)
        {
            if (string.IsNullOrEmpty(text)) return;
            // The same banner again (a drag preview re-saying "PINCER!" on each step) keeps its line up instead of stacking.
            if (banner && billboard != null && billboard.style.display.value != DisplayStyle.None)
            {
                var shown = BillboardRows(billboard);
                var last = shown.Count > 0 ? shown[shown.Count - 1] : null;
                if (last != null && last.ClassListContains(BannerRowClass) && last.Q<Label>()?.text == text)
                {
                    var again = new object();
                    last.userData = again;
                    last.style.opacity = 1f;
                    App.StartCoroutine(RetireBillboardRow(billboard, last, again));
                    return;
                }
            }
            NoteShown(text);
            if (billboard == null)
            {
                billboard = Ui.Window(null, App.Overlay, "window--compact");
                billboard.name = "billboard";
                billboard.pickingMode = PickingMode.Ignore;
                billboard.style.position = Position.Absolute;
                billboard.style.paddingTop = billboard.style.paddingBottom = Space.S0;
                billboard.style.marginTop = billboard.style.marginBottom = billboard.style.marginLeft = billboard.style.marginRight = Space.S0;
                billboard.style.alignItems = Align.Center;
                billboard.style.justifyContent = Justify.FlexEnd;
                billboard.style.overflow = Overflow.Hidden;
                billboard.style.opacity = BillboardOpacity;
            }
            billboard.style.display = DisplayStyle.Flex;
            PlaceBillboard();
            billboard.schedule.Execute(PlaceBillboard); // again once the band has its real layout (GG-T409)
            billboard.BringToFront();
            var row = Ui.El("row", billboard);
            row.pickingMode = PickingMode.Ignore;
            row.style.height = BannerViewportHeight;
            if (banner) row.AddToClassList(BannerRowClass);
            var token = new object();
            row.userData = token;
            // The line spans the band and centres, so it wraps only against the band's full width (GG-T405).
            row.style.alignSelf = Align.Stretch; row.style.justifyContent = Justify.Center; row.style.alignItems = Align.Center;
            if (icon != null)
            {
                var ic = Ui.El("", row);
                ic.style.width = ic.style.height = Size.IconM; ic.style.marginRight = Space.S16;
                ic.style.backgroundImage = new StyleBackground(icon);
                ic.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
            }
            var l = Ui.Text(text, "h2 shadow", row);
            l.style.color = color;
            // GG-T448 (owner: "no wrapping on the announcement box, make sure the font is sized to fit the message"):
            // one line, never two — the font shrinks to fit instead of wrapping. It starts at the one GUI size (GG-T770);
            // the GeometryChangedEvent below measures the actual unwrapped width against the row and, only when it
            // overflows, keeps shrinking (down to BillboardFontFloor) until it fits.
            l.style.fontSize = TypeScale.Base;
            l.style.whiteSpace = WhiteSpace.NoWrap;
            l.style.flexShrink = 1;
            bool icon0 = icon != null;
            l.RegisterCallback<GeometryChangedEvent>(_ => ShrinkBillboardLineToFit(l, row, icon0));
            var rows = BillboardRows(billboard);
            for (int i = 0; i < rows.Count - BillboardMaxLines; i++) rows[i].RemoveFromHierarchy();
            App.StartCoroutine(Tween.Pop(row, 1.12f, 0.12f));
            App.StartCoroutine(RetireBillboardRow(billboard, row, token));
        }

        /// <summary>The smallest a billboard line is ever allowed to shrink to (GG-T448): below this a one-line
        /// announcement is unreadable, so a message that still doesn't fit here belongs to a source string that
        /// should be shortened, not a smaller font.</summary>
        private static float BillboardFontFloor => TypeFloor.ShrinkFloor;

        /// <summary>
        /// GG-T448 (owner: "no wrapping on the announcement box, make sure the font is sized to fit the message, and
        /// fix any excessively long messages to fit in the announcement box"): measures the line's own natural,
        /// unwrapped width at its current font size against the row it has to fit in (minus the icon's own width,
        /// when there is one) and scales the font down proportionally — repeatedly, as each shrink reflows and fires
        /// another GeometryChangedEvent — until it fits or hits <see cref="BillboardFontFloor"/>. Never grows back:
        /// the line only gets smaller as it settles, so it can't oscillate.
        /// </summary>
        private static void ShrinkBillboardLineToFit(Label l, VisualElement row, bool hasIcon)
        {
            if (l.panel == null) return; // RetireBillboardRow already removed it (a late-settling layout event)
            float rowWidth = row.resolvedStyle.width;
            if (float.IsNaN(rowWidth) || rowWidth <= 1f) return;
            float available = rowWidth - (hasIcon ? Size.IconM + Space.S16 : 0f) - Space.S16; // a little breathing room
            if (available <= 1f) return;
            float natural = l.MeasureTextSize(l.text ?? "", 0, VisualElement.MeasureMode.Undefined, 0, VisualElement.MeasureMode.Undefined).x;
            if (natural <= available + 0.5f) return;
            float size = Mathf.Max(BillboardFontFloor, l.resolvedStyle.fontSize * (available / natural));
            if (size < l.resolvedStyle.fontSize - 0.5f) l.style.fontSize = size;
        }

        /// <summary>The billboard's message rows, not the theme's texture layer WindowThemes.Dress puts first (GG-T687: with
        /// Manapunk's wear texture that layer kept childCount above 0, so an emptied billboard stayed up as a bare pane).</summary>
        private static List<VisualElement> BillboardRows(VisualElement bb) =>
            bb.Children().Where(c => !c.ClassListContains(WindowThemes.OverlayClass)).ToList();

        /// <summary>Drops a billboard line after 1.6 s of game time — held while paused, unlike a scheduler delay (GG-T126).
        /// Takes the billboard itself: OnHidden may null the field before this fires.</summary>
        private IEnumerator RetireBillboardRow(VisualElement bb, VisualElement row, object token)
        {
            yield return Tween.Wait(Timing.Cinematic + Timing.Emphasis);
            if (row.userData != token) yield break; // a repeat kept it up and retires it itself
            yield return Tween.Run(Timing.Quick, k => { if (row.userData == token) row.style.opacity = 1f - k; });
            if (row.userData != token) yield break;
            row.RemoveFromHierarchy();
            if (BillboardRows(bb).Count == 0) bb.style.display = DisplayStyle.None;
        }

        /// <summary>Gold shown on the HUD: the party's purse plus what this battle has dropped so far.</summary>
        private int shownCoins = -1;

        public int CoinsShown => shownCoins;

        private void SyncCoins(int? value = null)
        {
            shownCoins = value ?? ((App.Save?.Gold ?? 0) + State.Results.Coins);
            coinLabel.text = $"{shownCoins} G";
        }

        /// <summary>
        /// The enemy "explodes" into gold (as in GridGame2026): coins burst out and bounce, then fly to the gold counter
        /// in the top-right, which counts up as each one lands. Runs alongside the next beats; never blocks play.
        /// </summary>
        private IEnumerator CoinBurst(Vector2 boardAt, int coins)
        {
            int n = Mathf.Clamp(coins, 3, ReduceMotion ? 4 : 12);
            int start = shownCoins < 0 ? (App.Save?.Gold ?? 0) + State.Results.Coins - coins : shownCoins;
            Vector2 from = App.Overlay.WorldToLocal(board.Root.LocalToWorld(boardAt));
            var wb = coinLabel.worldBound;
            Vector2 to = App.Overlay.WorldToLocal(new Vector2(wb.xMax - 40f, wb.center.y));
            int landed = 0;
            for (int i = 0; i < n; i++)
            {
                var coin = new VisualElement { name = "coin", pickingMode = PickingMode.Ignore };
                coin.style.position = Position.Absolute;
                coin.style.width = coin.style.height = Size.Pip;
                coin.style.backgroundImage = new StyleBackground(Art.Orb);
                coin.style.unityBackgroundImageTintColor = new Color(1f, 0.82f, 0.25f);
                App.Overlay.Add(coin);
                float ang = Random.Range(-Mathf.PI * 0.95f, -Mathf.PI * 0.05f), speed = Random.Range(260f, 520f);
                var vel = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * speed;
                float floor = from.y + Random.Range(30f, 70f), delay = i * 0.035f;
                int share = coins / n + (i < coins % n ? 1 : 0);
                App.StartCoroutine(OneCoin(coin, from, vel, floor, to, delay, () =>
                {
                    landed++;
                    SyncCoins(Mathf.Min(start + coins, shownCoins + share));
                    if (landed == n) SyncCoins();
                    App.Audio.Sfx("ui.buy", 0.35f, 1.1f + landed * 0.03f);
                    App.StartCoroutine(Tween.Pop(coinLabel, 1.15f, 0.08f));
                }));
            }
            yield break;
        }

        private IEnumerator OneCoin(VisualElement coin, Vector2 from, Vector2 vel, float floor, Vector2 to, float delay, System.Action arrive)
        {
            yield return Tween.Wait(delay);
            // Burst: ballistic arc with one bounce, ~0.45 s.
            var pos = from; float t = 0f; bool bounced = false;
            while (t < 0.45f)
            {
                float dt = Mathf.Min(Tween.Dt * Tween.Speed, 0.05f);
                t += dt;
                vel.y += 1800f * dt;
                pos += vel * dt;
                if (pos.y > floor && vel.y > 0f && !bounced) { pos.y = floor; vel.y *= -0.45f; vel.x *= 0.7f; bounced = true; }
                coin.style.left = pos.x - 17; coin.style.top = pos.y - 17;
                yield return null;
            }
            // Fly to the counter, accelerating, shrinking a little.
            var start = pos;
            yield return Tween.Run(Timing.Emphasis, k =>
            {
                float e = k * k;
                var p = Vector2.Lerp(start, to, e) + Vector2.up * (-80f * Ease.Arc(k));
                coin.style.left = p.x - 17; coin.style.top = p.y - 17;
                coin.style.scale = new Scale(Vector2.one * (1f - 0.35f * k));
            });
            coin.RemoveFromHierarchy();
            arrive();
        }

        // ── Shared helpers ────────────────────────────────────────────────────────

        private void RefreshUnit(int id)
        {
            var u = State.UnitById(id);
            if (u != null && board.Units.TryGetValue(id, out var v))
            {
                v.SetPlate(u.Facing, u.PlateHp, u.PlateMax);
                v.SetBuffs(u, Colorblind, State.TurnEntryOf(u.Id)?.StunTicks > 0);
                v.SetHp(u.Hp, u.MaxHp);
                if (!u.HasBuff("frozen")) v.Art.style.unityBackgroundImageTintColor = Palette.White; // the frost tint thaws with the status
            }
        }

        /// <summary>A status's pip glyph (its Letter, else its name's first letter), as its pip on the unit shows it.</summary>
        private static string StatusGlyph(BuffDef def) => string.IsNullOrEmpty(def.Letter) ? def.Name.Substring(0, 1) : def.Letter;

        /// <summary>A status's own colour (<c>BuffDef.Color</c>), as InflictAnim draws it.</summary>
        private static Color StatusColor(BuffDef def) => Art.HexColor(def.Color, def.Kind == BuffKind.Buff ? Palette.Mana : new Color(0.8f, 0.4f, 0.9f));

        /// <summary>GG-T830: a status on its last turn (or last tick) ticks a "1" countdown in its colour beside its glyph.</summary>
        private void StatusCountdownText(BuffTickedEvent bt)
        {
            if (bt.Remaining != 1 || !board.Units.TryGetValue(bt.UnitId, out var v) || v.Dead) return;
            var def = App.Content.TryBuff(bt.BuffId);
            if (def != null) Text.Countdown(BoardView.CenterOf(v.Tile, v.W, v.H), bt.Remaining, StatusColor(def), StatusGlyph(def));
        }

        private readonly Dictionary<int, int> lastCountdown = new Dictionary<int, int>();

        /// <summary>GG-T918 (§2.10): when a foe's countdown (its turn, or its pending cast landing) reaches 3, 2, 1 or 0 it
        /// floats once over it, Timer digits then a TimerEnd flash. Read through <see cref="TurnRules.CountdownLeft"/>.</summary>
        private void CastCountdownTexts()
        {
            foreach (var u in State.Units)
            {
                if (u.IsHero || !u.Alive || !board.Units.TryGetValue(u.Id, out var v) || v.Dead) { lastCountdown.Remove(u.Id); continue; }
                int n = TurnRules.CountdownLeft(State, u.Id);
                if (n < 0 || n > 3) { lastCountdown.Remove(u.Id); continue; }
                if (lastCountdown.TryGetValue(u.Id, out int was) && was == n) continue;
                lastCountdown[u.Id] = n;
                Text.Countdown(BoardView.CenterOf(v.Tile, v.W, v.H), n);
            }
        }

        /// <summary>GG-T830: a status running out, cleansed, cured or broken fades out struck through over its bearer.</summary>
        private void StatusGoneText(BuffRemovedEvent br)
        {
            var u = State.UnitById(br.UnitId);
            if (u == null || !u.Alive || !board.Units.TryGetValue(br.UnitId, out var v) || v.Dead) return;
            var def = App.Content.TryBuff(br.BuffId);
            if (def != null) Text.Expired(BoardView.CenterOf(v.Tile, v.W, v.H), def.Name, StatusColor(def));
        }

        private IEnumerator FloatAway(Label l, float seconds)
        {
            float top = l.resolvedStyle.top;
            if (float.IsNaN(top)) top = 0;
            var start = l.style.top.value.value;
            yield return Tween.Run(seconds, k =>
            {
                l.style.top = start - 90f * Tween.EaseOut(k);
                l.style.opacity = k < 0.6f ? 1f : 1f - (k - 0.6f) / 0.4f;
                float s = k < 0.15f ? 0.6f + k / 0.15f * 0.5f : 1.1f - 0.1f * Mathf.Min(1, (k - 0.15f) * 4);
                l.style.scale = new Scale(Vector2.one * s);
            });
            l.RemoveFromHierarchy();
        }
    }
}
