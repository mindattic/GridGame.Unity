using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GridGame.Core;
using GridGame.Core.Battle;
using GridGame.Core.Bot;
using GridGame.Core.Content;
using GridGame.Core.Rules;
using GridGame.Meta;
using MindAttic.Media.Capture;
using MindAttic.Media.Capture.Unity;
using MindAttic.Telemetry;
using MindAttic.Telemetry.Unity;
using UnityEngine;
using UnityEngine.UIElements;

namespace GridGame.App
{
    public sealed partial class BattleScreen
    {
        public override bool RebuildOnResize => false; // ApplyStageLayout re-lays the HUD out in place

        // Layout (design pixels). Vertical positions are fixed from the top; horizontal ones follow the live stage
        // width (bible §26.3) so a wider phone centres the board and stretches the timeline instead of leaving a gap.
        // §9.0 (GG-T1822, GG-T2118): fixed rows, nothing stacked between them: the top bar, the board a gutter under it, the
        // timeline under the board (the thumb zone, GG-T2118), the party mana strip, the action row, the description panel.
        private const float BoardTop = TopBarTop + TopBarHeight + Space.S8;
        private static float BoardLeft => (GameApp.StageWidth - BoardView.Tile * BattleConfig.Cols) / 2f;
        // The timeline's geometry lives with it in BattleScreen.Timeline.cs (GG-T287).

        /// <summary>The description panel's page (§9.0): the selected unit, its skills and the ability/confirm card draw here
        /// (a ScrollView, so Add/Clear reach its content).</summary>
        private VisualElement panel;
        /// <summary>Where the announcement overlay sits (§9.0, GG-T1824): no row of its own, an empty anchor on the board's
        /// top edge that the billboard reads its place from; it takes no input.</summary>
        private VisualElement bannerViewport;
        // Row heights (§9.0, GG-T1894): the top bar hugs the safe top and every gap between rows is tightened, so the description
        // panel gets the height back (the board keeps its 180-px tiles). GG-T1921: the action row's three buttons are one tap
        // target tall, so the description panel takes the rest of the five slots' row.
        private const float TopBarTop = Space.S8, TopBarHeight = 110f, BarHeight = Size.Target;
        private const float BannerTop = BoardTop + Space.S8;
        /// <summary>One announcement line's height; the billboard stacks up to <see cref="BillboardMaxLines"/> of them.</summary>
        private const float BannerViewportHeight = Size.Target - Space.S24;
        // The HUD rows under the timeline, top to bottom, a gutter apart (§9.0): the party mana strip and the action row, then
        // the description panel, which stretches to the safe bottom and so alone absorbs a taller stage (§26.3).
        private const float BoardBottom = BoardTop + BoardView.Tile * BattleConfig.Rows;
        private static float BarTop => TimelineBottom + Space.S24; // 24 px from the timeline's diamonds, also targets (GG-T656, POL-C3)
        /// <summary>The thin party mana strip (GG-T1921) sits in the gap between the timeline and the action row, full width.</summary>
        private static float ManaStripTop => TimelineBottom + Space.S8;
        private const float ManaStripHeight = Size.Bar;
        // GG-T706 (owner 2026-11-23): the ability bar is always the row under the board. GG-T523's side columns beside the
        // board on a 2:3 / 3:4 stage are gone: those shapes are now letterboxed at 9:16 (bible §26.3), so no stage is wide
        // enough for them.
        /// <summary>The description panel starts 16 px under the action row (GG-T1894).</summary>
        private static float DescriptionTop => BarTop + BarHeight + Space.S16;
        private Label waveLabel, coinLabel, timerLabel;
        private VisualElement bossBox, bossHpFill;
        private Label stageName, bossName;
        private int bossBarId = -1;
        private float bossBarFraction = -1f;

        private VisualElement shake;
        private VisualElement boardHost;
        /// <summary>Stage-sized layer inside the board, under board.Lines, that holds cut-in cards (GG-T128).</summary>
        private VisualElement cutInLayer;

        // ─────────────────────────────────────────────────────────────────────────
        // HUD construction
        // ─────────────────────────────────────────────────────────────────────────

        private void BuildTopBar(VisualElement root)
        {
            var bar = Ui.El("row", root);
            bar.style.position = Position.Absolute;
            bar.style.left = Fit.Edge; bar.style.right = Fit.Edge; bar.style.top = TopBarTop; bar.style.height = TopBarHeight;
            RowIcon.HamburgerButton(OpenMenu, bar); // GG-T754: icon-only pause
            // §9.0 row 1: the stage name and "Wave n/m" small on one line between the hamburger and the gold; the name
            // shrinks to the floor, then scrolls (GG-T1143), the wave never does.
            // GG-T1943: under that line, in the same row, the turn strip (BuildTurnStrip), so it costs the board no height.
            var col = Ui.El("col grow", bar);
            col.style.alignItems = Align.Stretch; col.style.justifyContent = Justify.Center;
            col.style.minWidth = Space.S0;
            col.style.marginLeft = col.style.marginRight = Space.S16;
            var mid = Ui.El("row", col);
            mid.style.alignItems = Align.Center; mid.style.justifyContent = Justify.Center;
            mid.style.minWidth = Space.S0;
            stageName = Ui.Marquee(Ui.Text(State.Stage.Name, "small shadow", mid), grow: false);
            stageName.style.flexShrink = 1; stageName.style.minWidth = Space.S0; stageName.style.unityTextAlign = TextAnchor.MiddleCenter;
            var sep = Ui.Text("·", "small shadow", mid);
            sep.style.flexShrink = 0; sep.style.marginLeft = sep.style.marginRight = Space.S8;
            // Full-colour, not .dim: the LivingBackdrop's drifting shards pass behind the top bar, and dim grey "1/5"
            // vanished against a pale shard so the label read as just "Wave" (GG-T666).
            waveLabel = Ui.Text("", "small shadow", mid);
            waveLabel.style.flexShrink = 0;
            // GG-T1952 (§9.0): during a boss wave the stage name gives way to the boss's name over a thin HP bar, so the boss
            // costs no row of its own.
            bossBox = Ui.El("col", mid);
            bossBox.name = "boss-bar";
            bossBox.style.flexShrink = 1; bossBox.style.flexGrow = 1; bossBox.style.minWidth = Space.S0;
            bossBox.style.justifyContent = Justify.Center;
            bossBox.style.display = DisplayStyle.None;
            bossBox.PlaceBehind(sep);
            bossName = Ui.Marquee(Ui.Text("", "small shadow", bossBox), grow: false);
            bossName.style.unityTextAlign = TextAnchor.MiddleCenter;
            bossName.style.color = Palette.TeamBoss;
            var gauge = Ui.El("gauge", bossBox);
            gauge.style.height = Size.Bar + Space.S4; gauge.style.marginTop = Space.S4;
            bossHpFill = Ui.El("gauge-fill", gauge);
            bossBarId = -1;
            coinLabel = Ui.Text("", "h2 gold shadow", bar);
            coinLabel.style.width = Fit.W(17f); coinLabel.style.unityTextAlign = TextAnchor.MiddleRight;
            BuildTurnStrip(col);
        }

