using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GridGame.Core.Battle;
using UnityEngine;
using UnityEngine.UIElements;

namespace GridGame.App
{
    /// <summary>Smokebomb on the board (§7, GG-T861, GG-T1916, the Black Ninja): a whoosh and a grey burst of smoke billow out
    /// of the ninja's tile with a "Vanish" callout and its token goes with it (off the board until its next turn; the turn's
    /// timeline entry carries the marker). When that turn comes a smaller burst and a pale flash mark where it reappears,
    /// timed to its teleport fading it back in. The smoke cloud itself is the region tile wash its RegionTilesEvent paints.
    /// Reduce Motion keeps one fading puff.</summary>
    public sealed partial class BattleScreen
    {
        private static readonly Color SmokeColor = Palette.Vfx.Smoke;

        private IEnumerator VanishedAnim(VanishedEvent e)
        {
            App.Audio.Sfx("battle.whoosh", 0.9f, 0.8f);
            Text?.Show(e.From, CombatTextKind.Callout, "Vanish", 0.2f, Color.Lerp(SmokeColor, Palette.White, 0.35f));
            if (board.Units.TryGetValue(e.UnitId, out var v) && !v.Dead) App.StartCoroutine(FadeOffBoard(v));
            yield return SmokeBurst(e.From, 1.1f);
        }

        /// <summary>GG-T1916: the vanished unit's token leaves the board inside the burst (the TeleportedEvent of its
        /// reappearance brings it back, as for a burrower).</summary>
        private IEnumerator FadeOffBoard(UnitView v)
        {
            yield return Tween.Fade(v.Root, 1, 0, Timing.Quick);
            v.Root.RemoveFromHierarchy();
            v.Dead = true;
            SyncVanishedMarker();
        }

        private IEnumerator ReappearedAnim(ReappearedEvent e)
        {
            vanishedShown.Remove(e.UnitId);
            SyncVanishedMarker();
            App.StartCoroutine(ReappearFlash(e.To));
            yield return SmokeBurst(e.To, 0.7f);
        }

        /// <summary>Billowing puffs of <see cref="SmokeColor"/> drift out of <paramref name="tile"/>, swell and thin, round a
        /// dense core; <paramref name="scale"/> of a tile and a bit of reach.</summary>
        private IEnumerator SmokeBurst(GridGame.Core.Coord tile, float scale)
        {
            Vector2 at = BoardView.CenterOf(tile);
            float reach = BoardView.Tile * scale;
            if (ReduceMotion)
            {
                yield return Tween.Run(Timing.Quick, k =>
                {
                    board.Lines.ClearLines();
                    board.Lines.Dot(at, reach * 0.6f, SmokeColor.WithA(0.7f * (1f - k)));
                });
                board.Lines.ClearLines();
                yield break;
            }
            const int puffs = 9;
            var dirs = new Vector2[puffs];
            var sizes = new float[puffs];
            for (int i = 0; i < puffs; i++)
            {
                float th = i * Mathf.PI * 2f / puffs + Random.Range(-0.3f, 0.3f);
                dirs[i] = new Vector2(Mathf.Cos(th), Mathf.Sin(th)) * Random.Range(0.6f, 1f);
                sizes[i] = Random.Range(0.25f, 0.38f) * reach;
            }
            yield return Tween.Run(Timing.Standard, k =>
            {
                board.Lines.ClearLines();
                float out_ = Ease.Out(k);
                // Billowing puffs drift out, swell and thin; a dense core hides the ninja as it goes.
                for (int i = 0; i < puffs; i++)
                    board.Lines.Dot(at + dirs[i] * reach * out_, sizes[i] * (0.6f + 0.6f * out_),
                        Color.Lerp(SmokeColor, Palette.White, 0.15f * (i % 3)).WithA(0.75f * (1f - k)));
                board.Lines.Dot(at, reach * (0.32f + 0.23f * out_), SmokeColor.WithA(0.9f * (1f - k * k)));
            });
            board.Lines.ClearLines();
        }

        /// <summary>The flash where the ninja reappears, timed to its teleport fading it back in.</summary>
        private IEnumerator ReappearFlash(GridGame.Core.Coord to)
        {
            yield return Tween.Wait(Timing.Quick);
            Vector2 at = BoardView.CenterOf(to);
            App.Audio.Sfx("battle.whoosh", 0.6f, 1.4f);
            yield return Tween.Run(Timing.Standard, k =>
            {
                board.Lines.Rings.Clear();
                board.Lines.Ring(at, Mathf.Lerp(8f, BoardView.Tile * 0.7f, Ease.Out(k)), Palette.White.WithA(0.85f * (1f - k)), 6f - 4f * k);
                board.Lines.Dot(at, 14f * (1f - k) + 4f, Palette.White.WithA(0.8f * (1f - k)));
            });
            board.Lines.Rings.Clear(); board.Lines.MarkDirtyRepaint();
        }

