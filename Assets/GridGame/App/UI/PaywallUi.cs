using System;
using GridGame.Meta;
using UnityEngine;
using UnityEngine.UIElements;

namespace GridGame.App
{
    /// <summary>Plays a paywall job out (§25.18, GG-T232): a modal bar that fills over the job's duration, a cue as it
    /// starts and another as it lands, then <see cref="Paywall.Finish"/> applies the effect. A job cut short by
    /// navigation stays in the save; Camp finishes it on its next build.</summary>
    public static class TimedJobView
    {
        public const string VeilName = "paywall-job";

        /// <summary>Starts a job via <paramref name="start"/> (which pays), then plays it out. A refused start toasts why.</summary>
        public static void Begin(Func<DateTime, (Result result, PaywallJob job)> start, string label, Action<Result> onDone = null)
        {
            var app = GameApp.I;
            var (r, job) = start(DateTime.UtcNow);
            if (!r.Ok || job == null)
            {
                app.Toast(r.Message ?? "Can't do that");
                app.Audio.Sfx("ui.error");
                return;
            }
            app.Persist();
            Run(job, label, onDone);
        }

        public static void Run(PaywallJob job, string label, Action<Result> onDone = null)
        {
            var app = GameApp.I;
            var veil = Ui.El("fill veil col center", app.Overlay);
            veil.name = VeilName;
            veil.pickingMode = PickingMode.Position;
            var w = Ui.Window(null, veil);
            w.style.width = Length.Percent(66);
            var title = Ui.Text(label, "h2", w);
            title.style.whiteSpace = WhiteSpace.Normal;
            title.style.marginBottom = Space.S16;
            var gauge = Ui.Gauge(0f, w);
            gauge.style.width = Length.Percent(100);
            gauge.name = "paywall-gauge";
            var left = Ui.Text("", "small dim", w);
            left.style.marginTop = Space.S8;
            app.Audio.Sfx("ui.buy");
            bool finished = false;
            veil.schedule.Execute(() =>
            {
                if (finished) return;
                IdleFrameRate.Stir();
                var now = DateTime.UtcNow;
                float p = (float)Paywall.Progress(job, now);
                Ui.SetGauge(gauge, p);
                double remaining = Math.Max(0, job.Seconds - (now - job.StartedUtc).TotalSeconds);
                left.text = p < 1f ? FormattableString.Invariant($"{remaining:0.0}s") : "Done";
                if (p < 1f) return;
                finished = true;
                var r = app.Save == null || !app.Save.Jobs.Contains(job) ? Result.Success() : Paywall.Finish(app.Content, app.Save, job, now);
                app.Audio.Sfx(r.Ok ? "ui.confirm" : "ui.error");
                app.Persist();
                veil.RemoveFromHierarchy();
                if (!string.IsNullOrEmpty(r.Message)) app.Toast(r.Message);
                onDone?.Invoke(r);
            }).Every(16);
        }
    }

    /// <summary>The Rested gate every launch passes through (§25.18): exhausted → offer the 1g·1s refill, then retry.</summary>
    public static class RestedGate
    {
        /// <summary>Bots, -demo and harness runs never hit the gate unless the run asks for it (`-rested`, or devrested?limit=1).</summary>
        private static bool initialized;

        private static void Init()
        {
            if (initialized) return;
            initialized = true;
            RestedPool.Unlimited = DemoMode.Active || TestMode.Active || CommandLine.Has("-autobattle") ||
                                   (CommandLine.Has("-automation") && !CommandLine.Has("-rested"));
        }

        /// <summary>True when the launch may go ahead; otherwise offers a refill that calls <paramref name="retry"/> once rested.</summary>
        public static bool Check(Action retry)
        {
            Init();
            var app = GameApp.I;
            var s = app.Save;
            if (s == null || RestedPool.CanLaunch(s, DateTime.UtcNow, out var why)) return true;
            app.Audio.Sfx("ui.error");
            Confirm.Show($"{why}\nRefill Rested — {Paywall.Tag(PaywallKind.RestedRefill, s)}?", "Refill", () => Refill(retry));
            return false;
        }

        public static void Spend()
        {
            Init();
            var s = GameApp.I.Save;
            if (s != null) RestedPool.Spend(s, DateTime.UtcNow);
        }