        /// <summary>The turn strip (GG-T1943, UX review §1.4 #2): whose turn it is and the heroes who can still act.</summary>
        private VisualElement turnStrip, turnHeroes;
        private Label turnState;
        private string turnStripKey;

        /// <summary>GG-T1943 (§9.0 row 1): one line under the stage name, on its own dark plate: YOUR TURN, CLOCK RUNNING, ENEMY
        /// TURN or TOO LITTLE TIME, then a thumbnail for each hero who can still act (thumbnails, so no name is ever cut).
        /// It polls the state on a short beat and redraws only when what it says changes; nothing on it moves.</summary>
        private void BuildTurnStrip(VisualElement parent)
        {
            turnStrip = Ui.El("row", parent);
            turnStrip.name = "turn-strip";
            turnStrip.pickingMode = PickingMode.Ignore;
            turnStrip.style.alignSelf = Align.Center;
            turnStrip.style.height = Size.IconS;
            turnStrip.style.marginTop = Space.S4;
            turnStrip.style.backgroundColor = Palette.Ink.WithA(0.6f);
            Ui.Pad(turnStrip, Space.S16, Space.S0, Space.S16, Space.S0);
            turnStrip.style.borderTopLeftRadius = turnStrip.style.borderTopRightRadius =
                turnStrip.style.borderBottomLeftRadius = turnStrip.style.borderBottomRightRadius = Space.S8;
            turnState = Ui.Text("", "small shadow", turnStrip);
            turnState.name = "turn-state";
            turnState.pickingMode = PickingMode.Ignore;
            turnState.style.flexShrink = 1; turnState.style.minWidth = Space.S0;
            turnHeroes = Ui.El("row", turnStrip);
            turnHeroes.name = "turn-heroes";
            turnHeroes.pickingMode = PickingMode.Ignore;
            turnStripKey = null;
            turnStrip.schedule.Execute(SyncTurnStrip).Every(100);
            SyncTurnStrip();
        }

        /// <summary>GG-T1943: fills the turn strip from the state. The rules are the state's own (TurnRules via BattleEngine);
        /// the clock runs while a hero is held, or while a hero-timeline turn's own seconds tick (TickHeroTurn), never untimed.</summary>
        private void SyncTurnStrip()
        {
            if (turnStrip == null || State == null) return;
            string state;
            Color color;
            var who = new List<Unit>();
            var actor = playing && actingEntry >= 0 ? State.UnitById(State.Timeline.FirstOrDefault(e => e.Id == actingEntry)?.OwnerId ?? -1) : null;
            if (ended || State.IsOver) { state = null; color = Palette.Text; }
            else if (actor != null && !actor.IsHero) { state = "ENEMY TURN"; color = Palette.Bad; who.Add(actor); }
            else if (!playing && BattleEngine.TooLittleTime(State)) { state = "TOO LITTLE TIME · tap GUARD"; color = Palette.Bad; }
            else
            {
                bool clock = !Untimed && (dragging || heroTurnHero >= 0);
                state = clock ? "CLOCK RUNNING" : "YOUR TURN";
                color = clock ? Palette.Warn : Palette.Good;
                who.AddRange(BattleEngine.HeroesLeftToAct(State));
            }
            string key = state + "|" + string.Join(",", who.Select(u => u.Id));
            if (key == turnStripKey) return;
            turnStripKey = key;
            turnStrip.style.display = state == null ? DisplayStyle.None : DisplayStyle.Flex;
            turnState.text = state ?? "";
            turnState.style.color = color;
            turnHeroes.Clear();
            foreach (var u in who)
            {
                var t = Ui.Thumb(u.Def.Id, turnHeroes, Size.IconS);
                t.name = "turn-unit-" + u.Id;
                t.pickingMode = PickingMode.Ignore;
                t.style.marginLeft = Space.S8;
                t.style.borderTopLeftRadius = t.style.borderTopRightRadius = t.style.borderBottomLeftRadius = t.style.borderBottomRightRadius = Space.S8;
            }
        }

        /// <summary>The announcement anchor (§9.0, GG-T1824): an empty, input-free box on the board's top edge where the
        /// billboard's fading lines stack. The drag timer's seconds sit in the timeline row beside the NOW diamond, above the
        /// track, so nothing of the drag covers the board.</summary>
        private void BuildAnnouncementBand(VisualElement root)
        {
            bannerViewport = Ui.El("", root);
            bannerViewport.name = "banner-viewport";
            bannerViewport.pickingMode = PickingMode.Ignore;
            bannerViewport.style.position = Position.Absolute;
            bannerViewport.style.left = bannerViewport.style.right = Fit.Edge; bannerViewport.style.top = BannerTop; bannerViewport.style.height = BannerViewportHeight;
            timerLabel = Ui.Text("", "h2 shadow warn", root);
            timerLabel.name = "drag-timer";
            Ui.Scrim(timerLabel); // GG-T1898
            timerLabel.pickingMode = PickingMode.Ignore;
            timerLabel.style.position = Position.Absolute;
            timerLabel.style.left = Fit.Edge + SlotSpan + Space.S8;
            timerLabel.style.top = TimelineY - TimelineFrameHeight / 2f; timerLabel.style.height = TrackY - IconSpan / 2f;
            timerLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
        }