        // ── GG-T2160: the vanished marker. Under PartyWindow (the default) heroes have no timeline entry to carry the
        // Vanished pip, so a hero in the smoke shows here instead: a chip in the tray beside "+N resting", its thumbnail
        // under the smoke pip, until the next party window brings it back. It never moves, so Reduce Motion changes nothing. ──
        private VisualElement vanishedChip, vanishedFaces;
        /// <summary>Heroes the marker shows, in the order they vanished: added when the token leaves the board, dropped when
        /// the reappearance plays.</summary>
        private readonly List<int> vanishedShown = new List<int>();
        private string vanishedKey = "";

        private void BuildVanishedMarker(VisualElement tray)
        {
            vanishedShown.Clear();
            vanishedKey = "";
            vanishedChip = Ui.Chip("", tray, onClick: TapVanishedMarker, name: "tl-vanished");
            vanishedChip.style.marginLeft = vanishedChip.style.marginTop = vanishedChip.style.marginBottom = Space.S0;
            vanishedChip.style.marginRight = Space.S8;
            vanishedChip.style.paddingLeft = vanishedChip.style.paddingRight = Space.S16;
            vanishedChip.style.flexShrink = 0;
            vanishedChip.style.borderTopColor = vanishedChip.style.borderBottomColor = vanishedChip.style.borderLeftColor = vanishedChip.style.borderRightColor = SmokeColor;
            vanishedChip.Q<Label>().style.display = DisplayStyle.None;
            vanishedFaces = new VisualElement { name = "tl-vanished-faces", pickingMode = PickingMode.Ignore };
            vanishedFaces.style.flexDirection = FlexDirection.Row;
            vanishedChip.Add(vanishedFaces);
            vanishedChip.style.display = DisplayStyle.None;
        }

        /// <summary>Brings the marker up to date: a hero joins once its token has left the board (or, on a rebuilt screen, while
        /// the state has it vanished and no token shows); one that died or came back without its beat leaves. Hidden under
        /// HeroTimeline, where the hero's own turn entry wears the pip.</summary>
        private void SyncVanishedMarker()
        {
            if (vanishedChip == null || State == null) return;
            foreach (var u in State.Units)
                if (u.Vanished && u.IsHero && u.Hp > 0 && !vanishedShown.Contains(u.Id)
                    && (!board.Units.TryGetValue(u.Id, out var v) || v.Dead))
                    vanishedShown.Add(u.Id);
            vanishedShown.RemoveAll(id => !(State.UnitById(id) is Unit u) || u.Hp <= 0
                || (!u.Vanished && board.Units.TryGetValue(id, out var v) && !v.Dead));
            var ids = TurnRules.HeroesOnTimeline(State) ? new List<int>() : vanishedShown;
            string key = string.Join(",", ids);
            if (key == vanishedKey) return;
            vanishedKey = key;
            vanishedFaces.Clear();
            for (int i = 0; i < ids.Count; i++)
            {
                var u = State.UnitById(ids[i]);
                var face = Ui.Thumb(u.Def.Id, vanishedFaces, Size.IconM);
                face.name = "tl-vanished-" + u.Id;
                face.pickingMode = PickingMode.Ignore;
                if (i > 0) face.style.marginLeft = -Size.IconM / 3f;
            }
            if (ids.Count > 0)
            {
                var pip = Ui.StatusPip("vanished", "S", SmokeColor, Size.IconInline, vanishedFaces);
                pip.name = "tl-vanished-pip";
                pip.style.position = Position.Absolute;
                pip.style.right = -Space.S8; pip.style.bottom = -Space.S4;
            }
            vanishedChip.style.display = ids.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            SyncTrayHost();
        }

        /// <summary>A tap on the marker: in Examine mode its card fills the panel; otherwise it opens as a tap card, as the
        /// resting chip does.</summary>
        private void TapVanishedMarker()
        {
            if (State == null) return;
            if (!examining) { InspectCard.Show(vanishedChip, VanishedCard); return; }
            ClearTapArrow();
            examineNote = null;
            examineCard = VanishedCard;
            App.Audio.Sfx("ui.click");
            SyncPanel();
        }

        /// <summary>Where the vanished heroes are and when they come back, one row per hero with its thumbnail.</summary>
        private bool VanishedCard(VisualElement body)
        {
            if (State == null) return false;
            var heroes = vanishedShown.Select(id => State.UnitById(id)).Where(u => u != null && u.Hp > 0).ToList();
            if (heroes.Count == 0) return false;
            var head = Ui.Text(PanelCopy(PanelState.Vanished), "small gold", body);
            head.name = "vanished-head";
            head.style.whiteSpace = WhiteSpace.Normal;
            head.style.marginBottom = Space.S8;
            foreach (var u in heroes)
            {
                var row = Ui.El("row", body);
                row.name = "vanished-unit-" + u.Id;
                row.style.alignItems = Align.Center;
                row.style.marginBottom = Space.S8;
                var t = Ui.Thumb(u.Def.Id, row, Size.IconS);
                t.pickingMode = PickingMode.Ignore;
                t.style.marginRight = Space.S8;
                var line = Ui.Text(PanelCopy(PanelState.VanishedRow, u.Name), "small", row);
                line.style.whiteSpace = WhiteSpace.Normal;
                line.style.flexShrink = 1;
            }
            return true;
        }
    }
}
