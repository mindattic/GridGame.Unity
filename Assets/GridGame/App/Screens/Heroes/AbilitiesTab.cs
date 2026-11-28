using System;
using System.Collections.Generic;
using System.Linq;
using GridGame.Core.Battle;
using GridGame.Core.Content;
using GridGame.Core.Rules;
using GridGame.Meta;
using UnityEngine;
using UnityEngine.UIElements;

namespace GridGame.App
{
    /// <summary>The Hero screen's Abilities tab (§25.6, GG-T435): the battle bar over the Known / Items / Passives /
    /// Locked sub-tabs, plus Board — the skill hexagon (GG-T1079: the gold Learn sub-tab is gone; abilities come from the board).</summary>
    internal sealed partial class AbilitiesTab : HeroTab
    {
        private int slot;
        private int tab = -1;
        private readonly SkillHexTab talents = new SkillHexTab();
        /// <summary>The Board sub-tab's index (after Known, Items, Passives, Locked): the skill hexagon (GG-T1056),
        /// in place of the old Talents skill trees; its id stays "talents".</summary>
        public const int TalentsSub = 4;
        private static readonly string[] SubTabs = { "Known", "Items", "Passives", "Locked", "Talents" };

        public AbilitiesTab(int subTab = -1, (int Q, int R)? boardNode = null) { tab = subTab; talents.Select(boardNode); }
        public override void OnHeroChanged() => talents.OnHeroChanged();
        /// <summary>"talents" while the Talents sub-tab is up, so `-start talents` and the harness's goto check still match.</summary>
        public override string Id => tab == TalentsSub ? talents.Id : "abilities";
        /// <summary>GG-T180/GG-T1086: while onboarding step 7 is ahead, the Board sub-tab (where the guided lesson sits)
        /// carries the training tip; the other sub-tabs keep the plain "abilities" tip.</summary>
        public override string TipStation => tab == TalentsSub ? (Training.Step(S) ? Training.TipStation : null) : "abilities";

        /// <summary>GG-T180 (§25.0.9), GG-T1086: during step 7 the tab opens on the Board, on the hero the guided first skill
        /// is for — decided before the hero picker draws, so the picker marks that hero. GG-T1407: an open Board marks its
        /// hero's ready ability seen, so its "New ability ready" badge is already gone when the picker draws.</summary>
        public override void Prepare()
        {
            if (tab < 0)
            {
                var plan = Training.First(C, S);
                tab = plan != null ? TalentsSub : 0;
                if (plan != null) HeroId = plan.Hero.ClassId;
            }
            if (tab == TalentsSub && NextAbility.MarkSeen(C, Hero)) App.Persist();
        }