        /// <summary>Re-applies the positions that depend on the stage width (build time and on resize).</summary>
        private void ApplyStageLayout()
        {
            if (boardHost == null) return;
            boardHost.style.left = BoardLeft;
            cutInLayer.style.left = -BoardLeft;
            // The rows under the board (mana strip, action row, description panel) keep to the centred content column, so on a
            // tablet they don't stretch past usefulness; the board centres and the timeline spans the stage (GG-T398).
            float inset = Fit.Inset;
            LayoutAbilityBar(inset);
            if (Root?.Q("description-panel") is VisualElement desc)
            {
                desc.style.left = desc.style.right = inset + Fit.Edge;
                desc.style.top = DescriptionTop;
            }
            cutInLayer.style.width = GameApp.StageWidth; cutInLayer.style.height = GameApp.StageHeight;
            LayoutTimelineFrame();
        }

        private void OnStageResized()
        {
            ApplyStageLayout();
            SyncAll();
        }

        /// <summary>The action row (named "abilitybar") under the board: Abilities, Info, GUARD (§9.0, GG-T1921).</summary>
        private VisualElement abilityBar;
        /// <summary>The host of the party mana bar (GG-T1928): a thin full-width strip above the action row.</summary>
        private VisualElement partyManaHost;
        /// <summary>The party's one mana bar (§3.1.0), filled by <see cref="SyncPartyMana"/>.</summary>
        private PartyManaBar partyManaBar;
        /// <summary>A hero's bar slots, which the tutorial and automation still address by index (<see cref="TapSlot"/>).</summary>
        private const int BarSlots = Progression.MaxBarSlots;

        /// <summary>Lays the action row's cells out as one row under the board, inside the HUD column, and the mana strip
        /// above it across the same width.</summary>
        private void LayoutAbilityBar(float inset)
        {
            if (abilityBar == null) return;
            if (partyManaHost != null)
            {
                partyManaHost.style.top = ManaStripTop; partyManaHost.style.height = ManaStripHeight;
                partyManaHost.style.left = partyManaHost.style.right = inset + Fit.Edge;
            }
            var bar = abilityBar;
            bar.style.top = BarTop; bar.style.height = BarHeight;
            // Row padding + each cell's 12-px margin: cells sit 24 px apart (POL-C3), the row's edge equals that gap
            // (POL-C8), and the cells still line up with the description panel at inset + Fit.Edge (GG-T651, GG-T656).
            bar.style.left = bar.style.right = inset + Fit.Edge - 2 * Space.TargetHalfGap;
            bar.style.paddingLeft = bar.style.paddingRight = Space.TargetHalfGap;
            foreach (var cell in bar.Children()) LayCell(cell);
        }

        /// <summary>A cell's share of the row: an even split, a gutter either side.</summary>
        private static void LayCell(VisualElement cell)
        {
            cell.style.flexGrow = 1;
            cell.style.flexBasis = 0;
            cell.style.height = BarHeight;
            cell.style.marginLeft = cell.style.marginRight = Space.TargetHalfGap;
            cell.style.marginTop = cell.style.marginBottom = Space.S0;
        }

        private void BuildAbilityBar(VisualElement root)
        {
            var strip = partyManaHost = Ui.El(null, root);
            strip.name = "party-mana";
            strip.pickingMode = PickingMode.Ignore;
            strip.style.position = Position.Absolute;
            strip.style.top = ManaStripTop; strip.style.height = ManaStripHeight; // left/right: ApplyStageLayout
            partyManaBar = new PartyManaBar { name = "party-mana-bar" };
            partyManaBar.style.position = Position.Absolute;
            partyManaBar.style.left = partyManaBar.style.right = partyManaBar.style.top = partyManaBar.style.bottom = Space.S0;
            strip.Add(partyManaBar);
            var bar = abilityBar = Ui.El("row", root);
            bar.name = "abilitybar";
            bar.style.position = Position.Absolute;
            // GG-T1921 (owner 2026-11-28, §9.0): three big thumb buttons share the row evenly, Abilities, Info, GUARD; even
            // gutters on the space scale, and the cells, not the gaps, take a wider stage (POL-C1/C8). Laid out by
            // LayoutAbilityBar.
            bar.style.top = BarTop; bar.style.height = BarHeight; // left/right: ApplyStageLayout (the HUD column)
            bar.style.justifyContent = Justify.FlexStart;
            BuildAbilitiesCell(bar);
            BuildInfoCell(bar);
            BuildGuardCell(bar);
        }

        /// <summary>The Info cell (GG-T1918): a tap toggles Examine mode (<see cref="ToggleExamine"/>); gold-edged while on.</summary>
        private void BuildInfoCell(VisualElement bar)
        {
            var cell = infoButton = HudCell(bar);
            cell.name = "info";
            Ui.Pressable(cell);
            var icon = cell.KindIcon(RowKind.Eye);
            icon.style.width = icon.style.height = Size.IconL;
            icon.style.marginRight = Space.S0;
            Ui.Text("Info", "tiny", cell).pickingMode = PickingMode.Ignore;
            cell.RegisterCallback<ClickEvent>(_ => ToggleExamine());
        }

        /// <summary>The GUARD cell for <see cref="SyncGuardCall"/>'s gold call and Guard's mana motes (<see cref="MintFrom"/>).</summary>
        private VisualElement guardButton;
        /// <summary>The GUARD cell's resting edge: pewter on the dark window, apart from the skills' plain frames.</summary>
        private static readonly Color GuardEdge = Palette.PewterLight;

