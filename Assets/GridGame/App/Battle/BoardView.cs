using System.Collections;
using System.Collections.Generic;
using GridGame.Core;
using GridGame.Core.Battle;
using GridGame.Core.Content;
using UnityEngine;
using UnityEngine.UIElements;

namespace GridGame.App
{
    /// <summary>One actor on the board: framed portrait crop, HP strip, buff pips (turn order lives on the timeline).</summary>
    public sealed class UnitView
    {
        public readonly int Id;
        public readonly VisualElement Root;
        public readonly VisualElement Frame;
        public readonly VisualElement Art;
        public readonly VisualElement Keyline;
        public readonly VisualElement Hp;
        public readonly VisualElement HpFill;
        public readonly VisualElement Buffs;
        public readonly VisualElement Glow;
        /// <summary>The armor plate (§13.5): a riveted steel bar on the edge the unit faces.</summary>
        public readonly VisualElement Plate;
        /// <summary>Cracked-weapon badge (GG-T39, GG-T1949): shows once the weapon is on its last (x1.5, then shatter) point; the only gear telegraph on the token.</summary>
        public readonly VisualElement WeaponCrack;
        private bool weaponGlow;
        private readonly float tileSize;
        public Coord Tile;
        public int W = 1, H = 1;
        public bool Dead;

        public UnitView(Unit u, float tile)
        {
            Id = u.Id;
            W = u.W; H = u.H;
            Root = new VisualElement { name = "unit-" + u.Id, pickingMode = PickingMode.Ignore };
            Root.style.position = Position.Absolute;
            Root.style.width = tile * u.W;
            Root.style.height = tile * u.H;

            // Rounded-square outline matching the tile frame (GG-T732); SetGlow tints it, a beat pulses it.
            Glow = new VisualElement { pickingMode = PickingMode.Ignore };
            Glow.style.position = Position.Absolute;
            Glow.style.left = Glow.style.top = Glow.style.right = Glow.style.bottom = -Size.GlowRing;
            float gr = tile * 0.4f + 5f;
            Glow.style.borderTopLeftRadius = Glow.style.borderTopRightRadius = Glow.style.borderBottomLeftRadius = Glow.style.borderBottomRightRadius = gr;
            Glow.style.borderTopWidth = Glow.style.borderBottomWidth = Glow.style.borderLeftWidth = Glow.style.borderRightWidth = 5f;
            Glow.style.opacity = 0f;
            glowColor = Palette.White;
            Glow.schedule.Execute(PulseGlow).Every(33);
            Root.Add(Glow);

            // Same frame as every menu portrait (.thumb): white rounded border over the window gradient.
            Frame = new VisualElement { pickingMode = PickingMode.Ignore };
            Frame.AddToClassList("unit-frame");
            Frame.style.position = Position.Absolute;
            Frame.style.left = Frame.style.top = Space.S8;
            Frame.style.right = Frame.style.bottom = Space.S8;
            Frame.style.backgroundImage = new StyleBackground(App.Art.Window);
            Root.Add(Frame);

            // The token is a window onto the unit's real portrait, framed by its class Thumbnail (§12.1.1, GG-T221).
            // The host flips (so the framing mirrors about the token's centre); Art is the portrait child that tints.
            var artHost = new VisualElement { name = "token-art", pickingMode = PickingMode.Ignore };
            artHost.style.position = Position.Absolute;
            artHost.style.left = artHost.style.top = artHost.style.right = artHost.style.bottom = Space.S0;
            if (!u.IsHero && !u.IsNeutral) artHost.style.scale = new Scale(new Vector2(-1, 1)); // enemies face the party
            Frame.Add(artHost);
            // A corpse shows the fallen unit's own art, upright and greyed (§14.7; owner 2026-10-19: never turn art sideways).
            // GG-T1774: a foe's rolled breed draws recoloured; a corpse keeps its living colours (the same unit, same breed).
            Art = Ui.PortraitThumb(artHost, u.IsCorpse && u.CorpseOf != null ? u.CorpseOf : u.Def.Id, breed: u.Breed);
            if (u.IsCorpse)
            {
                Art.style.unityBackgroundImageTintColor = Palette.Vfx.Corpse;
                Frame.style.opacity = 0.8f;
            }

            // Team keyline just inside the white border, so heroes, foes and bosses still read apart.
            Keyline = new VisualElement { name = "keyline", pickingMode = PickingMode.Ignore };
            Keyline.AddToClassList("unit-keyline");
            Frame.Add(Keyline);
            SetTeamLook(u);

            Hp = new VisualElement { pickingMode = PickingMode.Ignore };
            Hp.AddToClassList("gauge");
            Hp.style.position = Position.Absolute;
            Hp.style.left = Space.S16; Hp.style.right = Space.S16; Hp.style.bottom = Space.S8; Hp.style.height = Size.HpBar;
            HpFill = new VisualElement { name = "fill" };
            HpFill.AddToClassList("gauge-fill");
            Hp.Add(HpFill);
            Root.Add(Hp);

            Buffs = new VisualElement { name = "buffs-" + u.Id, pickingMode = PickingMode.Ignore };
            Buffs.style.position = Position.Absolute;
            Buffs.style.flexDirection = FlexDirection.Row;
            Buffs.style.right = Space.S8; Buffs.style.top = Space.S8;
            Root.Add(Buffs);

            tileSize = tile;
            Plate = new VisualElement { name = "plate-" + u.Id, pickingMode = PickingMode.Ignore };
            Plate.style.position = Position.Absolute;
            Plate.style.backgroundColor = Palette.Vfx.Steel;
            Plate.style.borderTopWidth = Plate.style.borderBottomWidth = Plate.style.borderLeftWidth = Plate.style.borderRightWidth = 3;
            Plate.style.borderTopColor = Plate.style.borderLeftColor = Palette.Text;
            Plate.style.borderBottomColor = Plate.style.borderRightColor = Palette.Vfx.SteelShadow;
            Plate.style.borderTopLeftRadius = Plate.style.borderTopRightRadius = Plate.style.borderBottomLeftRadius = Plate.style.borderBottomRightRadius = Radius.R6;
            Plate.style.display = DisplayStyle.None;
            for (int i = 0; i < 3; i++)
            {
                var rivet = new VisualElement { name = "rivet", pickingMode = PickingMode.Ignore };
                rivet.style.width = rivet.style.height = Size.Bar;
                rivet.style.backgroundColor = Palette.Vfx.Rivet;
                rivet.style.borderTopLeftRadius = rivet.style.borderTopRightRadius = rivet.style.borderBottomLeftRadius = rivet.style.borderBottomRightRadius = Radius.R4;
                Plate.Add(rivet);
            }
            Plate.style.justifyContent = Justify.SpaceAround; Plate.style.alignItems = Align.Center;
            Root.Add(Plate);

            // A cracked badge, not a glyph (GG-LAW-11): a red roundel with two crossed white shards.
            WeaponCrack = new VisualElement { name = "weapon-crack-" + u.Id, pickingMode = PickingMode.Ignore };
            WeaponCrack.style.position = Position.Absolute;
            WeaponCrack.style.left = Space.S16; WeaponCrack.style.bottom = Space.S64;
            WeaponCrack.style.width = WeaponCrack.style.height = Size.Crack;
            WeaponCrack.style.borderTopLeftRadius = WeaponCrack.style.borderTopRightRadius = WeaponCrack.style.borderBottomLeftRadius = WeaponCrack.style.borderBottomRightRadius = Radius.R12;
            WeaponCrack.style.backgroundColor = Palette.Vfx.Crack;
            WeaponCrack.style.borderTopWidth = WeaponCrack.style.borderBottomWidth = WeaponCrack.style.borderLeftWidth = WeaponCrack.style.borderRightWidth = 2;
            WeaponCrack.style.borderTopColor = WeaponCrack.style.borderBottomColor = WeaponCrack.style.borderLeftColor = WeaponCrack.style.borderRightColor = Palette.Bad;
            WeaponCrack.style.display = DisplayStyle.None;
            var shardA = new VisualElement { pickingMode = PickingMode.Ignore };
            shardA.style.position = Position.Absolute;
            shardA.style.left = Space.S8; shardA.style.top = Space.S4; shardA.style.width = Size.Hairline; shardA.style.height = Size.Shard;
            shardA.style.backgroundColor = Palette.White;
            shardA.style.rotate = new Rotate(22f);
            WeaponCrack.Add(shardA);
            var shardB = new VisualElement { pickingMode = PickingMode.Ignore };
            shardB.style.position = Position.Absolute;
            shardB.style.left = Space.S8; shardB.style.top = Space.S8; shardB.style.width = Size.Hairline; shardB.style.height = Size.Shard;
            shardB.style.backgroundColor = Palette.White;
            shardB.style.rotate = new Rotate(-26f);
            WeaponCrack.Add(shardB);
            Root.Add(WeaponCrack);

            // A bounty target (§22.4, GG-T431) wears the skull at its top-right corner, pulsing, so it reads as the
            // marked, tougher variant from anywhere on the board.
            if (u.IsBountyTarget)
            {
                // Larger than the status badges and pulsing harder (seen in play: at badge size it read as one more pip).
                var skull = BountySkull.Make(Size.IconM);
                skull.name = "bounty-skull-" + u.Id;
                skull.style.position = Position.Absolute;
                skull.style.right = Space.S4; skull.style.top = Space.S4;
                Root.Add(skull);
                skull.schedule.Execute(() => skull.style.scale = new Scale(Vector3.one * (1f + 0.14f * Mathf.Sin(Tween.Clock * 3f)))).Every(33);
            }

            // One scheduled beat drives the cracked badge glow; cheap no-op when it is inactive.
            Root.schedule.Execute(() =>
            {
                if (weaponGlow) WeaponCrack.style.opacity = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Tween.Clock * 3f));
            }).Every(33);