        public override void BuildBody(VisualElement body)
        {
            Prepare();
            if (tab == TalentsSub)
            {
                Ui.Tabs(SubTabs, tab, i => { tab = i; Refresh(); }, body).name = "abilities-tabs";
                var plan = Training.First(C, S);
                if (plan != null && plan.Hero == Hero) GuidedLesson(plan, body); // GG-T1086: above the hexagon
                talents.Host = Host;
                talents.BuildBody(body);
                return;
            }
            var h = Hero;
            int unlocked = Progression.UnlockedSlots(S);
            var barW = Ui.Panel(body, "Battle bar", false, "abilities-bar");
            var bar = Ui.El("row", barW);
            // Edge = row padding + cell margin = gap between cells = 24 (POL-C8 equal gutters; POL-C3 24 px between targets).
            Ui.Pad(bar, left: Space.TargetHalfGap, right: Space.TargetHalfGap);
            for (int i = 0; i < Progression.MaxBarSlots; i++)
            {
                int idx = i;
                // R3: a kit card; the picked slot is pressed in rather than ringed in gold.
                var cell = Ui.El("card card-tap" + (i == slot ? " tap--selected" : ""), bar);
                cell.style.flexDirection = FlexDirection.Column;
                cell.style.alignItems = Align.Center;
                cell.style.justifyContent = Justify.Center;
                // Every bar slot shares the row with even gutters, however many slots or however wide the stage (POL-C8).
                cell.style.flexGrow = 1; cell.style.flexBasis = 0; cell.style.minHeight = Size.Cell; // GG-T1145: min, not fixed: a long name wraps to 3 lines and grows the cell instead of spilling onto the Supporter line
                Ui.Margin(cell, left: Space.TargetHalfGap, right: Space.TargetHalfGap);
                if (i >= unlocked)
                {
                    Ui.Margin(cell.KindIcon(RowKind.Locked), right: Space.S0);
                    Ui.Caption("Locked", cell, "dim");
                    cell.style.opacity = Dim.Row;
                    continue;
                }
                var entry = h.Bar[i];
                var label = EntryName(entry);
                Ui.Margin(cell.KindIcon(RowKind.Ability, EntryIcon(entry)), right: Space.S0); // GG-T750 (§26.7): an empty slot shows the Ability emblem
                if (label == null) { var t = Ui.Caption("Empty", cell, "dim"); t.style.unityTextAlign = TextAnchor.MiddleCenter; }  // GG-T1874: filled slots are icon-only (as in battle, GG-T1819); the full name is under the bar
                InspectCard.Attach(cell, b => InspectEntry(b, entry));
                Ui.Tappable(cell, () => { slot = idx; Refresh(); });
            }
            var slotName = slot < unlocked ? EntryName(h.Bar[slot]) : null;
            bool swap = slotName != null && h.Bar[slot].StartsWith("weapon:");
            Ui.Para(slotName == null ? $"Slot {slot + 1}: empty" : $"Slot {slot + 1}: {slotName}", swap ? RowIcon.Line("battle-weapon-swap", barW) : barW, "gold").name = "abilities-slot-name";
            // GG-T298: what this hero does when it joins a pincer as a supporter.
            var support = GlossaryText.Create(GridGame.Core.Rules.SupportText.Describe(C, C.Class(h.ClassId).Support), "kit-text gold", barW);
            support.name = "abilities-support";
            Ui.Para("Pick a slot, then choose what goes in it — or move it along the bar.", barW, "dim");
            var tools = Ui.Actions(barW);
            var left = RowIcon.IconButton("Move left", RowKind.Back, () => { var r = Abilities.Swap(S, h, slot, slot - 1); if (r.Ok) slot--; Done(r); }, "btn--small", tools);
            left.name = "bar-move-left";
            Ui.SetEnabled(left, slot > 0 && slot < unlocked);
            var right = RowIcon.IconButton("Move right", RowKind.Enter, () => { var r = Abilities.Swap(S, h, slot, slot + 1); if (r.Ok) slot++; Done(r); }, "btn--small", tools);
            right.name = "bar-move-right";
            Ui.SetEnabled(right, slot + 1 < unlocked);
            RowIcon.IconButton("Arrange sheet", RowKind.Menu, OpenTree, "btn--small", tools).name = "abtree-open"; // GG-T2159: the battle Abilities sheet's folders, order and pins

            Ui.Tabs(SubTabs, tab, i => { tab = i; Refresh(); }, body).name = "abilities-tabs";
            var list = Ui.List(Ui.Panel(body, fill: true)).contentContainer;
            if (tab == PassivesTab) { Passives(h, list); return; }
            if (tab == LockedTab) { Locked(h, list); return; }
            if (slot < unlocked && h.Bar[slot] != null) Ui.MenuRow("Clear slot", "", () => Done(Abilities.Assign(C, S, h, slot, null)), list).KindIcon(RowKind.Close);
            if (tab == 0)
            {
                // GG-T1043: the bar picks from the abilities the hero owns (known, or on its owned skill hexagon nodes) —
                // only the ones it can fire (skills and spells); passives work from the Passives tab, not a slot.
                foreach (var id in HeroBoard.OwnedAbilities(C, h))
                {
                    var a = C.TryAbility(id);
                    if (a == null || !Abilities.IsHeroUsable(a)) continue;
                    var row = Ui.MenuRow(a.Name, KindLabel(a), () => Done(Abilities.Assign(C, S, h, slot, a.Id)), list, null, slot < unlocked);
                    row.KindIcon(RowKind.Ability, Art.AbilityIcon(a));
                    if (a.Cost > 0) Ui.CostPips(a.Cost, row);
                    InspectCard.Attach(row, b => InspectCard.Ability(b, a, wearer: C.TryClass(h.ClassId)));
                    Describe(a, list);
                }
            }
            else if (tab == 1)
            {
                var items = S.Inventory.Where(kv => kv.Value > 0).Select(kv => C.TryItem(kv.Key)).Where(i => i != null && (i.Type == ItemType.Consumable || i.Slot == EquipmentSlot.Weapon)).ToList();
                if (items.Count == 0) Ui.Empty("No battle items. Buy potions from the Merchant.", list);
                foreach (var it in items)
                {
                    var item = it;
                    bool weapon = item.Slot == EquipmentSlot.Weapon;
                    ItemUi.Row(item, weapon ? "swap in battle" : $"×{S.Count(item.Id)}", () => Done(Abilities.Assign(C, S, h, slot, (weapon ? "weapon:" : "item:") + item.Id)), list, slot < unlocked);
                }
            }
        }

