using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GridGame.Core.Battle;
using GridGame.Core.Content;
using GridGame.Meta;
using UnityEngine;
using UnityEngine.UIElements;

namespace GridGame.App
{
    /// <summary>Base for hub sub-screens: background art, header with back + gold, body column.</summary>
    public abstract class HubPage : GameScreen
    {
        /// <summary>The header's currency strip (<see cref="Ui.Currencies"/>, GG-T1361).</summary>
        protected VisualElement CurrencyBar;
        protected VisualElement Body;
        protected abstract string Title { get; }
        protected virtual string Backdrop => "greenvalley";
        /// <summary>Optional line shown under the header title (e.g. station level + next unlock, GG-T35).</summary>
        protected virtual string Subtitle => null;
        /// <summary>The automation name the subtitle label takes (default "header-sub").</summary>
        protected virtual string SubtitleName => null;
        /// <summary>True: the subtitle is the first line of the page body, not under the title, so a long line does not squeeze the header's title and currencies (GG-T1859).</summary>
        protected virtual bool SubtitleInBody => false;
        /// <summary>The Camp NPC who keeps this station (GG-T119), or null: the header gets a Talk button for them.</summary>
        protected virtual string Npc => null;
        /// <summary>The camp tip this page shows (GG-T173): its own Id, unless a tab has its own tip (the Blacksmith's Forge, GG-T177).</summary>
        protected virtual string TipStation => Id;
        public override bool ShowsNav => true;

        protected override void Build(VisualElement root)
        {
            var bg = Ui.El("fill", root);
            LivingBackdrop.Show(bg, Backdrop, 1);
            bg.style.unityBackgroundImageTintColor = Palette.Backdrop.Hub;
            var col = Ui.El("fill col", root);
            // GG-T1335: a page more than one step from the hub also gets Home, back to the hub in one fade.
            var header = Ui.Header(Title, () => App.Router.Back(), col, out CurrencyBar, SubtitleInBody ? null : Subtitle,
                ShowsHubChrome && App.Router.PagesAboveHub != 1 && App.Router.PagesAboveHub != 0 ? () => App.Router.Home() : (Action)null);
            if (SubtitleName != null && header.Q<Label>("header-sub") is Label subLabel) subLabel.name = SubtitleName;
            // Every Camp NPC can be talked to from their own station as well as from Camp's Talk row (GG-T119).
            Button talk = null;
            var topic = Npc != null && CampNpcs.Joined(S, Npc) ? DialogueRunner.TalkTopic(C, S, ContentLoader.NpcFlagPrefix + Npc) : null;
            if (topic != null)
            {
                talk = RowIcon.IconButton(S.SeenDialogues.Contains(topic.Id) ? "Talk" : "Talk!", RowKind.Talk, () => App.Router.Push(new DialogueScreen(topic, (string)null, (GameScreen)null)), "btn--small", null);
                talk.name = "npc-talk";
            }
            // GG-T454 (the menu kit): the kit's page body, the centred column with the kit's gutters (GG-T398).
            Body = Ui.PageBody(col);
            if (SubtitleInBody && Subtitle != null) Ui.Note(Subtitle, Body).name = SubtitleName ?? "header-sub";
            BuildBody(Body);
            Typography.Watch(Body);
            // GG-T1590: the hub's bottom nav under the body.
            if (ShowsHubChrome) Ui.NavBar(col, this);
            // GG-T1361: Gold, Shards and Summon Tickets on every hub page; tapping Shards opens the Shard Store.
            Ui.Currencies(CurrencyBar, App.Save, ShardsTap, talk, this is DiamondStoreScreen, this is SummonScreen);
            CampTipOverlay.ShowIfPending(this, TipStation);
        }

        /// <summary>Home and the bottom nav show on the hub's trail; the Shard Store (GG-T1871) also shows them with no hub under it.</summary>
        private bool ShowsHubChrome => App.Save != null && (App.Router.OnHubTrail || this is DiamondStoreScreen);

        /// <summary>What tapping Shards on the currency strip does: open the Shard Store, or nothing on it.</summary>
        protected virtual Action ShardsTap => this is DiamondStoreScreen ? null : () => App.Router.PushOrReturn(new DiamondStoreScreen());

        protected abstract void BuildBody(VisualElement body);

        /// <summary>Builds this page's body inside <paramref name="host"/>'s, as one of its tabs (GG-T1637).</summary>
        internal void BuildAsTab(HubPage host, VisualElement body)
        {
            TabHost = host;
            BuildBody(body);
        }

        /// <summary>Toasts the result and voices it: <paramref name="cue"/> on success (the coin by default; equip, repair
        /// and caravan pass their own, GG-T465), <c>ui.error</c> on a refusal.</summary>
        protected void Done(Result r, bool persist = true, string cue = "ui.buy")
        {
            App.Toast(r.Message ?? (r.Ok ? "Done" : "Can't do that"));
            App.Audio.Sfx(r.Ok ? cue : "ui.error");
            if (r.Ok && persist) App.Persist();
            Refresh();
        }

        protected ContentDb C => App.Content;
        protected SaveData S => App.Save;
    }
}