            // GG-T951: the Floating buff's drop shadow, under everything else on the tile; the card itself lifts off it.
            FloatShadow = new VisualElement { name = "floatshadow-" + u.Id, pickingMode = PickingMode.Ignore };
            FloatShadow.style.position = Position.Absolute;
            FloatShadow.style.left = FloatShadow.style.right = tile * u.W * 0.16f;
            FloatShadow.style.bottom = -Space.S4; FloatShadow.style.height = tile * 0.2f;
            FloatShadow.style.backgroundColor = Palette.Black.WithA(0.4f);
            FloatShadow.style.borderTopLeftRadius = FloatShadow.style.borderTopRightRadius =
                FloatShadow.style.borderBottomLeftRadius = FloatShadow.style.borderBottomRightRadius = tile * 0.1f;
            FloatShadow.style.display = DisplayStyle.None;
            Root.Insert(0, FloatShadow);
            floatPhase = u.Id * 1.37f; // floaters bob out of step with each other
            Root.schedule.Execute(StepFloat).Every(33);

            if (u.IsSnakeSegment)
            {
                Frame.style.borderTopLeftRadius = Frame.style.borderTopRightRadius = Frame.style.borderBottomLeftRadius = Frame.style.borderBottomRightRadius = tile * 0.4f;
                Keyline.style.borderTopLeftRadius = Keyline.style.borderTopRightRadius = Keyline.style.borderBottomLeftRadius = Keyline.style.borderBottomRightRadius = tile * 0.4f - 4;
            }
        }

        /// <summary>The team keyline and corner tag: hero / foe / boss, a GUEST, or a neutral recruitable's TALK (GG-T132).
        /// Called again when a recruitable is talked round, so its frame turns guest-green on the spot.</summary>
        /// <param name="independent">GG-T303: play back a tame / its end as it happened (the state is already the command's end).</param>
        public void SetTeamLook(Unit u, bool? independent = null)
        {
            bool tamed = independent ?? u.IsIndependent;
            bool wanderer = tamed && u.Wanderer; // GG-T342: its own keyline and tag
            foreach (var cls in new[] { "unit-keyline--guest", "unit-keyline--hero", "unit-keyline--boss", "unit-keyline--foe", "unit-keyline--neutral", "unit-keyline--tamed", "unit-keyline--wanderer" })
                Keyline.RemoveFromClassList(cls);
            Keyline.AddToClassList(u.IsNeutral ? "unit-keyline--neutral" : wanderer ? "unit-keyline--wanderer" : tamed ? "unit-keyline--tamed" : u.IsGuest ? "unit-keyline--guest" : independent == false || !u.IsHero ? ((u.IsBossLike || u.IsMultiTile) ? "unit-keyline--boss" : "unit-keyline--foe") : "unit-keyline--hero");
            Frame.Q<Label>("guest-" + u.Id)?.RemoveFromHierarchy();
            if (!u.IsGuest && !u.IsNeutral && !tamed) return;
            var tag = new Label(u.IsNeutral ? "TALK" : wanderer ? "WANDERER" : tamed ? "TAMED" : "GUEST") { name = "guest-" + u.Id, pickingMode = PickingMode.Ignore };
            tag.AddToClassList("shadow");
            tag.style.position = Position.Absolute; tag.style.left = Space.S8; tag.style.top = Space.S8;
            tag.style.fontSize = BoardView.TileLabelFont; Fonts.UseHeavy(tag);
            tag.style.color = u.IsNeutral ? Palette.Neutral : wanderer ? Palette.Wanderer : tamed ? Palette.Tamed : Palette.Vfx.Ally;
            Frame.Add(tag);
        }

        private VisualElement cardBack;

        /// <summary>GG-T1072: the tile's back face, built on first use: the same rounded frame as the front, filled with
        /// the card-back texture. Placeholder art (<see cref="CardFlip.DiamondBack"/>) until the owner supplies a real back.</summary>
        private VisualElement CardBack
        {
            get
            {
                if (cardBack != null) return cardBack;
                cardBack = new VisualElement { name = "card-back-" + Id, pickingMode = PickingMode.Ignore };
                cardBack.AddToClassList("unit-frame");
                cardBack.style.position = Position.Absolute;
                cardBack.style.left = cardBack.style.top = cardBack.style.right = cardBack.style.bottom = Space.S8;
                cardBack.style.borderTopLeftRadius = Frame.style.borderTopLeftRadius; cardBack.style.borderTopRightRadius = Frame.style.borderTopRightRadius;
                cardBack.style.borderBottomLeftRadius = Frame.style.borderBottomLeftRadius; cardBack.style.borderBottomRightRadius = Frame.style.borderBottomRightRadius;
                cardBack.style.backgroundImage = new StyleBackground(CardFlip.DiamondBack);
                cardBack.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Cover);
                cardBack.style.display = DisplayStyle.None;
                Root.Add(cardBack);
                return cardBack;
            }
        }

        /// <summary>
        /// GG-T1072: a card flip about the tile's vertical (Y) axis, <paramref name="turns"/> half-turns of 180° over
        /// <paramref name="seconds"/> (2 = a full turn back to the front). Drawn with the shared <see cref="CardFlip"/>
        /// rule: the width scales by |cos(angle)| down to one pixel edge-on, the front (portrait, bars, badges) shows only
        /// while the tile faces the camera and the back texture only once it has turned past 90°, so the face swap lands
        /// on the edge-on frame with no pop. <paramref name="atBack"/> runs once, the first frame the back fully faces the
        /// camera (180°), so a change to the front (a new team look) happens unseen.
        /// </summary>
        public IEnumerator FlipCard(float seconds, int turns = 2, System.Action atBack = null)
        {
            var back = CardBack;
            float total = 180f * Mathf.Max(1, turns);
            float onePixel = 1f / Mathf.Max(1f, tileSize * W);
            bool fired = atBack == null;
            bool? showingFront = null;
            void Pose(float deg)
            {
                bool front = CardFlip.FrontFacing(deg);
                if (showingFront != front)
                {
                    showingFront = front;
                    foreach (var child in Root.Children())
                        if (child != back && child != Glow) child.style.visibility = front ? Visibility.Visible : Visibility.Hidden;
                }
                float w = CardFlip.Apply(back, deg, onePixel);
                Root.style.scale = new Scale(new Vector2(w, 1f));
                if (!fired && deg >= 180f) { fired = true; atBack(); }
            }
            yield return Tween.Run(seconds, k => Pose(total * k), Tween.EaseInOut);
            Pose(total);
            if (!fired) { fired = true; atBack(); }
            // Rest on whichever face the flip ended on; an even count is back on the front at full width.
            Root.style.scale = new Scale(new Vector2(CardFlip.WidthScale(total, onePixel), 1f));
        }

        /// <summary>The HP fraction the gauge shows now (playback's beat, not the resolved state); the top bar's boss bar mirrors it.</summary>
        public float HpFraction { get; private set; } = 1f;

        public void SetHp(int hp, int max)
        {
            float f = max <= 0 ? 0 : Mathf.Clamp01(hp / (float)max);
            HpFraction = f;
            HpFill.style.width = Length.Percent(f * 100f);
            HpFill.EnableInClassList("gauge-fill--mid", f <= 0.5f && f > 0.25f);
            HpFill.EnableInClassList("gauge-fill--low", f <= 0.25f);
        }

        private readonly List<(string icon, string letter, Color color, float frac)> statuses = new List<(string, string, Color, float)>();
        private int statusBeat;

        private Label actedBadge;
        private bool acted;

        /// <summary>
        /// GG-T308 (§2.0): a hero that has acted in this window greys out and wears a check in its lower-left corner until
        /// the window ends (the next enemy turn, or a new wave); it can't be picked up again until then.
        /// </summary>
        public void SetActed(bool on)
        {
            if (on == acted) return;
            acted = on;
            if (actedBadge == null)
            {
                actedBadge = new Label("✓") { name = "acted-" + Id, pickingMode = PickingMode.Ignore };
                actedBadge.AddToClassList("shadow");
                actedBadge.style.position = Position.Absolute;
                actedBadge.style.left = Space.S8; actedBadge.style.bottom = Size.HpBar + Space.S8;
                actedBadge.style.width = actedBadge.style.height = Size.Pip;
                actedBadge.style.unityTextAlign = TextAnchor.MiddleCenter;
                actedBadge.style.fontSize = BoardView.TileLabelFont;
                actedBadge.style.color = Palette.Text;
                actedBadge.style.backgroundColor = Palette.Ink.WithA(0.85f);
                actedBadge.style.borderTopLeftRadius = actedBadge.style.borderTopRightRadius =
                    actedBadge.style.borderBottomLeftRadius = actedBadge.style.borderBottomRightRadius = Size.Pip / 2f;
                Root.Add(actedBadge);
                // Fades in and out rather than snapping (GG-LAW-5).
                var ease = new List<TimeValue> { new TimeValue(0.3f, TimeUnit.Second) };
                Frame.style.transitionProperty = new List<StylePropertyName> { "opacity" };
                Frame.style.transitionDuration = ease;
                Art.style.transitionProperty = new List<StylePropertyName> { "-unity-background-image-tint-color" };
                Art.style.transitionDuration = ease;
            }
            actedBadge.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
            Art.style.unityBackgroundImageTintColor = on ? Palette.Vfx.Dimmed : Palette.White;
            Frame.style.opacity = on ? 0.7f : 1f;
        }

        private WeightBadge weightBadge;

        /// <summary>
        /// GG-T892 (§1.1.3, §14.2): a Heavy unit (<see cref="Unit.IsHeavy"/>: a Heavy class, Raging, Anchored) wears a
        /// small iron weight in its lower-right corner, above the HP bar, so it reads at a glance as one no drag, shove,
        /// pull or swap will move. It goes while the unit is Floating (Heavy no longer anchors it) and comes back when it lands.
        /// </summary>
        public void SetHeavy(bool on)
        {
            if (weightBadge == null)
            {
                if (!on) return;
                weightBadge = new WeightBadge { name = "heavy-" + Id, pickingMode = PickingMode.Ignore };
                weightBadge.style.right = Space.S8; weightBadge.style.bottom = Size.HpBar + Space.S8;
                weightBadge.style.width = weightBadge.style.height = Size.Pip;
                Root.Add(weightBadge);
            }
            weightBadge.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
        }

        /// <summary>The Heavy weight badge, when the unit wears one (a refused drag or a fizzled shove pops it).</summary>
        public VisualElement HeavyBadge => weightBadge != null && weightBadge.style.display != DisplayStyle.None ? weightBadge : null;

        /// <summary>GG-T951: the soft shadow a Floating unit casts on its tile.</summary>
        public readonly VisualElement FloatShadow;
        private bool floating;
        private float floatPhase, settleStart = -1f;
        private const float FloatLift = 16f, FloatBob = 4f, FloatWobble = 1.2f, SettleTime = 0.35f;

        /// <summary>
        /// GG-T951 (owner 2026-11-24): a Floating unit's card hovers a little above its tile over a soft shadow, bobbing
        /// and wobbling ever so slightly like a Final Fantasy float; it settles back with a small bump when the buff ends.
        /// Reduce Motion keeps only the static lift and shadow (and drops straight back).
        /// </summary>
        public void SetFloating(bool on)
        {
            if (on == floating) return;
            floating = on;
            if (on) { settleStart = -1f; FloatShadow.style.display = DisplayStyle.Flex; }
            else settleStart = Tween.Clock;
            StepFloat();
        }

        private void StepFloat()
        {
            if (!floating && settleStart < 0f) return;
            bool calm = GameApp.I?.Settings?.ReduceMotion == true;
            float y, tilt = 0f, shadow = 1f;
            if (floating)
            {
                float bob = calm ? 0f : Mathf.Sin(Tween.Clock * 2.4f + floatPhase);
                y = -FloatLift - FloatBob * (bob * 0.5f + 0.5f);
                if (!calm) tilt = FloatWobble * Mathf.Sin(Tween.Clock * 1.6f + floatPhase * 0.7f);
                shadow = 1f - 0.12f * (bob * 0.5f + 0.5f); // higher up, smaller shadow
            }
            else
            {
                float k = calm ? 1f : Mathf.Clamp01((Tween.Clock - settleStart) / SettleTime);
                // Drop to the tile (ease in), dip a hair past it, then come to rest: the small bump of landing.
                y = k < 0.7f ? -FloatLift * (1f - (k / 0.7f) * (k / 0.7f)) : 3f * Mathf.Sin((k - 0.7f) / 0.3f * Mathf.PI);
                shadow = 1f - k;
                if (k >= 1f) { settleStart = -1f; y = 0f; FloatShadow.style.display = DisplayStyle.None; }
            }
            FloatShadow.style.opacity = Mathf.Clamp01(shadow);
            FloatShadow.style.scale = new Scale(new Vector2(shadow, 1f));
            foreach (var child in Root.Children())
            {
                if (child == FloatShadow) continue;
                child.style.translate = new Translate(0, y);
                if (child == Frame || child == Glow) child.style.rotate = new Rotate(tilt);
            }
        }

        /// <summary>
        /// One status slot per unit (owner direction 2026-10-10): it flashes through every buff, debuff and stun on
        /// the 1.2 s beat; the selected unit's card explains each one.
        /// </summary>
        public void SetBuffs(Unit u, bool colorblind, bool stunned = false)
        {
            SetHeavy(u.IsHeavy && !u.IsCorpse && !u.IsMultiTile); // GG-T892: buffs make and lift Heavy (Raging, Anchored, Floating)
            statuses.Clear();
            if (stunned) statuses.Add(("stunned", "!", Palette.Gold, 1f)); // pushback stun isn't a buff, but it's why it isn't moving
            foreach (var b in u.Buffs)
                statuses.Add((b.Def.Id, string.IsNullOrEmpty(b.Def.Letter) ? b.Def.Name.Substring(0, 1) : b.Def.Letter,
                    App.Art.HexColor(b.Def.Color, b.Def.Kind == BuffKind.Buff ? Palette.Mana : Palette.Vfx.Debuff),
                    Mathf.Clamp01(b.Remaining / (float)Mathf.Max(1, b.Def.Duration))));
            ShowStatus();
            SetFloating(u.IsFloating && u.Alive);
        }

        public void NextStatus()
        {
            if (statuses.Count < 2) return;
            statusBeat++;
            ShowStatus();
        }

        private void ShowStatus()
        {
            Buffs.Clear();
            if (statuses.Count == 0) return;
            var (icon, letter, color, _) = statuses[statusBeat % statuses.Count];
            var pip = Ui.StatusPip(icon, letter, color, 50);
            pip.name = "statuspip-" + Id;
            Buffs.Add(pip);
        }

        /// <summary>Draw the plate on the facing edge; it fades as it dents and vanishes when broken.</summary>
        public void SetPlate(Coord facing, int hp, int max)
        {
            Plate.style.display = hp > 0 && max > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            if (hp <= 0 || max <= 0) return;
            // On the tile's outer rim (half outside the frame), so it never covers the HP bar or the art.
            float thick = 18f, inset = 14f, w = tileSize * W, h = tileSize * H, out_ = thick * 0.55f;
            bool horizontal = facing.Y != 0;
            Plate.style.flexDirection = horizontal ? FlexDirection.Row : FlexDirection.Column;
            Plate.style.left = facing.X > 0 ? w - thick + out_ : facing.X < 0 ? -out_ : inset;
            Plate.style.top = facing.Y > 0 ? h - thick + out_ : facing.Y < 0 ? -out_ : inset;
            Plate.style.width = horizontal ? w - inset * 2 : thick;
            Plate.style.height = horizontal ? thick : h - inset * 2;
            Plate.style.opacity = 0.45f + 0.55f * Mathf.Clamp01(hp / (float)max);
        }

        /// <summary>
        /// Cracked-weapon badge (GG-T39, GG-T1949): a weapon at its last point (the x1.5 "last strike" durability of 1, §24.5)
        /// shows the badge, pulsing unless <paramref name="animate"/> is false (Reduce Motion). No wear bars: they read as a second HP bar.
        /// </summary>
        public void SetWeaponCracked(bool lastStrike, bool animate)
        {
            weaponGlow = animate && lastStrike;
            WeaponCrack.style.display = lastStrike ? DisplayStyle.Flex : DisplayStyle.None;
            if (!weaponGlow) WeaponCrack.style.opacity = 1f;
        }

        public void SetGlow(Color c, float alpha)
        {
            glowColor = c;
            glowAlpha = alpha;
            Glow.style.borderTopColor = Glow.style.borderBottomColor = Glow.style.borderLeftColor = Glow.style.borderRightColor = c;
            Glow.style.backgroundColor = c.WithA(0.12f);
            Glow.style.opacity = alpha;
        }

        private Color glowColor;
        private float glowAlpha;

        // Rhythmic intensity; static under Reduce Motion.
        private void PulseGlow()
        {
            if (glowAlpha <= 0f || GameApp.I?.Settings?.ReduceMotion == true) return;
            Glow.style.opacity = glowAlpha * (0.6f + 0.4f * Mathf.Sin(Tween.Clock * 4f));
        }
    }

    /// <summary>The 6×8 board: tiles, trap marks, highlight overlay, units, floating fx.</summary>
    public sealed class BoardView
    {
        public const float Tile = 180f;
        /// <summary>GG-T770: marks drawn inside a tile (a GUEST tag, a cast chip's letter, the acted tick, a status count,
        /// a region counter) and a tile-filling pictogram (a trap's skull, a region glyph) are part of the board's fixed
        /// geometry, sized to the tile like an icon; every other text uses the one GUI size, <see cref="TypeScale.Base"/>.</summary>
        public const float TileLabelFont = Size.Pip * 0.75f, TileGlyphFont = Tile * 0.5f;
        public readonly VisualElement Root;
        public readonly VisualElement TileLayer;
        public readonly VisualElement MarkLayer;
        public readonly VisualElement CueLayer;
        public readonly VisualElement UnitLayer;
        public readonly VisualElement FxLayer;
        public readonly Dictionary<int, UnitView> Units = new Dictionary<int, UnitView>();
        private readonly VisualElement[,] tiles = new VisualElement[BattleConfig.Cols, BattleConfig.Rows];
        private readonly VisualElement[,] marks = new VisualElement[BattleConfig.Cols, BattleConfig.Rows];
        private readonly List<VisualElement> trapMarks = new List<VisualElement>();
        public readonly LineLayer Lines;
        public readonly TrailLayer Trail;

        private readonly string theme;

        public BoardView(VisualElement parent, string theme = null)
        {
            this.theme = theme;
            Root = new VisualElement { name = "board" };
            Root.style.position = Position.Absolute;
            Root.style.width = Tile * BattleConfig.Cols;
            Root.style.height = Tile * BattleConfig.Rows;
            parent.Add(Root);

            // GG-T665: under everything, a dark backing that is off unless the region's sky asks for it (SetScrim).
            scrim = Layer("scrim");
            scrim.style.backgroundColor = Palette.Ink.WithA(0f);
            // Order matters: highlights and lines draw OVER the units so targets and pincer previews read clearly.
            TileLayer = Layer("tiles");
            // Under the units: the streak trails behind the moving token (GG-T219).
            Trail = new TrailLayer { name = "trail" };
            Trail.style.position = Position.Absolute;
            Trail.style.left = Trail.style.top = Trail.style.right = Trail.style.bottom = Space.S0;
            Root.Add(Trail);
            UnitLayer = Layer("units");
            // Over the tokens, under the marks: the timeline's approach glow / next-actor and selection outlines (GG-T287).
            CueLayer = Layer("cues");
            MarkLayer = Layer("marks");
            Lines = new LineLayer();
            Lines.style.position = Position.Absolute;
            Lines.style.left = Lines.style.top = Lines.style.right = Lines.style.bottom = Space.S0;
            Lines.pickingMode = PickingMode.Ignore;
            Root.Add(Lines);
            FxLayer = Layer("fx");

            for (int y = 0; y < BattleConfig.Rows; y++)
                for (int x = 0; x < BattleConfig.Cols; x++)
                {
                    var t = new VisualElement { pickingMode = PickingMode.Ignore };
                    t.style.position = Position.Absolute;
                    t.style.left = x * Tile; t.style.top = y * Tile;
                    t.style.width = Tile; t.style.height = Tile;
                    // GridGame2026's tile art recoloured per biome (GridGame.Studio/tools/make_tiles.py), tinted light/dark for the checkerboard.
                    bool light = (x + y) % 2 == 0;
                    t.style.backgroundImage = new StyleBackground(App.Art.BoardTile(theme));
                    // Owner direction 2026-10-11: lighter and lower-contrast so the stage background reads clearly through the board.
                    t.style.unityBackgroundImageTintColor = light ? Palette.White.WithA(0.34f) : Palette.Vfx.Shard.WithA(0.24f);
                    TileLayer.Add(t);
                    tiles[x, y] = t;

                    var m = new VisualElement { pickingMode = PickingMode.Ignore };
                    m.style.position = Position.Absolute;
                    m.style.left = x * Tile + 4; m.style.top = y * Tile + 4;
                    m.style.width = Tile - 8; m.style.height = Tile - 8;
                    m.style.borderTopLeftRadius = m.style.borderTopRightRadius = m.style.borderBottomLeftRadius = m.style.borderBottomRightRadius = Radius.R18;
                    m.style.opacity = 0;
                    MarkLayer.Add(m);
                    marks[x, y] = m;
                }
        }

        private readonly VisualElement scrim;

        /// <summary>GG-T665: darkens the board's own rect behind the tiles to <paramref name="opacity"/> (0 = see-through).</summary>
        public void SetScrim(float opacity) => scrim.style.backgroundColor = Palette.Ink.WithA(Mathf.Clamp01(opacity));

        private VisualElement Layer(string name)
        {
            var l = new VisualElement { name = name, pickingMode = PickingMode.Ignore };
            l.style.position = Position.Absolute;
            l.style.left = l.style.top = l.style.right = l.style.bottom = Space.S0;
            Root.Add(l);
            return l;
        }

        public static Vector2 PosOf(Coord c) => new Vector2(c.X * Tile, c.Y * Tile);
        public static Vector2 CenterOf(Coord c, int w = 1, int h = 1) => new Vector2((c.X + w / 2f) * Tile, (c.Y + h / 2f) * Tile);

        public Coord TileAt(Vector2 local)
        {
            int x = Mathf.Clamp(Mathf.FloorToInt(local.x / Tile), 0, BattleConfig.Cols - 1);
            int y = Mathf.Clamp(Mathf.FloorToInt(local.y / Tile), 0, BattleConfig.Rows - 1);
            return new Coord(x, y);
        }

        // ── Physical blocks (§1.1.1): raised stone that nothing can enter ──
        private readonly HashSet<Coord> drawnBlocks = new HashSet<Coord>();

        public void SyncBlocks(IEnumerable<Coord> blocks)
        {
            foreach (var c in blocks)
            {
                if (!drawnBlocks.Add(c)) continue;
                var shadow = new VisualElement { pickingMode = PickingMode.Ignore };
                shadow.style.position = Position.Absolute;
                shadow.style.left = c.X * Tile + 14; shadow.style.top = c.Y * Tile + 22;
                shadow.style.width = Tile - 22; shadow.style.height = Tile - 22;
                shadow.style.backgroundColor = Palette.Black.WithA(0.45f);
                SetRadius(shadow, Radius.R26);
                TileLayer.Add(shadow);
                var rock = new VisualElement { name = $"block-{c.X}-{c.Y}", pickingMode = PickingMode.Ignore };
                rock.style.position = Position.Absolute;
                rock.style.left = c.X * Tile + 8; rock.style.top = c.Y * Tile + 8;
                rock.style.width = Tile - 16; rock.style.height = Tile - 22;
                rock.style.backgroundImage = new StyleBackground(App.Art.BoardTile(theme));
                rock.style.unityBackgroundImageTintColor = Palette.Vfx.RockTint;
                rock.style.backgroundColor = Palette.Vfx.Rock;
                SetRadius(rock, Radius.R26);
                rock.style.borderTopWidth = 6; rock.style.borderLeftWidth = rock.style.borderRightWidth = 4; rock.style.borderBottomWidth = 12;
                rock.style.borderTopColor = Palette.Vfx.RockLight;
                rock.style.borderLeftColor = rock.style.borderRightColor = Palette.Vfx.RockSide;
                rock.style.borderBottomColor = Palette.Vfx.RockDark;
                TileLayer.Add(rock);
            }
        }

        private static void SetRadius(VisualElement e, float r) =>
            e.style.borderTopLeftRadius = e.style.borderTopRightRadius = e.style.borderBottomLeftRadius = e.style.borderBottomRightRadius = r;

        // ── Power Amplifier marker (§1.2.6): a pulsing gold diamond on its tile ──
        private VisualElement amp;
        public VisualElement Amp => amp;
        /// <summary>The tile the amplifier token is drawn on (null while hidden) — for BoardAudit (GG-T129).</summary>
        public Coord? AmpTile { get; private set; }

        public void SyncAmplifier(Coord? at)
        {
            if (amp == null)
            {
                amp = new VisualElement { name = "amplifier", pickingMode = PickingMode.Ignore };
                amp.style.position = Position.Absolute;
                amp.style.width = amp.style.height = Tile * 0.5f;
                amp.style.rotate = new Rotate(45f);
                amp.style.borderTopWidth = amp.style.borderBottomWidth = amp.style.borderLeftWidth = amp.style.borderRightWidth = 6;
                amp.style.borderTopColor = amp.style.borderBottomColor = amp.style.borderLeftColor = amp.style.borderRightColor = Palette.Vfx.AmpRim;
                amp.style.backgroundColor = Palette.Vfx.AmpFill;
                amp.style.borderTopLeftRadius = amp.style.borderTopRightRadius = amp.style.borderBottomLeftRadius = amp.style.borderBottomRightRadius = Radius.R10;
                var glow = new VisualElement { pickingMode = PickingMode.Ignore };
                glow.style.position = Position.Absolute;
                glow.style.left = glow.style.top = glow.style.right = glow.style.bottom = -Space.S32;
                glow.style.backgroundImage = new StyleBackground(App.Art.Glow);
                glow.style.unityBackgroundImageTintColor = Palette.Warn.WithA(0.8f);
                amp.Add(glow);
                MarkLayer.Add(amp);
                amp.schedule.Execute(() =>
                {
                    float k = 1f + 0.08f * Mathf.Sin(Tween.Clock * 5f);
                    amp.style.scale = new Scale(new Vector2(k, k));
                }).Every(33);
            }
            AmpTile = at;
            amp.style.display = at.HasValue ? DisplayStyle.Flex : DisplayStyle.None;
            if (at.HasValue) PlaceAmp(CenterOf(at.Value));
        }

        public void PlaceAmp(Vector2 centre)
        {
            amp.style.left = centre.x - Tile * 0.25f;
            amp.style.top = centre.y - Tile * 0.25f;
        }

        /// <summary>Board-space rect around every tile of a group (a pincer's attackers + opponents), slightly outside the frames.</summary>
        public static Rect GroupRect(IEnumerable<(Coord pos, int w, int h)> members)
        {
            float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
            foreach (var (pos, w, h) in members)
            {
                x0 = Mathf.Min(x0, pos.X * Tile); y0 = Mathf.Min(y0, pos.Y * Tile);
                x1 = Mathf.Max(x1, (pos.X + w) * Tile); y1 = Mathf.Max(y1, (pos.Y + h) * Tile);
            }
            return Rect.MinMaxRect(x0 - 4f, y0 - 4f, x1 + 4f, y1 + 4f);
        }

        /// <summary>Short link bridging the gap between a supporter's frame and its partner's (edge to edge, never over the art).</summary>
        public static (Vector2 a, Vector2 b) EdgeLink(Coord from, Coord to)
        {
            var d = new Vector2(to.X - from.X, to.Y - from.Y);
            if (d.sqrMagnitude > 0f) d.Normalize();
            var ca = CenterOf(from); var cb = CenterOf(to);
            return (ca + d * (Tile * 0.5f - 16f), cb - d * (Tile * 0.5f - 16f));
        }

        public UnitView Add(Unit u)
        {
            var v = new UnitView(u, Tile);
            Units[u.Id] = v;
            UnitLayer.Add(v.Root);
            Place(v, u.Pos);
            return v;
        }

        public void Place(UnitView v, Coord c)
        {
            v.Tile = c;
            var p = PosOf(c);
            v.Root.style.left = p.x;
            v.Root.style.top = p.y;
        }

        public void ClearMarks()
        {
            foreach (var m in marks) m.style.opacity = 0;
        }

        public void Mark(Coord c, Color color, float alpha = 0.55f)
        {
            if (!BattleState.InBounds(c)) return;
            var m = marks[c.X, c.Y];
            m.style.backgroundColor = color;
            m.style.opacity = alpha;
        }

        // ── Timeline cues (GG-T287): a unit whose icon nears the left edge glows on its tile in the icon's own colour, the
        //    next actor's tile carries the same gold border its icon does, and the selected unit gets a white outline. ──
        private readonly Dictionary<int, VisualElement> cues = new Dictionary<int, VisualElement>();
        private VisualElement selectionCue;

        private VisualElement NewCue(string name, float radius)
        {
            var c = new VisualElement { name = name, pickingMode = PickingMode.Ignore };
            c.style.position = Position.Absolute;
            c.style.borderTopLeftRadius = c.style.borderTopRightRadius = c.style.borderBottomLeftRadius = c.style.borderBottomRightRadius = radius;
            CueLayer.Add(c);
            return c;
        }

        private static void Frame(VisualElement c, Coord at, int w, int h, float inset, float border, Color borderColor)
        {
            c.style.left = at.X * Tile + inset; c.style.top = at.Y * Tile + inset;
            c.style.width = w * Tile - 2f * inset; c.style.height = h * Tile - 2f * inset;
            c.style.borderTopWidth = c.style.borderBottomWidth = c.style.borderLeftWidth = c.style.borderRightWidth = border;
            c.style.borderTopColor = c.style.borderBottomColor = c.style.borderLeftColor = c.style.borderRightColor = borderColor;
        }

        /// <summary>Glow a unit's footprint: <paramref name="fill"/> tints the tile, <paramref name="border"/> rings it.</summary>
        public void SetCue(int unitId, Coord at, int w, int h, Color fill, Color border, float borderWidth)
        {
            if (!cues.TryGetValue(unitId, out var c)) cues[unitId] = c = NewCue("cue-" + unitId, 18);
            Frame(c, at, w, h, 6f, borderWidth, border);
            c.style.backgroundColor = fill;
            c.style.display = DisplayStyle.Flex;
        }

        /// <summary>The selection outline (GG-T288; null hides it), on the tile edge outside the approach cue.</summary>
        public void SetSelection(Coord? at, int w, int h, Color c)
        {
            if (selectionCue == null) selectionCue = NewCue("cue-selected", 12);
            if (!at.HasValue) { selectionCue.style.display = DisplayStyle.None; return; }
            Frame(selectionCue, at.Value, w, h, 0f, 5f, c);
            selectionCue.style.display = DisplayStyle.Flex;
        }

        private VisualElement activeHeroCue;

        /// <summary>
        /// The active hero's outline (GG-T1110, TurnMode.HeroTimeline; null hides it): a ring just outside the tile edge, so
        /// it reads alongside the selection outline and the approach cue instead of covering them.
        /// </summary>
        public void SetActiveHero(Coord? at, int w, int h, Color c)
        {
            if (activeHeroCue == null) activeHeroCue = NewCue("cue-active-hero", 16);
            if (!at.HasValue) { activeHeroCue.style.display = DisplayStyle.None; return; }
            Frame(activeHeroCue, at.Value, w, h, -5f, 4f, c);
            activeHeroCue.style.display = DisplayStyle.Flex;
        }

        /// <summary>Hides every unit cue not in <paramref name="keep"/>.</summary>
        public void HideCues(ICollection<int> keep)
        {
            foreach (var kv in cues)
                if (!keep.Contains(kv.Key)) kv.Value.style.display = DisplayStyle.None;
        }

        // ── Pickups (§13.3.1): a bobbing, glowing token on its tile; a ring shows how long until it fades ──
        public readonly Dictionary<int, VisualElement> PickupViews = new Dictionary<int, VisualElement>();

        public VisualElement AddPickup(Pickup p, PickupDef def)
        {
            var root = new VisualElement { name = "pickup-" + p.Id, pickingMode = PickingMode.Ignore };
            root.style.position = Position.Absolute;
            root.style.width = root.style.height = Tile * 0.56f;
            var c = App.Art.HexColor(def?.Color, Palette.Gold);
            var glow = new VisualElement { pickingMode = PickingMode.Ignore };
            glow.style.position = Position.Absolute;
            glow.style.left = glow.style.top = glow.style.right = glow.style.bottom = -Space.S32;
            glow.style.backgroundImage = new StyleBackground(App.Art.Glow);
            glow.style.unityBackgroundImageTintColor = c.WithA(0.85f);
            root.Add(glow);
            var disc = new VisualElement { name = "disc", pickingMode = PickingMode.Ignore };
            disc.style.position = Position.Absolute;
            disc.style.left = disc.style.top = disc.style.right = disc.style.bottom = Space.S0;
            SetRadius(disc, Tile * 0.28f);
            disc.style.backgroundColor = Palette.Ink.WithA(0.92f);
            disc.style.borderTopWidth = disc.style.borderBottomWidth = disc.style.borderLeftWidth = disc.style.borderRightWidth = 5;
            disc.style.borderTopColor = disc.style.borderBottomColor = disc.style.borderLeftColor = disc.style.borderRightColor = c;
            var icon = new VisualElement { pickingMode = PickingMode.Ignore };
            icon.style.position = Position.Absolute;
            icon.style.left = icon.style.top = icon.style.right = icon.style.bottom = Space.S8;
            var tex = App.Art.UiIcon(def?.Icon);
            if (tex != null) { icon.style.backgroundImage = new StyleBackground(tex); icon.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain); }
            disc.Add(icon);
            root.Add(disc);
            // The fade ring lives inside the bobbing disc so it floats with the token (GG-T229); the disc fills the root,
            // so the ring's -4 inset is unchanged and it still draws over the icon.
            var ring = new RingTimer(Mathf.Clamp01(p.ActionsLeft / (float)BattleConfig.PickupLifetimeActions)) { name = "life", pickingMode = PickingMode.Ignore };
            disc.Add(ring);
            PlacePickup(root, p.At);
            float phase = (p.Id * 1.7f) % 6.28f;
            root.schedule.Execute(() =>
            {
                float t = Tween.Clock * 3f + phase;
                disc.style.translate = new Translate(0, -8f * Mathf.Abs(Mathf.Sin(t)));
                glow.style.opacity = 0.65f + 0.35f * Mathf.Sin(t * 1.3f);
            }).Every(33);
            MarkLayer.Add(root);
            PickupViews[p.Id] = root;
            return root;
        }

        /// <summary>The top-left of a pickup token centred on <paramref name="at"/>.</summary>
        public static Vector2 PickupPos(Coord at) => CenterOf(at) - new Vector2(Tile * 0.28f, Tile * 0.28f);

        /// <summary>Put a pickup token on a tile. Pickups move like actors since GG-T228, so its userData — the tile it is
        /// drawn on, which BoardAudit checks (GG-T129) — follows every placement.</summary>
        public static void PlacePickup(VisualElement v, Coord at)
        {
            var pos = PickupPos(at);
            v.style.left = pos.x;
            v.style.top = pos.y;
            v.userData = at;
        }

        /// <summary>Snap pickup tokens to the state: add missing ones, put each on its tile, drop collected/faded ones, refresh fade rings.</summary>
        public void SyncPickups(BattleState s)
        {
            var live = new HashSet<int>();
            foreach (var p in s.Pickups)
            {
                live.Add(p.Id);
                s.Content.Pickups.TryGetValue(p.PickupId, out var def);
                if (!PickupViews.TryGetValue(p.Id, out var v)) v = AddPickup(p, def);
                if (!(v.userData is Coord drawnAt) || drawnAt != p.At) PlacePickup(v, p.At);
                var old = v.Q("life");
                old?.RemoveFromHierarchy();
                (v.Q("disc") ?? v).Add(new RingTimer(Mathf.Clamp01(p.ActionsLeft / (float)BattleConfig.PickupLifetimeActions)) { name = "life", pickingMode = PickingMode.Ignore });
                v.style.opacity = p.ActionsLeft <= 1 ? 0.6f : 1f;
            }
            foreach (var id in new List<int>(PickupViews.Keys))
                if (!live.Contains(id)) { PickupViews[id].RemoveFromHierarchy(); PickupViews.Remove(id); }
        }

        /// <summary>GG-T839: while a playback runs, the trap tiles as of the beat now playing (later beats rewound out of
        /// the end state); null, or a null result, draws the state's own.</summary>
        public System.Func<IEnumerable<Coord>> TrapsShown;

        public void SyncTraps(BattleState s)
        {
            foreach (var t in trapMarks) t.RemoveFromHierarchy();
            trapMarks.Clear();
            var shown = TrapsShown?.Invoke();
            if (shown == null) { var own = new List<Coord>(); foreach (var trap in s.Traps) own.Add(trap.At); shown = own; }
            foreach (var at in shown)
            {
                var e = new Label("☠") { pickingMode = PickingMode.Ignore };
                e.style.position = Position.Absolute;
                e.style.left = at.X * Tile; e.style.top = at.Y * Tile;
                e.style.width = Tile; e.style.height = Tile;
                e.style.unityTextAlign = TextAnchor.MiddleCenter;
                e.style.fontSize = TileGlyphFont;
                e.style.color = Palette.Vfx.CurseText;
                e.style.backgroundColor = Palette.Vfx.CurseBack;
                MarkLayer.Add(e);
                trapMarks.Add(e);
            }
        }

        private readonly List<VisualElement> regionMarks = new List<VisualElement>();
        private readonly HashSet<string> regionSeen = new HashSet<string>();

        /// <summary>
        /// Region rules (GG-T359): every painted tile as a wash in its state's colour with its glyph, every tile a queued
        /// hazard telegraphs as a pulsing warning in its event's colour (it shows from the moment it is queued until it
        /// lands), and every rule anchor. A mark that wasn't drawn last sync fades in.
        /// </summary>
        /// <summary>GG-T839: while a playback runs, the queued hazards as of the beat now playing (a later beat's queue not
        /// up yet, a later beat's landing still up); null, or a null result, draws the state's timeline.</summary>
        public System.Func<IEnumerable<(int Id, string RuleEvent, List<Coord> Tiles)>> HazardsShown;

        private static IEnumerable<(int Id, string RuleEvent, List<Coord> Tiles)> HazardsOf(BattleState s)
        {
            foreach (var t in s.Timeline)
                if (t.Kind == TimelineKind.Hazard && t.HazardTiles != null) yield return (t.Id, t.RuleEvent, t.HazardTiles);
        }

        public void SyncRegion(BattleState s)
        {
            foreach (var m in regionMarks) m.RemoveFromHierarchy();
            regionMarks.Clear();
            if (s?.Region == null) return;
            var seen = new HashSet<string>();
            regionCellAt.Clear();
            bool calm = GameApp.I?.Settings?.ReduceMotion == true;
            foreach (var kv in s.Region.Tiles)
            {
                var def = s.Content?.TryTileState(kv.Value.State);
                var col = Art.HexColor(def?.Color, kv.Value.Hazard ? Palette.Bad : Palette.Info);
                string key = "m" + kv.Key + kv.Value.State;
                // GG-T843: a hidden mark is drawn only when the party sees it (its own, faint); a foe's is not drawn at all.
                // GG-T846: one that was in plain sight last sync fades out over 0.5 s instead of blinking off.
                if (!RegionBoard.Sees(kv.Value, Team.Hero))
                {
                    if (kv.Value.Hidden && regionSeen.Contains(key) && !regionFaint.Contains(key) && !calm) FadeOutCell(kv.Key, col, def?.Glyph);
                    continue;
                }
                bool faint = kv.Value.Hidden;
                seen.Add(key);
                // GG-T846: an owned mark's edge wears its painter's team colour (the party blue, a foe red).
                var edge = kv.Value.OwnerTeam == Team.Hero ? TeamHero : kv.Value.OwnerTeam == Team.Enemy ? Palette.TeamFoe : col;
                // A hole (GG-T368) is a near-black pit with its rim in the state's colour; other blocks a heavy wash.
                var fill = def != null && def.Pit ? Palette.Vfx.Pit : col.WithA(kv.Value.Blocks ? 0.55f : 0.3f);
                var e = RegionCell(kv.Key, fill, edge, def?.Glyph, kv.Value.Blocks ? 6f : 3f, !faint && !regionSeen.Contains(key));
                regionCellAt[kv.Key] = e;
                // GG-T846: hidden outline and hatch pulse (no life ring or countdown on the tile, GG-T1817).
                float life = def != null && def.LifeSeconds > 0f && kv.Value.LifeTicks > 0
                    ? Mathf.Clamp01(kv.Value.LifeTicks / (def.LifeSeconds * BattleConfig.TicksPerSecond)) : -1f;
                var fx = new TileMarkFx(edge, col, faint);
                // GG-T935: the party's standing wall (the Machinist's barricade) is a braced crate, its brace splitting as it is bashed.
                if (IsPartyWall(def, kv.Value))
                {
                    fx.Crate = true;
                    fx.Wear = def.Threshold > 0 ? Mathf.Clamp01(kv.Value.Counter / (float)def.Threshold) : 0f;
                    foreach (var c in e.Children()) if (c is Label) c.style.opacity = 0f; // the crate is its glyph
                }
                e.Add(fx);
                // GG-T895: a hatching mark (a slime glob) is a wobbling blob, redrawn every frame so it jiggles.
                if (Hatches(def))
                {
                    fx.Glob = true; fx.Calm = calm;
                    if (!calm && !faint) fx.schedule.Execute(fx.MarkDirtyRepaint).Every(33);
                }
                if (faint)
                {
                    // The party's own hidden mark: its wash fades out over 0.5 s (from when it hid), the dotted owner outline stays.
                    if (!regionFaint.Contains(key)) regionHidAt[key] = Tween.Clock;
                    float hidAt = regionHidAt.TryGetValue(key, out var h) ? h : Tween.Clock - 1f;
                    void Faint()
                    {
                        float k = calm ? 1f : Mathf.Clamp01((Tween.Clock - hidAt) / 0.5f);
                        e.style.backgroundColor = fill.WithA(Mathf.Lerp(fill.a, fill.a * 0.15f, k));
                        float bw = Mathf.Lerp(kv.Value.Blocks ? 6f : 3f, 0f, k);
                        e.style.borderTopWidth = e.style.borderBottomWidth = e.style.borderLeftWidth = e.style.borderRightWidth = bw;
                        foreach (var c in e.Children()) if (c is Label) c.style.opacity = Mathf.Lerp(1f, 0.35f, k);
                    }
                    Faint();
                    if (!calm) e.schedule.Execute(Faint).Every(16).Until(() => Tween.Clock - hidAt >= 0.5f);
                }
                else regionHidAt.Remove(key);
                if (life >= 0f)
                {
                    float from = regionLife.TryGetValue(key, out var was) && was >= life ? was : life;
                    regionLife[key] = life;
                    if (!calm && from > life)
                    {
                        float t0 = Tween.Clock;
                        fx.schedule.Execute(() => { fx.Life = Mathf.Lerp(from, life, Mathf.Clamp01((Tween.Clock - t0) / 0.3f)); fx.MarkDirtyRepaint(); })
                            .Every(16).Until(() => Tween.Clock - t0 >= 0.3f);
                    }
                    fx.Life = calm ? life : from;
                    // A mark that hatches when its life runs out (a slime glob's OnExpire SpawnUnit) swells in a pulse that
                    // grows stronger and quicker as it nears the end. Reduce Motion: no pulse, the ring still drains.
                    if (!calm && Hatches(def))
                    {
                        float amp = 0.03f + 0.09f * (1f - life), speed = 3f + 6f * (1f - life);
                        e.schedule.Execute(() => e.style.scale = new Scale(Vector3.one * (1f + amp * Mathf.Abs(Mathf.Sin(Tween.Clock * speed))))).Every(16);
                    }
                }
                if (faint) regionFaint.Add(key); else regionFaint.Remove(key);
                if (kv.Value.Counter > 0 && def != null && def.Threshold > 0)
                {
                    var n = new Label($"{kv.Value.Counter}/{def.Threshold}") { pickingMode = PickingMode.Ignore };
                    n.style.position = Position.Absolute;
                    n.style.right = Space.S8; n.style.bottom = Space.S4;
                    n.style.fontSize = TileLabelFont;
                    n.style.color = Palette.Text;
                    e.Add(n);
                }
            }
            foreach (var (hid, ruleEvent, hazardTiles) in HazardsShown?.Invoke() ?? HazardsOf(s))
            {
                if (hazardTiles == null) continue;
                var ev = s.Content?.TryRuleEvent(ruleEvent);
                var col = Art.HexColor(ev?.Color, Palette.CastEnemy);
                foreach (var c in hazardTiles)
                {
                    string key = "h" + hid + c;
                    seen.Add(key);
                    var w = RegionCell(c, col.WithA(ev == null || ev.Hazard ? 0.22f : 0.1f), col, string.IsNullOrEmpty(ev?.Glyph) ? "⚠" : ev.Glyph, 5f, !regionSeen.Contains(key));
                    // GG-T846: a queued blast keeps its telegraph over any mark (drawn above them); Reduce Motion holds it steady.
                    if (!calm) w.schedule.Execute(() => w.style.opacity = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Tween.Clock * 4f))).Every(50);
                }
            }
            foreach (var a in s.Region.Anchors)
            {
                string key = "a" + a.Key + a.Value;
                seen.Add(key);
                RegionCell(a.Value, Color.clear, Palette.Gold, "◎", 4f, !regionSeen.Contains(key));
            }
            regionSeen.Clear();
            regionSeen.UnionWith(seen);
            regionFaint.IntersectWith(seen);
            Prune(regionLife, seen); Prune(regionHidAt, seen);
        }

        // GG-T846: per-mark view memory across syncs (marks are rebuilt each sync).
        private readonly HashSet<string> regionFaint = new HashSet<string>();
        private readonly Dictionary<string, float> regionLife = new Dictionary<string, float>();
        private readonly Dictionary<string, float> regionHidAt = new Dictionary<string, float>();
        private readonly Dictionary<Coord, VisualElement> regionCellAt = new Dictionary<Coord, VisualElement>();

        private static Color TeamHero => Palette.Colorblind ? Palette.CbHero : Palette.Hero;

        /// <summary>The state hatches a unit when its life runs out (an OnExpire SpawnUnit: a slime glob).</summary>
        public static bool Hatches(TileStateDef def)
        {
            if (def?.OnExpire == null) return false;
            foreach (var fx in def.OnExpire) if (fx != null && fx.Kind == RuleEffectKind.SpawnUnit) return true;
            return false;
        }

        private static void Prune<T>(Dictionary<string, T> d, HashSet<string> keep)
        {
            if (d.Count == 0) return;
            var drop = new List<string>();
            foreach (var k in d.Keys) if (!keep.Contains(k)) drop.Add(k);
            foreach (var k in drop) d.Remove(k);
        }

        /// <summary>GG-T846: a mark the party just lost sight of fades out over 0.5 s, then is gone.</summary>
        private void FadeOutCell(Coord at, Color col, string glyph)
        {
            var e = RegionCell(at, col.WithA(0.3f), col, glyph, 3f, false);
            regionMarks.Remove(e); // not part of the synced set: it removes itself
            float t0 = Tween.Clock;
            e.schedule.Execute(() =>
            {
                float k = Mathf.Clamp01((Tween.Clock - t0) / 0.5f);
                e.style.opacity = 1f - k;
                if (k >= 1f) e.RemoveFromHierarchy();
            }).Every(16).Until(() => e.parent == null);
        }

        /// <summary>GG-T846: the drawn cell of the painted mark on <paramref name="at"/> (null when none is drawn).</summary>
        public VisualElement RegionCellAt(Coord at) => regionCellAt.TryGetValue(at, out var e) ? e : null;

        /// <summary>GG-T935: a standing wall the party raised (a blocking mark that is neither a hole nor a hazard, owned by
        /// the party: the Machinist's barricade), drawn as a crate.</summary>
        public static bool IsPartyWall(TileStateDef def, TileMark m) =>
            def != null && m != null && def.Blocks && !def.Pit && !def.Hazard && m.OwnerTeam == Team.Hero;

        /// <summary>GG-T935: a loose crate on the mark layer over <paramref name="at"/> (Shapes-drawn, ink outline) for the
        /// barricade's drop; the caller moves it with <c>style.translate</c> and removes it.</summary>
        public VisualElement CrateDrop(Coord at)
        {
            var e = new VisualElement { pickingMode = PickingMode.Ignore };
            e.style.position = Position.Absolute;
            e.style.left = at.X * Tile; e.style.top = at.Y * Tile;
            e.style.width = Tile; e.style.height = Tile;
            e.generateVisualContent += ctx => TileMarkFx.DrawCrate(ctx.painter2D, e.contentRect, 0f);
            MarkLayer.Add(e);
            return e;
        }

        /// <summary>GG-T846: a squished mark bursts into a splat of its colour (Reduce Motion: a plain fade, no burst).</summary>
        public void Splat(Coord at, Color col)
        {
            var e = new TileSplat(col, GameApp.I?.Settings?.ReduceMotion == true);
            e.style.left = at.X * Tile; e.style.top = at.Y * Tile;
            MarkLayer.Add(e);
        }

        private VisualElement RegionCell(Coord at, Color fill, Color border, string glyph, float borderWidth, bool fadeIn)
        {
            var e = new VisualElement { pickingMode = PickingMode.Ignore };
            e.style.position = Position.Absolute;
            e.style.left = at.X * Tile; e.style.top = at.Y * Tile;
            e.style.width = Tile; e.style.height = Tile;
            e.style.backgroundColor = fill;
            e.style.borderTopWidth = e.style.borderBottomWidth = e.style.borderLeftWidth = e.style.borderRightWidth = borderWidth;
            e.style.borderTopColor = e.style.borderBottomColor = e.style.borderLeftColor = e.style.borderRightColor = border.WithA(0.85f);
            if (!string.IsNullOrEmpty(glyph))
            {
                var l = new Label(glyph) { pickingMode = PickingMode.Ignore };
                l.style.position = Position.Absolute;
                l.style.left = l.style.top = l.style.right = l.style.bottom = Space.S0;
                l.style.unityTextAlign = TextAnchor.MiddleCenter;
                l.style.fontSize = TileGlyphFont;
                l.style.color = border.WithA(0.9f);
                e.Add(l);
            }
            if (fadeIn)
            {
                e.style.opacity = 0f;
                e.schedule.Execute(() => e.style.opacity = Mathf.Min(1f, e.style.opacity.value + 0.12f)).Every(16).Until(() => e.style.opacity.value >= 1f);
            }
            MarkLayer.Add(e);
            regionMarks.Add(e);
            return e;
        }

        /// <summary>A bare floating text label (not animated, the caller moves it) in a combat text kind's face and
        /// outline (GG-T826: built through <see cref="CombatText.Build"/>). Prefer <see cref="CombatText.Show(UnitView, CombatTextKind, string, float, Color?)"/>,
        /// which also animates and stacks; this stays for the few callers that drive the label themselves (the damage tally).
        /// GG-T829: <paramref name="size"/> defaults to <see cref="TypeScale.Base"/>; a literal size here fails POL-A1.</summary>
        public Label Float(string text, Vector2 at, Color color, float? size = null, CombatTextKind kind = CombatTextKind.Support)
        {
            float s = size ?? TypeScale.Base;
            return CombatText.Build(this, text, at - new Vector2(0f, 60f - s * 0.75f), CombatText.StyleOf(kind), color, s);
        }
    }

    /// <summary>Radial countdown ring drawn around a buff pip: the arc shrinks clockwise as the buff runs out.</summary>
    public sealed class RingTimer : VisualElement
    {
        private readonly float frac;
        public RingTimer(float frac)
        {
            this.frac = frac;
            style.position = Position.Absolute;
            style.left = style.top = style.right = style.bottom = -Space.S4;
            generateVisualContent += ctx =>
            {
                // GG-T737: a gold Shapes arc (ink outline) shrinking clockwise from 12 o'clock.
                var r = contentRect;
                var st = ShapeStyle.Small.Tint(Palette.Gold.WithA(0.95f));
                float radius = Mathf.Min(r.width, r.height) / 2f - st.Width / 2f - st.Outline;
                if (this.frac <= 0f || radius <= 0f) return;
                Shapes.Ring(ctx.painter2D, r.center, radius, st, -90f, 360f * this.frac);
            };
        }
    }

    /// <summary>GG-T892: the Heavy badge (Shapes, GG-T735): an ink disc holding an iron kettlebell, its handle a ring arc
    /// over a round steel body with a lighter sheen, so it reads as weight at pip size.</summary>
    public sealed class WeightBadge : VisualElement
    {
        private static readonly Color Iron = Palette.Vfx.Iron, Sheen = Palette.Vfx.Sheen;

        public WeightBadge()
        {
            style.position = Position.Absolute;
            generateVisualContent += ctx =>
            {
                var r = contentRect;
                float size = Mathf.Min(r.width, r.height);
                if (size <= 0f) return;
                var p = ctx.painter2D;
                var c = r.center;
                var small = ShapeStyle.Small;
                Shapes.Dot(p, c, size * 0.5f - small.Outline, small.Tint(Palette.Ink.WithA(0.85f)));
                var body = c + new Vector2(0f, size * 0.1f);
                float br = size * 0.24f;
                Shapes.Ring(p, c - new Vector2(0f, size * 0.08f), size * 0.16f, small.Thick(Mathf.Max(3f, size * 0.09f)).Tint(Iron), 180f, 180f);
                Shapes.Dot(p, body, br, small.Tint(Iron));
                Shapes.Dot(p, body + new Vector2(-br * 0.35f, -br * 0.35f), br * 0.28f, small.Tint(Sheen).Outlined(Sheen, 0f));
            };
        }
    }

    /// <summary>Vector overlay: pincer lines, projectile trails, line-charge telegraphs.</summary>
    public sealed class LineLayer : VisualElement
    {
        public struct Seg { public Vector2 A, B; public Color Color; public float Width; }
        public readonly List<Seg> Segs = new List<Seg>();
        public readonly List<(Vector2 c, float r, Color color)> Dots = new List<(Vector2, float, Color)>();
        public readonly List<(Vector2 c, float r, Color color, float w)> Rings = new List<(Vector2, float, Color, float)>();
        /// <summary>Rounded outlines drawn around a group of tiles; <c>f</c> is how much of the border has drawn itself (0..1).</summary>
        public readonly List<(Rect r, Color color, float w, float f)> Outlines = new List<(Rect, Color, float, float)>();

        /// <summary>GG-T736: board indicators drawn as <see cref="Shapes"/> (white-bodied, ink-outlined vector shapes).</summary>
        public enum ShapeKind { Arrow, PathArrow, Chevron, Bar }
        public struct ShapeEntry
        {
            public ShapeKind Kind;
            /// <summary>Arrow/Bar: [from, to]; PathArrow: the whole path, tip last.</summary>
            public Vector2[] Path;
            /// <summary>Chevron only: centre, pointing direction and size.</summary>
            public Vector2 Center, Dir;
            public float Size;
            /// <summary>Arrow only: px trimmed off the tail and the tip along the path.</summary>
            public float Inset;
            public ShapeStyle Style;
        }
        public readonly List<ShapeEntry> ShapeList = new List<ShapeEntry>();

        public LineLayer() { generateVisualContent += Draw; }

        public void ClearLines() { Segs.Clear(); Dots.Clear(); Rings.Clear(); Outlines.Clear(); ShapeList.Clear(); MarkDirtyRepaint(); }
        /// <summary>A right-angle arrow (GG-T1833: straight, or an L round one corner) from <paramref name="from"/> with its tip at
        /// <paramref name="to"/>, trimmed <paramref name="inset"/> px at both ends.</summary>
        public void Arrow(Vector2 from, Vector2 to, ShapeStyle s, float inset = 0f) { ShapeList.Add(new ShapeEntry { Kind = ShapeKind.Arrow, Path = new[] { from, to }, Style = s, Inset = inset }); MarkDirtyRepaint(); }
        /// <summary>An arrow along a polyline (a tile route, rounded at its corners) with its head at the last point.</summary>
        public void PathArrow(IList<Vector2> path, ShapeStyle s) { ShapeList.Add(new ShapeEntry { Kind = ShapeKind.PathArrow, Path = new List<Vector2>(path).ToArray(), Style = s }); MarkDirtyRepaint(); }
        public void Chevron(Vector2 center, Vector2 dir, float size, ShapeStyle s) { ShapeList.Add(new ShapeEntry { Kind = ShapeKind.Chevron, Center = center, Dir = dir, Size = size, Style = s }); MarkDirtyRepaint(); }
        /// <summary>A round-capped bar (a link with no head) from <paramref name="from"/> to <paramref name="to"/>.</summary>
        public void Bar(Vector2 from, Vector2 to, ShapeStyle s) { ShapeList.Add(new ShapeEntry { Kind = ShapeKind.Bar, Path = new[] { from, to }, Style = s }); MarkDirtyRepaint(); }
        public void Outline(Rect r, Color color, float w, float f) { Outlines.Add((r, color, w, f)); MarkDirtyRepaint(); }
        public void Ring(Vector2 c, float r, Color color, float w) { Rings.Add((c, r, color, w)); MarkDirtyRepaint(); }
        public void Add(Vector2 a, Vector2 b, Color c, float w) { Segs.Add(new Seg { A = a, B = b, Color = c, Width = w }); MarkDirtyRepaint(); }
        public void Dot(Vector2 c, float r, Color color) { Dots.Add((c, r, color)); MarkDirtyRepaint(); }

        private void Draw(MeshGenerationContext ctx)
        {
            var p = ctx.painter2D;
            p.lineCap = LineCap.Round;
            foreach (var s in Segs)
            {
                p.strokeColor = s.Color;
                p.lineWidth = s.Width;
                p.BeginPath();
                p.MoveTo(s.A);
                p.LineTo(s.B);
                p.Stroke();
            }
            foreach (var (rect, color, w, f) in Outlines)
            {
                if (f <= 0f) continue;
                // GG-T736: a Shapes frame (tinted body, ink border) that draws itself clockwise from top-centre.
                var r = new Rect(rect.xMin + 5f, rect.yMin + 5f, Mathf.Max(0f, rect.width - 10f), Mathf.Max(0f, rect.height - 10f));
                float rad = Mathf.Min(30f, r.width * 0.5f, r.height * 0.5f);
                float perim = 2f * (r.width + r.height) - 8f * rad + 2f * Mathf.PI * rad;
                float start = perim > 0f ? (r.width * 0.5f - rad) / perim : 0f;
                var style = ShapeStyle.Indicator.Tint(color).Thick(w);
                style.Line = style.Line.WithA(style.Line.a * color.a);
                Shapes.RoundedRect(p, r, rad, style, start: start, portion: f);
            }
            foreach (var e in ShapeList)
            {
                switch (e.Kind)
                {
                    case ShapeKind.Arrow: Shapes.OrthoArrow(p, e.Path[0], e.Path[1], e.Style, insetStart: e.Inset, insetEnd: e.Inset); break;
                    case ShapeKind.PathArrow: Shapes.OrthoPathArrow(p, e.Path, e.Style); break;
                    case ShapeKind.Chevron: Shapes.Chevron(p, e.Center, e.Dir, e.Size, e.Style); break;
                    case ShapeKind.Bar: Shapes.Bar(p, e.Path[0], e.Path[1], e.Style); break;
                }
            }
            foreach (var (c, r, color, w) in Rings)
            {
                p.strokeColor = color;
                p.lineWidth = w;
                p.BeginPath();
                p.Arc(c, r, Angle.Degrees(0f), Angle.Degrees(360f));
                p.Stroke();
            }
            foreach (var (c, r, color) in Dots)
            {
                p.fillColor = color;
                p.BeginPath();
                p.Arc(c, r, Angle.Degrees(0f), Angle.Degrees(360f));
                p.Fill();
            }
        }
    }

    /// <summary>
    /// GG-T219: a trail renderer for moving units (Terra Battle / Friends &amp; Dragons): a fast streak as wide as the
    /// token, drawn under it, following its real free-hand position. It is never anchored: points age out in
    /// <see cref="PointLife"/> seconds, so a faster or longer move draws a longer streak, and it catches up as soon
    /// as the unit stops. The tail keeps nearly full width and fades out by age rather than tapering to a point.
    /// </summary>
    public sealed class TrailLayer : VisualElement
    {
        private struct Pt { public Vector2 P; public float T; }
        private sealed class Trail
        {
            public VisualElement Source;
            public readonly List<Pt> Points = new List<Pt>();
            public Color Color;
            public float Width;
            public bool Following = true;
        }

        public const float PointLife = 0.15f;
        private const float MinVertexDistance = 4f;
        private readonly List<Trail> trails = new List<Trail>();
        private IVisualElementScheduledItem ticker;

        public TrailLayer() { generateVisualContent += Draw; pickingMode = PickingMode.Ignore; }

        /// <summary>Starts (or keeps) a streak behind <paramref name="source"/>, a unit token on the board.</summary>
        public void Follow(VisualElement source, Color color, float width = BoardView.Tile - 12f)
        {
            if (trails.Exists(t => t.Source == source && t.Following)) return;
            var tr = new Trail { Source = source, Color = color, Width = width };
            tr.Points.Add(new Pt { P = CenterOf(source), T = Time.unscaledTime });
            trails.Add(tr);
            if (ticker == null) ticker = schedule.Execute(Tick).Every(16);
            else ticker.Resume();
        }

        /// <summary>Stops sampling <paramref name="source"/>; what is left of its streak ages out on its own.</summary>
        public void Release(VisualElement source)
        {
            foreach (var t in trails)
                if (t.Source == source && t.Following) { Sample(t, Time.unscaledTime); t.Following = false; }
        }

        public void Clear() { trails.Clear(); MarkDirtyRepaint(); }

        private static Vector2 CenterOf(VisualElement e) =>
            new Vector2(e.resolvedStyle.left + BoardView.Tile / 2f, e.resolvedStyle.top + BoardView.Tile / 2f);

        private static void Sample(Trail t, float now)
        {
            if (t.Source.panel == null) { t.Following = false; return; }
            var p = CenterOf(t.Source);
            var last = t.Points.Count > 0 ? t.Points[t.Points.Count - 1].P : p + Vector2.one * MinVertexDistance * 2f;
            if ((p - last).sqrMagnitude >= MinVertexDistance * MinVertexDistance) t.Points.Add(new Pt { P = p, T = now });
        }

        private void Tick()
        {
            float now = Time.unscaledTime;
            foreach (var t in trails)
            {
                if (t.Following) Sample(t, now);
                t.Points.RemoveAll(q => now - q.T > PointLife);
            }
            trails.RemoveAll(t => !t.Following && t.Points.Count < 2);
            if (trails.Count == 0) ticker.Pause();
            MarkDirtyRepaint();
        }

        private void Draw(MeshGenerationContext ctx)
        {
            float now = Time.unscaledTime;
            var pts = new List<Vector2>();
            var fade = new List<float>();
            foreach (var t in trails)
            {
                pts.Clear(); fade.Clear();
                // Newest first: the token's own position leads, so the streak meets it with no gap. Each point fades
                // with its age, so a fast move stretches the streak and it catches up within PointLife of stopping.
                if (t.Following && t.Source.panel != null) { pts.Add(CenterOf(t.Source)); fade.Add(1f); }
                for (int i = t.Points.Count - 1; i >= 0; i--)
                    if (pts.Count == 0 || (pts[pts.Count - 1] - t.Points[i].P).sqrMagnitude > 1f)
                    {
                        pts.Add(t.Points[i].P);
                        fade.Add(Mathf.Clamp01(1f - (now - t.Points[i].T) / PointLife));
                    }
                if (pts.Count < 2) continue;
                Ribbon(ctx, pts, fade, t.Width, t.Color, 0.6f);
                Ribbon(ctx, pts, fade, t.Width * 0.45f, t.Color, 0.75f);
            }
        }

        /// <summary>One triangle strip along <paramref name="pts"/> (newest first); <paramref name="fade"/> scales alpha and narrows the tail.</summary>
        private static void Ribbon(MeshGenerationContext ctx, List<Vector2> pts, List<float> fade, float width, Color color, float alpha)
        {
            int n = pts.Count;
            var verts = new Vertex[n * 2];
            var idx = new ushort[(n - 1) * 6];
            for (int i = 0; i < n; i++)
            {
                // UI Toolkit culls back faces: this tangent keeps the strip's winding front-facing for newest-first points.
                var tan = (pts[Mathf.Min(n - 1, i + 1)] - pts[Mathf.Max(0, i - 1)]).normalized;
                var normal = new Vector2(-tan.y, tan.x);
                // The tail stays nearly full width and fades out as a whole, never tapering to a point.
                float half = width * Mathf.Lerp(0.88f, 1f, fade[i]) * 0.5f;
                var c = color.WithA(color.a * alpha * fade[i] * fade[i]);
                var l = pts[i] - normal * half;
                var r = pts[i] + normal * half;
                verts[i * 2] = new Vertex { position = new Vector3(l.x, l.y, Vertex.nearZ), tint = c };
                verts[i * 2 + 1] = new Vertex { position = new Vector3(r.x, r.y, Vertex.nearZ), tint = c };
            }
            for (int i = 0; i < n - 1; i++)
            {
                ushort l0 = (ushort)(i * 2), r0 = (ushort)(i * 2 + 1), l1 = (ushort)(i * 2 + 2), r1 = (ushort)(i * 2 + 3);
                int k = i * 6;
                idx[k] = l0; idx[k + 1] = l1; idx[k + 2] = r0;
                idx[k + 3] = r0; idx[k + 4] = l1; idx[k + 5] = r1;
            }
            var mesh = ctx.Allocate(verts.Length, idx.Length);
            mesh.SetAllVertices(verts);
            mesh.SetAllIndices(idx);
        }
    }

    /// <summary>
    /// GG-T97: running tally of the board's drawn tokens against the rule "two actors never share a tile once a slide is
    /// over". BattleScreen feeds it after every beat (tokens unique), as playback settles (tokens match the state, before
    /// the safety-net snap hides a miss) and after that snap (must always be clean). AutomationServer serves it at
    /// /state → board_audit, and tools/harness.py boardaudit fails a run on any nonzero count.
    /// </summary>
    public static class BoardAudit
    {
        public static int Actions, Beats, BeatOverlaps, SettleDrift, SyncedProblems, TileConflicts;
        public static readonly List<string> Recent = new List<string>();

        public static void Beat(List<string> problems, BattleEvent after)
        {
            Beats++;
            if (problems.Count == 0) return;
            BeatOverlaps++;
            Note("beat after " + after, problems);
        }

        public static void Settled(List<string> problems)
        {
            Actions++;
            if (problems.Count == 0) return;
            SettleDrift++;
            Note("playback settle", problems);
        }

        public static void Synced(List<string> problems)
        {
            if (problems.Count == 0) return;
            SyncedProblems++;
            Note("after sync", problems);
        }

        public static void Conflict(string what)
        {
            TileConflicts++;
            Note("engine", new List<string> { what });
        }

        private static void Note(string where, List<string> problems)
        {
            var line = "[BoardAudit] " + where + ": " + string.Join("; ", problems);
            Debug.LogError(line);
            Recent.Add(line.Length > 400 ? line.Substring(0, 400) : line);
            while (Recent.Count > 20) Recent.RemoveAt(0);
        }

        public static void Reset()
        {
            Actions = Beats = BeatOverlaps = SettleDrift = SyncedProblems = TileConflicts = 0;
            Recent.Clear();
        }

        public static object Snapshot() => new { actions = Actions, beats = Beats, beat_overlaps = BeatOverlaps, settle_drift = SettleDrift, synced = SyncedProblems, tile_conflicts = TileConflicts, recent = Recent.ToArray() };
    }
}