        private const int PassivesTab = 2;
        private const int LockedTab = 3;

        /// <summary>A skill this hero lacks and where it comes from (GG-T1081: the talent-tree sources are gone).</summary>
        private sealed class LockedSkill
        {
            public AbilityDef Ability;
            public bool Kit;
            public List<string> Sources = new List<string>();
        }

        private List<LockedSkill> LockedSkills(HeroSave h)
        {
            var result = new List<LockedSkill>();
            var def = C.TryClass(h?.ClassId);
            if (def == null || h.Domesticated) return result;
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            void Add(string id, bool kit)
            {
                var a = C.TryAbility(id);
                if (a == null || h.Known.Contains(a.Id) || PassiveLoadout.IsInnate(C, h, a.Id) || !seen.Add(a.Id)) return;
                var sources = new List<string>();
                foreach (var tome in C.Items.Values.Where(i => i.Type == ItemType.Tome && string.Equals(i.TeachesAbility, a.Id, StringComparison.OrdinalIgnoreCase)))
                    sources.Add($"{tome.Name}: {string.Join(", ", TomeFoundAt(tome.Id).DefaultIfEmpty("not found anywhere yet"))}");
                var boardPt = SkillHexProgress.Board(C)?.FindAbility(a.Id);
                if (boardPt != null) sources.Add($"Talents - {SkillHexProgress.Board(C).Price(boardPt.Value)} SP");
                if (sources.Count == 0) sources.Add("Not taught anywhere yet");
                result.Add(new LockedSkill { Ability = a, Kit = kit, Sources = sources });
            }
            foreach (var id in Progression.KitAbilities(C, def)) Add(id, true);
            foreach (var id in def.Abilities) Add(id, false);
            foreach (var item in C.Items.Values.Where(i => i.Type == ItemType.Tome && i.TeachesAbility != null).OrderBy(i => i.Id, StringComparer.Ordinal))
            {
                var a = C.TryAbility(item.TeachesAbility);
                if (a != null && RewardCatalog.AllowedFor(a, def)) Add(a.Id, false);
            }
            return result;
        }

        /// <summary>Where an item turns up: a quest or bounty reward, a theme's first clear, the Merchant, a foe's drops or a chest.</summary>
        private List<string> TomeFoundAt(string itemId)
        {
            var found = new List<string>();
            foreach (var q in C.QuestOrder) if (q.RewardItem == itemId) found.Add($"quest {q.Name}");
            foreach (var b in C.Bounties.Values.OrderBy(b => b.Id, StringComparer.Ordinal)) if (b.RewardItem == itemId) found.Add($"bounty {b.Name}");
            foreach (var t in C.Campaign.Themes) if (t.MilestoneReward == itemId) found.Add($"clearing {t.Name}");
            if (C.TryItem(itemId)?.SoldByVendor == true) found.Add("the Merchant");
            var tables = new HashSet<string>(C.DropTables.Values.Where(t => t.Entries.Any(e => e.Item == itemId)).Select(t => t.Id), StringComparer.OrdinalIgnoreCase);
            var foes = C.Classes.Values.Where(d => d.Team == Team.Enemy && d.DropTable != null && tables.Contains(d.DropTable))
                .Select(d => d.Name).Distinct().OrderBy(n => n, StringComparer.Ordinal).ToList();
            if (foes.Count > 0) found.Add("dropped by " + string.Join(", ", foes.Take(3)) + (foes.Count > 3 ? $" and {foes.Count - 3} more" : ""));
            foreach (var chest in C.Items.Values.Where(i => i.Type == ItemType.Chest && i.OpensTable != null && tables.Contains(i.OpensTable)).OrderBy(i => i.Id, StringComparer.Ordinal))
                found.Add($"a {chest.Name}");
            return found;
        }