        public static void Refill(Action then = null)
        {
            // The follow-up (a launch retry, a page refresh) belongs to the screen the refill was started on: if the
            // player has left it by the time the job lands, the refill still applies but nothing launches from elsewhere.
            var from = GameApp.I.Router.Current;
            TimedJobView.Begin(now => (RestedPool.BeginRefill(GameApp.I.Save, now, out var job), job), "Resting the party",
                r => { if (r.Ok && GameApp.I.Router.Current == from) then?.Invoke(); });
        }

        /// <summary>The Diamond side of the same refill (GG-T427): instant, no timed job. Paid through the exchange
        /// (GG-T455): Diamonds first, any shortfall in gold; too little of both offers the Diamond Store.</summary>
        public static void DiamondRefill(Action then = null)
        {
            var app = GameApp.I;
            var now = DateTime.UtcNow;
            if (RestedPool.Points(app.Save, now) < RestedPool.Max && !DiamondExchange.CanPay(app.Save, RestedPool.DiamondRefillPrice).Ok)
            {
                DiamondStores.OfferStore(DiamondExchange.CanPay(app.Save, RestedPool.DiamondRefillPrice).Message);
                return;
            }
            var r = RestedPool.DiamondRefill(app.Save, now);
            app.Audio.Sfx(r.Ok ? "ui.confirm" : "ui.error");
            app.Persist();
            if (!string.IsNullOrEmpty(r.Message)) app.Toast(r.Message);
            if (r.Ok) then?.Invoke();
        }

        /// <summary>Camp's Rested window: points, a meter, the next-point timer, and the two refill buttons (gold, Diamond).</summary>
        public static void Panel(VisualElement parent, Action refresh) => Draw(Ui.Window(null, parent), refresh);

        /// <summary>The same Rested block drawn straight into <paramref name="parent"/> (a hub card) instead of its own
        /// window; still named camp-rested.</summary>
        public static void PanelIn(VisualElement parent, Action refresh) => Draw(Ui.El("col", parent), refresh);

        private static void Draw(VisualElement w, Action refresh)
        {
            Init();
            var s = GameApp.I.Save;
            var now = DateTime.UtcNow;
            int pts = RestedPool.Points(s, now);
            w.name = "camp-rested";
            Ui.Text("Rested", "small dim", w);
            // "Unlimited" is wider than "10/10": one step smaller, and it scrolls if the window is narrower still (GG-T395).
            Ui.Marquee(Ui.Text(RestedPool.Exempt(s) ? "Unlimited" : $"{pts}/{RestedPool.Max}", "kit-name num", w), false);
            Ui.Gauge(RestedPool.Exempt(s) ? 1f : pts / (float)RestedPool.Max, w);
            if (RestedPool.Exempt(s) || pts >= RestedPool.Max) return;
            var next = RestedPool.NextIn(s, now);
            Ui.Text($"+1 in {(int)next.TotalMinutes}:{next.Seconds:00}", "small dim", w);
            // The price has its own line: the side window is too narrow for "Refill · price" on one button (GG-T400).
            Ui.Text(Paywall.Tag(PaywallKind.RestedRefill, s), "small dim", w).style.marginTop = Space.S8;
            var b = RowIcon.IconButton("Refill", RowKind.Shop, () => Refill(refresh), "btn--small", w);
            b.name = "rested-refill";
            b.style.marginTop = Space.S8;
            Ui.SetEnabled(b, s.Gold >= Paywall.Cost(PaywallKind.RestedRefill, s));
            // The side window only fits a short label (GG-T400). GG-T518: the Diamond button used to carry both of its prices
            // (GG-T455), "25 Diamonds (or 150 G)", which scrolled in ~110 px and read as fragments ("monds (or"); like the
            // gold one, its price is now a whole line of its own above a short "Refill". Always enabled: short of both,
            // it offers the store.
            var dp = Ui.Text(DiamondExchange.Label(RestedPool.DiamondRefillPrice), "small dim", w);
            dp.name = "rested-refill-diamond-price";
            dp.style.whiteSpace = WhiteSpace.Normal;
            dp.style.marginTop = Space.S16;
            var db = RowIcon.IconButton("Refill", RowKind.Diamond, () => DiamondRefill(refresh), "btn--small", w);
            db.name = "rested-refill-diamond";
            db.style.marginTop = Space.S8;
        }
    }
}