        /// <summary>GG-T1904 (owner 2026-11-28, §9.0): GUARD at the right end of the bottom action row, in thumb reach, built
        /// apart from the other cells (GG-T1914). Its Protection shield fills the cell above the GUARD
        /// caption, which always shows: TextFit shrinks it to the 30 px floor rather than hiding it (GG-T2112).</summary>
        private void BuildGuardCell(VisualElement bar)
        {
            var cell = guardButton = HudCell(bar);
            cell.name = "shield";
            cell.AddToClassList("guard");
            cell.style.backgroundImage = new StyleBackground(Art.WindowDark);
            cell.style.borderTopColor = cell.style.borderBottomColor = cell.style.borderLeftColor = cell.style.borderRightColor = GuardEdge;
            Ui.Pressable(cell);
            cell.RegisterCallback<ClickEvent>(_ => { ExitExamine(); if (CanCommand()) Commit(new ShieldCommand()); });
            var icon = Ui.El(null, cell); icon.name = "icon";
            icon.pickingMode = PickingMode.Ignore;
            icon.style.position = Position.Absolute;
            icon.style.left = icon.style.top = icon.style.right = Space.S16;
            icon.style.backgroundImage = new StyleBackground(Art.StatusIcon("protection"));
            icon.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
            var caption = Ui.Text("GUARD", "tiny shadow", cell);
            caption.name = "guard-label";
            caption.pickingMode = PickingMode.Ignore;
            caption.style.position = Position.Absolute;
            caption.style.left = caption.style.right = Space.S0;
            caption.style.bottom = Space.S8;
            caption.style.unityTextAlign = TextAnchor.LowerCenter;
            caption.style.whiteSpace = WhiteSpace.NoWrap;
            Fonts.UseHeavy(caption);
            cell.RegisterCallback<GeometryChangedEvent>(_ =>
            {
                var need = caption.MeasureTextSize(caption.text, 0, VisualElement.MeasureMode.Undefined, 0, VisualElement.MeasureMode.Undefined);
                icon.style.bottom = Space.S8 + need.y;
            });
        }

