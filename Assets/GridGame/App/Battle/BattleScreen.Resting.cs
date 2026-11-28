using System.Linq;
using GridGame.Core;
using GridGame.Core.Battle;
using UnityEngine;
using UnityEngine.UIElements;

namespace GridGame.App
{
    /// <summary>
    /// GG-T1954 (UX review #12): the recovery tray at the timeline's right end, collapsed to one "+N resting" kit chip.
    /// Recovering icons hide inside it (BattleScreen.Timeline places them); a tap lists who rests and when each returns.
    /// </summary>
    public sealed partial class BattleScreen
    {
        private VisualElement trayHost, restingChip;
        private Label restingLabel;
        private int restingShown = -1;

        /// <summary>GG-T1954: the tray's one control, a kit chip right-aligned at the stage edge and centred on the track, hidden
        /// while nobody rests. It never moves or pulses, so Reduce Motion changes nothing.</summary>
        private void BuildRestingChip(VisualElement frame)
        {
            restingShown = -1;
            trayHost = new VisualElement { name = "tl-tray", pickingMode = PickingMode.Ignore };
            trayHost.style.position = Position.Absolute;
            trayHost.style.width = TrayW;
            trayHost.style.top = TrackY - Size.Target / 2f; trayHost.style.height = Size.Target;
            trayHost.style.flexDirection = FlexDirection.Row;
            trayHost.style.justifyContent = Justify.FlexEnd;
            trayHost.style.alignItems = Align.Center;
            trayHost.style.display = DisplayStyle.None;
            frame.Add(trayHost);
            restingChip = Ui.Chip("", trayHost, onClick: TapRestingChip, name: "tl-resting");
            restingChip.style.marginLeft = restingChip.style.marginRight = restingChip.style.marginTop = restingChip.style.marginBottom = Space.S0;
            restingChip.style.maxWidth = TrayW;
            restingLabel = restingChip.Q<Label>();
            restingLabel.style.whiteSpace = WhiteSpace.Normal; // a large type scale wraps to two lines rather than cutting the count
            restingLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        }

        private void SyncRestingChip(int n)
        {
            if (trayHost == null || n == restingShown) return;
            restingShown = n;
            trayHost.style.display = n > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            if (n > 0) restingLabel.text = PanelCopy(PanelState.RestingChip, n: n);
        }

        /// <summary>A tap on the chip: in Examine mode the resting list fills the panel; otherwise it opens as a tap card, as a
        /// tap on any timeline diamond does.</summary>
        private void TapRestingChip()
        {
            if (State == null) return;
            if (!examining) { InspectCard.Show(restingChip, RestingCard); return; }
            ClearTapArrow();
            examineNote = null;
            examineCard = RestingCard;
            App.Audio.Sfx("ui.click");
            SyncPanel();
        }

        /// <summary>Who is resting off the bar, soonest back first: each one's thumbnail and the seconds until it rejoins.</summary>
        private bool RestingCard(VisualElement body)
        {
            if (State == null) return false;
            var rows = drawn.Values.Where(e => inTray.Contains(e.Id) && State.UnitById(e.OwnerId) is Unit o && o.Alive)
                .OrderBy(e => recoverView.TryGetValue(e.Id, out var r) ? r.left : int.MaxValue).ThenBy(e => e.Id).ToList();
            if (rows.Count == 0) return false;
            var head = Ui.Text(PanelCopy(PanelState.Resting), "small gold", body);
            head.name = "resting-head";
            head.style.whiteSpace = WhiteSpace.Normal;
            head.style.marginBottom = Space.S8;
            foreach (var e in rows)
            {
                var u = State.UnitById(e.OwnerId);
                float secs = recoverView.TryGetValue(e.Id, out var r) ? r.left / (float)BattleConfig.TicksPerSecond : 0f;
                var row = Ui.El("row", body);
                row.name = "resting-unit-" + u.Id;
                row.style.alignItems = Align.Center;
                row.style.marginBottom = Space.S8;
                var t = Ui.Thumb(u.Def.Id, row, Size.IconS);
                t.pickingMode = PickingMode.Ignore;
                t.style.marginRight = Space.S8;
                var line = Ui.Text(PanelCopy(PanelState.RestingRow, u.Name, extra: $"{secs:0.0}"), "small", row);
                line.style.whiteSpace = WhiteSpace.Normal;
                line.style.flexShrink = 1;
            }
            return true;
        }
    }
}