        /// <summary>The Locked tab (GG-T388): every skill this hero doesn't know yet that belongs to it — its own kit moves,
        /// the rest of its class list, its home tree and the passives a Tome could teach — each with where it comes from
        /// (the trainer's price, the tree node and its points, the Tome and where that Tome is found).</summary>
        private void Locked(HeroSave h, VisualElement list)
        {
            var locked = LockedSkills(h);
            Ui.Para("A hero starts with two moves. The rest are bought from the trainer (Learn) or in Talents, or discovered as Tomes.", list, "dim");
            if (locked.Count == 0) Ui.Empty("Nothing left to learn.", list);
            foreach (var l in locked)
            {
                var ab = l.Ability;
                var row = Ui.MenuRow(ab.Name, l.Kit ? "Own kit" : Abilities.IsHeroUsable(ab) ? KindLabel(ab) : "Passive", null, list);
                row.KindIcon(RowKind.Ability, Art.AbilityIcon(ab));
                row.name = "locked-" + ab.Id;
                if (l.Kit) row.Q<Label>("value")?.AddToClassList("gold");
                InspectCard.Attach(row, b => InspectCard.Ability(b, ab, wearer: C.TryClass(h.ClassId)), tap: true); // GG-T450: a read-only row reads on a tap
                foreach (var src in l.Sources) Ui.Note("· " + src, list, null);
                Describe(ab, list);
            }
        }

        /// <summary>The Passives tab (GG-T301, §25.6.4): learned passives (Tomes, skill-tree grants) with an On/Off switch,
        /// at most <see cref="PassiveLoadout.Slots"/> on; the class kit's innate ones, always on; and any Tome in the bag
        /// this hero could read.</summary>
        private void Passives(HeroSave h, VisualElement list)
        {
            var learned = PassiveLoadout.Learned(C, h);
            var on = PassiveLoadout.Active(C, h);
            int slots = PassiveLoadout.Slots(h), next = PassiveLoadout.NextSlotLevel(h);
            var head = Ui.Section($"Learned passives on: {on.Count}/{slots}" + (next > 0 ? $"   ·   another slot at Lv {next}" : ""), list);
            head.name = "passive-slots";
            Ui.Para("Tap one to switch it on or off. Tomes come from quests, bounties, chests, theme clears and rare drops.", list, "dim");
            if (learned.Count == 0) Ui.Empty("No learned passives yet.", list);
            foreach (var a in learned)
            {
                var ab = a;
                bool active = on.Contains(a.Id);
                var row = Ui.MenuRow(a.Name, active ? "On" : "Off", () => Done(PassiveLoadout.Toggle(C, h, ab.Id)), list);
                row.KindIcon(RowKind.Ability, Art.AbilityIcon(a));
                row.name = "passive-" + a.Id;
                row.Q<Label>("value").AddToClassList(active ? "good" : "dim");
                InspectCard.Attach(row, b => InspectCard.Ability(b, ab, wearer: C.TryClass(h.ClassId)));
                Sentence(a, list);
            }
            var innate = PassiveLoadout.Innate(C, h);
            if (innate.Count > 0)
            {
                Ui.Section("Class passives — always on", list);
                foreach (var a in innate)
                {
                    var ab = a;
                    var row = Ui.MenuRow(a.Name, "Innate", null, list);
                    row.KindIcon(RowKind.Ability, Art.AbilityIcon(a));
                    InspectCard.Attach(row, b => InspectCard.Ability(b, ab, wearer: C.TryClass(h.ClassId)), tap: true); // GG-T450
                    Sentence(a, list);
                }
            }
            var tomes = S.Inventory.Where(kv => kv.Value > 0).Select(kv => C.TryItem(kv.Key))
                .Where(i => i != null && i.Type == ItemType.Tome && Tomes.WhyNot(C, S, i.Id, h.ClassId) == null).ToList();
            if (tomes.Count > 0) Ui.Section("Tomes in the bag", list);
            foreach (var t in tomes)
            {
                var tome = t;
                var row = ItemUi.Row(t, "Read", () =>
                {
                    var r = Tomes.Teach(C, S, tome.Id, h.ClassId);
                    App.Toast(r.Message, 2.4f);
                    App.Audio.Sfx(r.Ok ? "battle.powerup" : "ui.error");
                    if (r.Ok) App.Persist();
                    Refresh();
                }, list);
                row.name = "read-" + t.Id;
                TomeUi.Preview(list, t);
            }
        }