        /// <summary>One cell of the action row: a window-framed column that takes an even share of the row.</summary>
        private static VisualElement HudCell(VisualElement bar)
        {
            var cell = Ui.El("col center", bar);
            cell.style.flexGrow = 1; cell.style.flexBasis = 0; cell.style.height = BarHeight;
            cell.style.marginLeft = cell.style.marginRight = Space.S8;
            cell.style.borderTopLeftRadius = cell.style.borderTopRightRadius = cell.style.borderBottomLeftRadius = cell.style.borderBottomRightRadius = Radius.R18;
            cell.style.borderTopWidth = cell.style.borderBottomWidth = cell.style.borderLeftWidth = cell.style.borderRightWidth = 4;
            cell.style.backgroundImage = new StyleBackground(Art.Window);
            return cell;
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Sync views ← state (the safety net after every playback)
        // ─────────────────────────────────────────────────────────────────────────

        public void SyncAll()
        {
            SyncTempo();
            foreach (var u in State.Units)
            {
                if (!board.Units.TryGetValue(u.Id, out var v))
                {
                    if (!u.Alive) continue;
                    v = board.Add(u);
                }
                if (!u.Alive)
                {
                    if (!v.Dead) { v.Root.RemoveFromHierarchy(); v.Dead = true; }
                    continue;
                }
                if (v.Dead) { board.UnitLayer.Add(v.Root); v.Dead = false; }
                board.Place(v, u.Pos);
                v.Root.style.opacity = 1; v.Root.style.scale = new Scale(Vector2.one); v.Root.style.translate = new Translate(0, 0);
                v.SetHp(u.Hp, u.MaxHp);
                v.SetBuffs(u, Colorblind, State.TurnEntryOf(u.Id)?.StunTicks > 0);
                v.SetPlate(u.Facing, u.PlateHp, u.PlateMax);
                v.SetActed(u.IsHero && (TurnRules.OneHeroPerTurn(State) ? TurnRules.MovedThisRound(State, u) : TurnRules.HasActed(State, u))); // GG-T308, §2.13: acted this party turn, as the mode says; GG-T787: no ✓ when the turn is one hero's action; GG-T800: under rounds the ✓ holds until the round resets
                var weaponDef = u.IsHero && u.WeaponId != null ? App.Content.TryItem(u.WeaponId) : null;
                v.SetWeaponCracked(weaponDef != null && weaponDef.Durability > 0 && u.WeaponDurability == 1, !ReduceMotion);
                bool sel = u.Id == SelectedHeroId;
                var aura = AuraFor(u);
                if (sel) v.SetGlow(new Color(1f, 0.9f, 0.5f), 0.65f);
                else if (aura.a > 0f && !ReduceMotion) v.SetGlow(aura, 0.45f);
                else v.SetGlow(Palette.White, 0f);
            }
            board.SyncTraps(State);
            board.SyncRegion(State); // GG-T359
            board.SyncBlocks(State.Blocks);
            board.SyncAmplifier(State.Amplifier);
            board.SyncPickups(State);
            SyncTimeline(null);
            SyncBar();
            SyncPanel();
            SyncTurnStrip();
            int wave = Math.Min(State.Wave + 1, State.Stage.Waves.Count);
            waveLabel.text = InCrucible ? $"Crucible wave {wave}" : $"Wave {wave}/{State.Stage.Waves.Count}";
            if (shownCoins < 0) SyncCoins();
        }

        /// <summary>GG-T1952 (§9.0): the top bar's named boss HP bar. The first living boss on the board (Unit.IsBossLike, lowest
        /// id) swaps in for the stage name; its fill follows the boss token's own gauge, so it drops with playback's beats.</summary>
        private void SyncBossBar()
        {
            if (bossBox == null || State == null) return;
            Unit boss = null;
            UnitView bv = null;
            foreach (var u in State.Units)
                if (u.Team == Team.Enemy && u.OnBoard && u.IsBossLike && (boss == null || u.Id < boss.Id)
                    && board.Units.TryGetValue(u.Id, out var v) && !v.Dead) { boss = u; bv = v; }
            int id = boss?.Id ?? -1;
            if (id != bossBarId)
            {
                bossBarId = id;
                bossBarFraction = -1f;
                bossBox.style.display = boss != null ? DisplayStyle.Flex : DisplayStyle.None;
                VisualElement nameHost = stageName; // the marquee may have wrapped the name in a clip
                while (nameHost.parent != null && nameHost.parent != bossBox.parent) nameHost = nameHost.parent;
                nameHost.style.display = boss != null ? DisplayStyle.None : DisplayStyle.Flex;
                if (boss != null) bossName.text = boss.Name;
            }
            if (bv == null || Mathf.Approximately(bv.HpFraction, bossBarFraction)) return;
            float f = bossBarFraction = bv.HpFraction;
            bossHpFill.style.width = Length.Percent(f * 100f);
            bossHpFill.EnableInClassList("gauge-fill--mid", f <= 0.5f && f > 0.25f);
            bossHpFill.EnableInClassList("gauge-fill--low", f <= 0.25f);
        }

        /// <summary>Linger effect (bible §6.2 stage 4): units carrying a strong status glow its color.</summary>
        private static Color AuraFor(Unit u)
        {
            foreach (var b in u.Buffs)
                switch (b.Def.Id)
                {
                    case "burning": return new Color(1f, 0.45f, 0.1f, 1f);
                    case "frozen": return new Color(0.6f, 0.9f, 1f, 1f);
                    case "poisoned": return new Color(0.5f, 1f, 0.3f, 1f);
                    case "regen": return Palette.Good;
                    case "sleep": return new Color(0.7f, 0.7f, 1f, 1f);
                    case "taunting": return new Color(1f, 0.3f, 0.2f, 1f);
                    case "empowered": return Palette.Buff;
                }
            return new Color(0, 0, 0, 0);
        }

        /// <summary>The effect that stops this unit's clock or turn, if any: buffs that halt/immobilise, else pushback stun.</summary>
        private static (string icon, string letter, Color color, string name)? StatusOf(Unit u, TimelineEntry turn)
        {
            foreach (var b in u.Buffs)
                if (b.Def.HaltsTimeline || b.Def.Immobile)
                    return (b.Def.Id, string.IsNullOrEmpty(b.Def.Letter) ? b.Def.Name.Substring(0, 1) : b.Def.Letter, Art.HexColor(b.Def.Color, new Color(0.6f, 0.85f, 1f)), b.Def.Name);
            if (turn != null && turn.StunTicks > 0) return ("stunned", "!", Palette.Gold, "Stunned");
            return null;
        }

        // ── Pending casts ──────────────────────────────────────────────────────

        private Dictionary<int, float> lastAt;
        private int castCycle;

        /// <summary>
        /// A cast time shows only on the caster's timeline entry (BIBLE §9.0, GG-T1817, owner 2026-11-27): nothing is drawn on
        /// board tiles for a pending cast. This keeps each queued hero cast's ghost at its place on the bar.
        /// </summary>
        private void SyncCastGhosts(Dictionary<int, float> at)
        {
            foreach (var g in castGhosts.Values)
            {
                if (at != null && at.TryGetValue(g.EntryId, out var u)) g.U = u;
                else if (State.Timeline.FirstOrDefault(x => x.Id == g.EntryId) is TimelineEntry e) g.U = BattleEngine.BarU(State, e);
            }
        }

        /// <summary>The shared 1.2 s beat: overlapping timeline circles and status slots each show their next item.</summary>
        private void CycleBeat()
        {
            if (Paused) return; // the 1.2 s beat is game time too (GG-T126)
            castCycle++;
            if (State == null || board == null || lastAt == null) return;
            SyncCastGhosts(lastAt);
            LayoutIcons(lastAt);
            SyncTimelineStatus();
            foreach (var v in board.Units.Values) v.NextStatus();
        }

        /// <summary>Dev hook (automation): queue a pending enemy cast on a unit so its timeline entry can be checked.</summary>
        public int DevQueueCast(int targetId, string abilityId, int ticks)
        {
            var target = State.UnitById(targetId);
            var caster = State.Units.FirstOrDefault(u => u.Alive && !u.IsHero);
            if (target == null || caster == null || App.Content.TryAbility(abilityId) == null) return -1;
            State.Timeline.Add(new TimelineEntry { Id = State.NextEntryId++, Kind = TimelineKind.Cast, OwnerId = caster.Id, AbilityId = abilityId, CasterTeam = Team.Enemy, TargetUnitId = target.Id, TargetTile = target.Pos, TotalCastTicks = ticks, RemainingTicks = ticks, IsCharge = true });
            SyncAll();
            return caster.Id;
        }

        private static void SetBorder(VisualElement e, float radius, float width, Color c)
        {
            e.style.borderTopLeftRadius = e.style.borderTopRightRadius = e.style.borderBottomLeftRadius = e.style.borderBottomRightRadius = radius;
            e.style.borderTopWidth = e.style.borderBottomWidth = e.style.borderLeftWidth = e.style.borderRightWidth = width;
            e.style.borderTopColor = e.style.borderBottomColor = e.style.borderLeftColor = e.style.borderRightColor = c;
        }

        private void SyncBar() { SyncAbilitiesButton(); SyncPartyMana(); }

        /// <summary>Fills one move's <see cref="Ui.AbilitySlot"/> face (an Abilities sheet row's or detail page's icon): the
        /// recharge sweep for a skill, the spell's readiness fill (<see cref="SpellFill"/>), an item's stack. Returns whether
        /// the spell is still charging; <paramref name="border"/> is the kind's frame colour.</summary>
        private bool FillMoveFace(VisualElement v, Unit hero, int i, SlotStatus status, out Color border)
        {
            border = Palette.Text.WithA(0.75f);
            var sl = hero.Bar[i];
            var a = BattleEngine.SlotAbility(State, hero, i);
            bool silenced = status == SlotStatus.Silenced;
            if (sl.IsWeapon)
            {
                border = new Color(0.8f, 0.8f, 0.9f);
                Ui.SetAbilitySlot(v, Art.ItemIcon(sl.WeaponId) ?? Art.Orb, silenced: silenced);
                return false;
            }
            if (sl.IsItem)
            {
                border = new Color(0.86f, 0.62f, 0.36f);
                Ui.SetAbilitySlot(v, Art.ItemIcon(sl.ItemId) ?? Art.AbilityIcon(a) ?? Art.Orb, count: sl.Charges, silenced: silenced);
                return false;
            }
            if (a == null) { Ui.SetAbilitySlot(v, null, silenced: silenced); return false; }
            float sweep = 0f, mana = -1f; int left = 0;
            if (a.Kind == AbilityKind.Skill)
            {
                border = new Color(0.4f, 0.9f, 0.5f);
                if (status == SlotStatus.Cooldown && hero.Cooldowns.TryGetValue(a.Id, out left) && left > 0)
                    sweep = (float)left / Mathf.Max(left, a.CooldownTurns);
            }
            else
            {
                border = Palette.Mana;
                mana = SpellFill(hero, i);
            }
            Ui.SetAbilitySlot(v, Art.AbilityIcon(a) ?? Art.Orb, sweep, left, silenced: silenced, ready: mana >= 1f && !silenced);
            return mana >= 0f && mana < 1f;
        }

        private int panelUnitId = -1;

        private void SyncPanel()
        {
            panel.Clear();
            var u = State.UnitById(panelUnitId >= 0 ? panelUnitId : SelectedHeroId);
            if (u == null || !u.Alive) u = State.UnitById(SelectedHeroId);
            if (refusal != null) PanelLine(panel, refusal, "small warn", "refusal");
            if (examining) { ExaminePanel(); return; }
            if (TargetingSlot >= 0 && State.UnitById(SelectedHeroId) is Unit caster)
            {
                var ab = BattleEngine.SlotAbility(State, caster, TargetingSlot);
                if (ab != null) { AbilityInfo(caster, TargetingSlot, ab); return; }
            }
            if (TapMoving && State.UnitById(tapMoveHeroId) is Unit mover) { TapMoveCard(mover); return; }
            if (advice != null) PanelLine(panel, advice, "small gold", "advice");
            // A slot read without aiming (a hold, or a tap on one that can't aim) stays in the panel until a unit is read or
            // another hero is selected (GG-T1823).
            if (panelUnitId >= 0 || readSlotHero != SelectedHeroId) readSlot = -1;
            if (readSlot >= 0 && State.UnitById(SelectedHeroId) is Unit reader && BattleEngine.SlotAbility(State, reader, readSlot) is AbilityDef read)
            {
                InspectCard.Ability(panel, read, state: SlotState(reader, readSlot, read));
                return;
            }
            // A running lesson is the panel's content (§9.0, GG-T1827) until an aim, a held slot or a unit the player reads needs it.
            if (TutorialActive && panelUnitId < 0)
            {
                TutorialCard(panel);
                return;
            }
            if (u == null) return;
            // Idle (§9.0): one line for the selected hero, then its card and every skill's readout.
            if (u.Id == SelectedHeroId && u.IsHero && u.IsCommandable)
            {
                var idle = Ui.Text($"{u.Name}: tap Abilities to pick a move · hold it to repeat the last one · drag {u.Name} or tap a tile to move", "small gold", panel);
                idle.name = "idle-prompt";
                idle.style.whiteSpace = WhiteSpace.Normal;
                idle.style.marginBottom = Space.S8;
            }
            // GG-T2111: a commandable hero's skills come right under the instruction, so a short panel (9:20, 3:4) shows the
            // instruction and the first skill and scrolls to the hero's card, instead of clipping the skills below it.
            bool skillsFirst = u.IsHero && u.IsCommandable;
            if (skillsFirst) SkillReadout(u);
            var row = Ui.El("row", panel);
            row.style.alignItems = Align.FlexStart;
            if (skillsFirst) row.style.marginTop = Space.S8;
            Ui.Thumb(u.Def.Id, row, Size.Thumb).style.marginRight = Space.S16;
            var col = Ui.El("col grow", row);
            col.style.flexShrink = 1;
            var top = Ui.El("row", col);
            top.style.alignItems = Align.Center;
            var nm = Ui.Text(u.Name, "h2 grow", top);
            nm.style.flexShrink = 1;
            if (u.IsIndependent && !u.Wanderer) Ui.RoleBadge(Ui.TamedRole, top); // GG-T303
            else if (u.Wanderer && !string.IsNullOrEmpty(u.Def.Role)) Ui.RoleBadge(u.Def.Role, top); // a wanderer is a hero class (GG-T342)
            else if (u.IsHero && !string.IsNullOrEmpty(u.Def.Role)) Ui.RoleBadge(u.Def.Role, top);
            Ui.Text($"Lv {u.Level}", "h2 gold", top);
            // What the Bestiary has learned from defeating its class gates the numbers (§25.11, GG-T290).
            int know = Knowledge(u);
            // FF-style plain numbers, no bar (owner 2026-11-21): "HEALTH"/"AETHER" are the same width, so those labels
            // line up instead of "HP"/"MP". A hero's MANA is the party bar and how many of its spells it can pay (§3.1.0).
            var hpRow = Ui.El("row", col);
            Ui.Text("HEALTH", "tiny dim", hpRow).style.marginRight = Space.S8;
            Ui.Text(know >= BestiaryKnowledge.Stats ? $"{u.Hp}/{u.MaxHp}" : "???", "small", hpRow);
            if (u.IsHero)
            {
                var aetherRow = Ui.El("row", col);
                Ui.Text("MANA", "tiny dim", aetherRow).style.marginRight = Space.S8;
                int spells = 0, payable = 0;
                for (int i = 0; i < u.Bar.Count; i++)
                {
                    if (State.SpellPrice(u, i) <= 0) continue;
                    spells++;
                    if (State.SpellReady(u, i)) payable++;
                }
                Ui.Text($"{State.PartyMana}/{BattleConfig.PartyManaCapacity}{(spells == 0 ? "" : $" · {payable}/{spells} spells ready")}", "small", aetherRow);
            }
            if (know >= BestiaryKnowledge.Stats) Ui.Text($"{Ui.StatLine(u.Effective)}  ·  EVA {GridGame.Core.Rules.Formulas.Evasion(u):0}%", "tiny dim", col); // GG-T297
            else if (know >= BestiaryKnowledge.Speed) Ui.Text($"SPD {u.Effective.Speed:0}  ·  defeat more to learn the rest", "tiny dim", col);
            if (!u.IsHero && (know >= BestiaryKnowledge.Elements || u.Def.RevealResistances)) Ui.Text(BestiaryKnowledge.ElementLine(u.Def), "tiny dim", col);
            if (!u.IsHero)
            {
                int n = BattleEngine.ActionsUntilTurn(State, u.Id);
                var cast = State.CastEntryOf(u.Id);
                Ui.Text(cast != null ? $"Charging {App.Content.TryAbility(cast.AbilityId)?.Name}!" : n < 0 ? "Frozen in time" : n == 0 ? "Acts after your next move!" : $"Acts in {n} moves", "small warn", col);
                if (know >= BestiaryKnowledge.Abilities)
                {
                    var mind = Ui.Text($"{EnemyPlanner.DescribeBrain(u.Def.Brain)} · {EnemyPlanner.Describe(EnemyPlanner.TargetingOf(u)).ToLowerInvariant()}", "tiny dim", col);
                    mind.name = "enemy-brain";
                    mind.style.whiteSpace = WhiteSpace.Normal;
                }
                if (u.HasPlate) { var pl = Ui.Text($"Armor plate {u.PlateHp}/{u.PlateMax} on its {SideName(u.Facing)} — strike from the sides, break it head-on, or use spells", "tiny", col); pl.style.whiteSpace = WhiteSpace.Normal; pl.style.color = Palette.Info; }
                if (u.IsCorpse) { var co = Ui.Text($"The body of a {App.Content.TryClass(u.CorpseOf)?.Name ?? "foe"} — blocks the tile, rots in {u.DecayIn} actions", "tiny dim", col); co.style.whiteSpace = WhiteSpace.Normal; }
                var kit = Ui.KitTraits(App.Content, u.Def);
                if (kit.Length > 0) { var kt = GlossaryText.Create(kit, "tiny gold", col); kt.name = "enemy-kit"; kt.style.whiteSpace = WhiteSpace.Normal; }
            }
            // What the flashing status slot on the tile means, one line per effect.
            var statusTurn = State.TurnEntryOf(u.Id);
            if (statusTurn != null && statusTurn.StunTicks > 0) StatusRow(col, "stunned", "!", Palette.Gold, "Stunned", "knocked back on the timeline — its turn icon is held");
            foreach (var b in u.Buffs)
            {
                var pip = StatusRow(col, b.Def.Id, string.IsNullOrEmpty(b.Def.Letter) ? b.Def.Name.Substring(0, 1) : b.Def.Letter,
                    Art.HexColor(b.Def.Color, b.Def.Kind == BuffKind.Buff ? Palette.Mana : new Color(0.8f, 0.4f, 0.9f)),
                    b.Def.Name, $"{Ui.BuffEffect(b.Def)} ({b.Remaining} {(b.Def.Unit == GridGame.Core.Content.DurationUnit.Turns ? "turns" : "s")} left)");
                AttachStatusInspect(pip, b.Def, b.Remaining);
            }
        }

        private static string SideName(Coord f) => f.Y > 0 ? "front (facing down)" : f.Y < 0 ? "front (facing up)" : f.X > 0 ? "front (facing right)" : "front (facing left)";

        private static VisualElement StatusRow(VisualElement parent, string icon, string letter, Color color, string name, string what)
        {
            var row = Ui.El("row", parent);
            var pip = Ui.StatusPip(icon, letter, color, 40, row);
            pip.style.marginRight = Space.S8;
            var t = GlossaryText.Create(GameApp.I?.Content?.TryTerm(name) != null ? $"[{name}] — {what}" : $"{name} — {what}", "tiny", row);
            t.style.whiteSpace = WhiteSpace.Normal; t.style.flexShrink = 1;
            return pip;
        }

        /// <summary>
        /// The aimed ability in the description panel (§9.0, GG-T1823). While aiming: one prompt line ("Fireball: tap a
        /// highlighted foe", GG-T1906); once a target is picked, the confirm card takes the top: on whom and the outcome preview
        /// across the panel's full width, then Cancel and Cast on their own row under it (GG-T1903). Under either, the full card.
        /// </summary>
        private void AbilityInfo(Unit hero, int slot, AbilityDef a)
        {
            bool confirm = PendingTarget.HasValue && TargetingSlot >= 0;
            var col = Ui.El("col", panel);
            col.name = "aim-card";
            col.style.marginBottom = Space.S16;
            if (!confirm)
            {
                var ask = Ui.Text($"{a.Name}: tap a highlighted {AimNoun(a)}", "small gold", col);
                ask.name = "aim-prompt";
                ask.style.whiteSpace = WhiteSpace.Normal;
                // GG-T1921: Cancel goes back to the move's page in the Abilities sheet.
                var back = RowIcon.IconButton("Cancel", RowKind.Close, CancelByPlayer, "btn--small", col);
                back.name = "aim-cancel";
                back.style.alignSelf = Align.FlexStart;
                back.style.marginTop = Space.S8;
            }
            else
            {
                var hits = TargetResolver.Collect(State, a, hero, PendingTarget.Value);
                string who = hits.Count == 0 ? (a.IsTeleport ? "Teleport to the marked tile" : "No one in the area")
                    : hits.Count == 1 ? (hits[0] == hero ? $"On {hero.Name} (self)" : $"On {hits[0].Name}")
                    : $"On {string.Join(", ", hits.Take(3).Select(h => h.Name))}{(hits.Count > 3 ? $" +{hits.Count - 3}" : "")}";
                var summary = Ui.Text(who, "small", col);
                summary.name = "confirm-summary";
                summary.style.whiteSpace = WhiteSpace.Normal;
                OutcomeLines(col, a);
                // Cancel left, Cast right (L7), on their own row under the outcome so it keeps the full width.
                var btns = Ui.El("row", col);
                btns.name = "confirm-buttons";
                btns.style.justifyContent = Justify.SpaceBetween;
                btns.style.marginTop = Space.S8;
                var cancel = RowIcon.IconButton("Cancel", RowKind.Close, CancelByPlayer, "btn--small", btns);
                cancel.name = "confirm-cancel";
                string verb = a.Kind == AbilityKind.Item ? "Use" : a.Kind == AbilityKind.Spell ? "Cast" : "Go";
                var ok = RowIcon.IconButton(verb, RowKind.Confirm, ConfirmPending, "btn--primary", btns, confirmTap: false);
                ok.name = "confirm-action";
            }
            InspectCard.Ability(panel, a, state: SlotState(hero, slot, a));
        }

        /// <summary>What the aim prompt asks the player to tap: a foe, an ally, a unit or a tile.</summary>
        private static string AimNoun(AbilityDef a) =>
            a.Mode != TargetMode.PickActor ? "tile"
            : a.Filter == TargetFilter.EnemyOnly ? "foe"
            : a.Filter == TargetFilter.AllyOnly ? "ally"
            : "unit";

        // ─────────────────────────────────────────────────────────────────────────
        // The description panel (§9.0, GG-T1822): from a gutter under the ability bar to the safe bottom
        // ─────────────────────────────────────────────────────────────────────────

        private ScrollView logPage;
        /// <summary>Every combat line this battle (newest last, capped), so the Log survives a rebuild.</summary>
        private const int LogCap = 200;
        private readonly List<(string text, Color color)> logLines = new List<(string, Color)>();

        /// <summary>The panel holds one page: the aimed or read ability, a running lesson, or the unit. The Party · Lore · Log tab strip
        /// and window are gone (owner 2026-11-27): the Log page is built and filled off the layout and shown by the pause
        /// menu's Battle log (GG-T1825); a unit's lore lives in the hub's Bestiary and hero sheet (GG-T1826).</summary>
        private void BuildDescriptionPanel(VisualElement root)
        {
            var host = Ui.El("col description-panel", root);
            host.name = "description-panel";
            host.style.position = Position.Absolute;
            host.style.top = DescriptionTop; host.style.bottom = Space.S16; // left/right/top: ApplyStageLayout (the HUD column)
            var win = Ui.Window(null, host, "window--compact description-panel__window");
            win.name = "description-window";
            panel = InfoPage(win, "description");
            // The combat log keeps the old feed's name, so automation that looks for "feed" finds it on the pause menu's Battle log.
            logPage = InfoPage(null, "feed");
            foreach (var (text, color) in logLines) AddLogLine(text, color);
        }

        private static ScrollView InfoPage(VisualElement win, string name)
        {
            var sv = new ScrollView(ScrollViewMode.Vertical) { name = name };
            sv.AddToClassList("list");
            sv.AddToClassList("description-panel__page");
            sv.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            sv.touchScrollBehavior = ScrollView.TouchScrollBehavior.Clamped;
            win?.Add(sv);
            return sv;
        }

        /// <summary>The Party page's skill readout under a hero's card: each bar slot's icon, name, cost or recharge,
        /// and its description. Holding a row reads it in full, like holding the slot.</summary>
        private void SkillReadout(Unit hero)
        {
            if (hero == null || !hero.IsHero) return;
            var head = Ui.Text("Skills", "small gold", panel);
            head.style.marginTop = Space.S8;
            for (int i = 0; i < hero.Bar.Count && i < BarSlots; i++)
            {
                if (i >= hero.UnlockedSlots || hero.Bar[i].IsEmpty) continue;
                var sl = hero.Bar[i];
                var a = BattleEngine.SlotAbility(State, hero, i);
                if (a == null) continue;
                var row = Ui.El("row", panel);
                row.name = "skill" + i;
                row.style.alignItems = Align.FlexStart;
                row.style.marginBottom = Space.S8;
                var icon = Ui.El("menu-icon", row);
                icon.style.width = icon.style.height = Size.IconM;
                icon.style.marginRight = Space.S16;
                var tex = sl.IsItem ? Art.ItemIcon(sl.ItemId) : sl.IsWeapon ? Art.ItemIcon(sl.WeaponId) : null;
                icon.style.backgroundImage = new StyleBackground(tex ?? Art.AbilityIcon(a) ?? Art.Orb);
                var col = Ui.El("col grow", row);
                col.style.flexShrink = 1;
                var line = Ui.El("row", col);
                line.style.alignItems = Align.Center;
                var nm = Ui.Text(sl.IsWeapon ? App.Content.TryItem(sl.WeaponId)?.Name ?? a.Name : sl.IsItem ? App.Content.TryItem(sl.ItemId)?.Name ?? a.Name : a.Name, "small", line);
                nm.style.flexShrink = 1;
                if (a.Kind != AbilityKind.Skill && a.Cost > 0)
                {
                    var mana = Ui.Text($"{a.Cost * BattleConfig.ManaPointsPerOrb} mana", "small", line);
                    mana.style.color = Palette.OnWindow(Palette.Mana);
                    mana.style.marginLeft = Space.S8;
                    mana.style.flexShrink = 0;
                }
                int cd = hero.Cooldowns.TryGetValue(a.Id, out var c) ? c : 0;
                string when = sl.IsItem ? $"Item · {sl.Charges}/{sl.MaxCharges} left"
                    : a.Kind == AbilityKind.Skill ? (cd > 0 ? $"Skill · ready in {cd} turns" : a.CooldownTurns > 0 ? $"Skill · recharge {a.CooldownTurns} turns" : "Skill")
                    : a.CastTime > 0 ? $"Spell · charges {a.CastTime:0.#}s" : "Instant";
                var w = Ui.Text(when, "small gold", line);
                w.style.marginLeft = Space.S16;
                w.style.flexShrink = 0;
                if (!string.IsNullOrEmpty(a.Description))
                {
                    var d = GlossaryText.Create(a.Description, "small dim", col);
                    d.style.whiteSpace = WhiteSpace.Normal;
                }
                var ab = a;
                int idx = i;
                InspectCard.Attach(row, body => InspectCard.Ability(body, ab, state: SlotState(hero, idx, ab)));
            }
        }

        private void AddLogLine(string text, Color color)
        {
            if (logPage == null) return;
            var l = Ui.Text(text, "small", logPage);
            // GG-T525: a line's colour (a team's, an element's, mana blue) is lifted until it reads at 4.5:1 on the window.
            l.style.color = Palette.OnWindow(color);
            l.style.whiteSpace = WhiteSpace.Normal;
            l.style.marginBottom = Space.S4;
            while (logPage.contentContainer.childCount > LogCap) logPage.contentContainer.RemoveAt(0);
        }

        /// <summary>Newest at the bottom: pin the log to its end once the new line has been laid out.</summary>
        private void ScrollLogToEnd()
        {
            if (logPage == null) return;
            logPage.schedule.Execute(() => logPage.scrollOffset = new Vector2(0f, Mathf.Max(0f, logPage.contentContainer.layout.height - logPage.contentViewport.layout.height)));
        }
    }
}
