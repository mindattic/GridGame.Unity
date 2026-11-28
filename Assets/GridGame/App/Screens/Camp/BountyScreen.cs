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
    /// <summary>Hunts for marked targets: collect what was slain (with a claim cue), track what is hunted, and take a fresh
    /// poster. Each poster shows the target's portrait under the bounty skull, its title, its kind, the clue naming its
    /// stage, and the reward.</summary>
    public sealed class BountyScreen : HubPage
    {
        public override string Id => "bounties";
        protected override string Title => "Bounties";
        /// <summary>The Tome a just-claimed bounty paid (GG-T301), offered for teaching at the top of the board.</summary>
        private string claimedTome;

        protected override void BuildBody(VisualElement body)
        {
            if (C.TryItem(claimedTome) is ItemDef paid && S.Count(paid.Id) > 0)
            {
                var tw = Ui.Panel(body, $"Reward: {paid.Name}", false, "bounty-tome");
                TomeUi.Offer(tw, paid, r => { if (r.Ok) claimedTome = null; });
            }
            var list = Ui.ListPanel(body, "Wanted", "bounty-board");
            Ui.Para("Hunt a marked foe: play the stage its clue names, kill the one wearing the skull, then collect here.", list, "dim");
            var board = Bounties.Board(C, S);
            if (board.Count == 0) Ui.Empty("No one is posted yet. Clear more stages.", list);
            foreach (var b in board) Poster(b, list);
        }

        private void Poster(BountyDef b, VisualElement list)
        {
            var bounty = b;
            var target = C.TryClass(b.TargetClass);
            var reward = C.TryItem(b.RewardItem);
            bool slain = Bounties.IsSlain(S, b.Id), hunting = Bounties.IsTaken(S, b.Id);
            var card = Ui.Card(list, null, null, out var col, "poster-" + b.Id);
            var face = Ui.El(null, null);
            card.Insert(0, face);
            face.style.position = Position.Relative;
            if (target != null) Ui.Thumb(target.Id, face, Kit.Thumb);
            var skull = BountySkull.Make(Size.IconM, face);
            skull.style.position = Position.Absolute;
            skull.style.right = Space.S16; skull.style.top = -Space.S4;
            // GG-T1881: the poster's one action sits on its right edge beside the name, not on a row of its own.
            var acts = Ui.El("row", col);
            var title = Ui.Name(b.Name, acts);
            title.style.flexShrink = 1; title.style.minWidth = 0; title.style.whiteSpace = WhiteSpace.Normal;
            Ui.El("grow", acts);
            Ui.Caption($"{target?.Name ?? b.TargetClass} · tougher variant", col, "dim");
            Ui.Para(Bounties.Clue(C, b), col, "gold");
            int sp = Bounties.SkillPointsPaid(b);
            Ui.Para($"Reward {Ui.Gold(Bounties.RewardGold(C, b))}{(reward != null ? " + " + reward.Name : "")}{(sp > 0 ? $" + {sp} SP each" : "")}", col, "dim");
            TomeUi.Preview(col, reward); // GG-T301: what a Tome reward teaches
            if (slain)
            {
                var claim = RowIcon.IconButton("Claim", RowKind.Confirm, () => // GG-T750 (§26.7)
                {
                    claimedTome = reward?.Type == ItemType.Tome ? reward.Id : null;
                    var r = Bounties.Claim(C, S, bounty.Id);
                    Done(r);
                    if (r.Ok) App.Audio.Sfx("battle.pickup"); // the claim cue: the purse fills
                }, "btn--primary btn--small", acts);
                claim.name = "bounty-claim-" + b.Id;
            }
            else if (hunting)
            {
                var stop = RowIcon.IconButton("Hunting", RowKind.Close, () => Confirm.Show($"Stop hunting {bounty.Name}?", "Stop", () => Done(Bounties.Abandon(C, S, bounty.Id))), "btn--small", acts);
                stop.name = "bounty-abandon-" + b.Id;
            }
            else
            {
                var take = RowIcon.IconButton("Take", RowKind.Confirm, () => Done(Bounties.Take(C, S, bounty.Id)), "btn--small", acts);
                take.name = "bounty-" + b.Id;
            }
        }
    }
}