        private void Sentence(AbilityDef a, VisualElement list) => GlossaryText.Create(TriggerText.Describe(C, a), "kit-text dim", list).AddToClassList("kit-note"); // on the box, not each word

        /// <summary>The guided first skill (GG-T180, §25.0.9) heads the Board sub-tab (GG-T1086): what it does, that its board
        /// node is free (no skill points) and which bar slot it lands in, with a Learn button (the camp tip's target) that
        /// buys the node and slots the skill in one tap (<see cref="Training.Teach"/>).</summary>
        private void GuidedLesson(Training.Plan plan, VisualElement list)
        {
            var card = Ui.Panel(list, null, false, "guided-lesson");
            var head = Ui.El("row", card);
            head.style.alignItems = Align.Center;
            head.KindIcon(RowKind.Ability, Art.AbilityIcon(plan.Skill));
            Ui.Heading($"{plan.Skill.Name} — {KindLabel(plan.Skill)}", head, "grow");
            GlossaryText.Create(plan.Skill.Description ?? "", "kit-text dim", card);
            string hero = C.TryClass(plan.Hero.ClassId)?.Name ?? plan.Hero.ClassId;
            Ui.Para($"First lesson free — the {hero} takes this Talents node at no skill point cost, and it goes onto the battle bar" + (plan.Slot >= 0 ? $", slot {plan.Slot + 1}." : "."), card, "gold");
            var btn = RowIcon.IconButton("Learn", RowKind.Confirm, () => LearnDone(Training.Teach(C, S), plan.Hero, plan.Skill.Id), "btn--primary", Ui.Actions(card));
            btn.name = CampTips.CraftButton;
        }

        /// <summary>After learning, select the bar slot the skill went into (the guided first one is slotted automatically).</summary>
        private void LearnDone(Result r, HeroSave h, string abilityId)
        {
            if (r.Ok)
            {
                int at = h.Bar.IndexOf(abilityId);
                if (at >= 0) { slot = at; tab = 0; }
            }
            Done(r);
        }

        private void Describe(AbilityDef a, VisualElement list) => GlossaryText.Create(a.Description ?? "", "kit-text dim", list).AddToClassList("kit-note"); // on the box, not each word

        /// <summary>The hold-to-read card for a bar entry: an ability, a battle item or a swap weapon.</summary>
        private bool InspectEntry(VisualElement body, string entry)
        {
            if (entry == null) return false;
            if (entry.StartsWith("item:") || entry.StartsWith("weapon:"))
                return InspectCard.Item(body, C.TryItem(entry.Substring(entry.IndexOf(':') + 1)));
            return InspectCard.Ability(body, C.TryAbility(entry));
        }

        private static string KindLabel(AbilityDef a) => a.Kind == AbilityKind.Skill ? (a.CooldownTurns > 0 ? $"Skill · CD {a.CooldownTurns}" : "Skill") : a.CastTime > 0 ? FormattableString.Invariant($"{a.CastTime:0.#}s") : "Instant";

        private string EntryName(string entry)
        {
            if (entry == null) return null;
            if (entry.StartsWith("item:")) { var it = C.TryItem(entry.Substring(5)); return it == null ? null : $"{it.Name} ×{S.Count(it.Id)}"; }  // GG-T711: one line, shrunk to fit the bar cell by TextFit; it wraps (GG-T701) only below the type floor
            if (entry.StartsWith("weapon:")) { var w = C.TryItem(entry.Substring(7)); return w?.Name; }
            return C.TryAbility(entry)?.Name;
        }

        private Texture2D EntryIcon(string entry)
        {
            if (entry == null) return null;
            if (entry.StartsWith("item:")) return Art.ItemIcon(entry.Substring(5));
            if (entry.StartsWith("weapon:")) return Art.ItemIcon(entry.Substring(7));
            return Art.AbilityIcon(C.TryAbility(entry));
        }
    }
}
