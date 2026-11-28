using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace GridGame.App
{
    /// <summary>A full-screen page. Build() is called each time it becomes visible, so it always reflects current state.</summary>
    public abstract class GameScreen
    {
        public VisualElement Root { get; private set; }
        public abstract string Id { get; }
        /// <summary>Music track for this screen (null = keep current).</summary>
        public virtual string Music => null;

        internal VisualElement Mount()
        {
            Root = new VisualElement();
            Root.AddToClassList("fill");
            Root.name = "screen-" + Id;
            Build(Root);
            Ui.ApplyHeavy(Root);
            BleedBackdrops(Root);
            return Root;
        }

        /// <summary>The screens container sits inside the safe area (§26.5), so a screen's content and buttons never go under
        /// a notch or gesture bar; its backdrop layers (a leaf "fill" child of the screen root: the background art, a
        /// shade) reach out under the insets so the art still covers the whole screen (GG-T483). A living backdrop's root
        /// bleeds too although it holds its layers; they lay out from its full rect (GG-T663).</summary>
        internal static void BleedBackdrops(VisualElement root)
        {
            if (root == null) return;
            foreach (var c in root.Children())
            {
                if (c is TextElement || !c.ClassListContains("fill")) continue;
                if (c.childCount != 0 && !c.ClassListContains(LivingBackdrop.RootClass)) continue;
                c.style.top = -GameApp.SafeTop;
                c.style.bottom = -GameApp.SafeBottom;
            }
        }

        protected abstract void Build(VisualElement root);
        public virtual void OnShown() { }
        public virtual void OnHidden() { }
        public virtual void Tick(float dt) { }
        /// <summary>Return false to swallow the back action (e.g. cancel targeting first).</summary>
        public virtual bool OnBack() => true;
        /// <summary>The app is going to the background or losing focus on a phone (GG-T480): stop anything that runs
        /// on a clock so nothing happens while the player is away. The battle opens its pause menu.</summary>
        public virtual void OnAppSuspended() { }
        /// <summary>Rebuilt in place when the stage changes size (GG-T398), so its Fit.* widths follow the new shape. A screen
        /// that re-lays itself out (the battle) or whose rebuild would restart it (a crawl, the credits) opts out.</summary>
        public virtual bool RebuildOnResize => true;

        /// <summary>GG-T1205: work the screen needs done before it is first mounted (the battle releases the last stage's
        /// art and preloads its own), run by the router behind its fade with a progress bar, the screen it replaced already
        /// gone. Report progress in 0..1 through <paramref name="progress"/>. Null (the default) mounts at once. Runs once
        /// per screen instance: coming back to it (a pop) never prepares it again.</summary>
        public virtual IEnumerator Prepare(Action<float> progress) => null;
        internal bool Prepared;

        /// <summary>GG-T1206: true for a screen that shows heroes and enemies outside the party (the bestiary, the hero and
        /// party picker, the mercenary camp): the router gives it its own <see cref="ArtScope.Screen"/> token while it is on
        /// the stack, focused whenever it is the mounted screen, and releases that art when it leaves the stack. A screen
        /// pushed over it keeps it (it is rebuilt on the way back); popping releases only the popped screen's own art.</summary>
        public virtual bool HoldsScreenArt => false;
        internal int ArtToken;

        /// <summary>GG-T1335: true for a page that shows the hub's bottom nav bar (every <see cref="HubPage"/>).</summary>
        public virtual bool ShowsNav => false;

        /// <summary>The page showing this one's body as a tab (the Shop hub's Merchant and Shards, GG-T1637), or null.</summary>
        internal GameScreen TabHost;

        /// <summary>Rebuilds in place (after a purchase etc.); a page hosted as a tab rebuilds its host.</summary>
        public void Refresh()
        {
            if (TabHost != null) { TabHost.Refresh(); return; }
            if (Root == null) return;
            Root.Clear();
            Build(Root);
            Ui.ApplyHeavy(Root);
            BleedBackdrops(Root);
        }

        protected static GameApp App => GameApp.I;
    }

    /// <summary>Stack-based navigation with a fade. One screen is mounted at a time on the stage.</summary>
    public sealed class ScreenRouter
    {
        private readonly VisualElement host;
        private readonly VisualElement fade;
        private readonly Stack<GameScreen> stack = new Stack<GameScreen>();
        private bool busy;
        // Set once the in-flight transition has applied its mutation (behind the fade); until then, later
        // screen changes wait in `queued` and are applied in order right after it (GG-T218).
        private bool applied;
        private readonly Queue<Action> queued = new Queue<Action>();
        // GG-T2028: the screen re-fit after the new top screen was built but before its fade cleared; refreshed then.
        private bool resizedInTransition;
        private VisualElement loading, loadingFill;

        /// <summary>GG-T1205: true while the top screen's <see cref="GameScreen.Prepare"/> runs behind the fade: it is
        /// Current but not mounted yet, so it is neither ticked nor told the app was suspended.</summary>
        public bool Preparing { get; private set; }

        /// <summary>True from the start of a screen change until its fade is gone (GG-T1857: the UI audit waits on it).</summary>
        public bool Busy => busy;

        public GameScreen Current => stack.Count > 0 ? stack.Peek() : null;
        public IEnumerable<GameScreen> Stack => stack;
        public event Action<GameScreen> Changed;

        public ScreenRouter(VisualElement host, VisualElement fade)
        {
            this.host = host;
            this.fade = fade;
            GameApp.StageResized += () =>
            {
                var c = Current;
                if (c == null) return;
                if (busy) resizedInTransition = true;
                if (!busy && c.RebuildOnResize) c.Refresh();
                else GameScreen.BleedBackdrops(c.Root); // the battle, a crawl: new insets still reach their backdrop (GG-T483)
            };
        }

        public void Push(GameScreen s) => Go(() => stack.Push(s));
        public void Replace(GameScreen s) => Go(() => { if (stack.Count > 0) Leave(stack.Pop()); stack.Push(s); });
        public void ResetTo(GameScreen s) => Go(() => { while (stack.Count > 0) Leave(stack.Pop()); stack.Push(s); });

        /// <summary>GG-T1335: back to the hub root in one fade, the stack left as just <see cref="HubScreen"/>. Refused
        /// (false) while a timed paywall job plays out on screen, which must finish where it was started.</summary>
        public bool Home()
        {
            if (GameApp.I.Overlay?.Q(TimedJobView.VeilName) != null) return false;
            ResetTo(new HubScreen());
            return true;
        }

        /// <summary>GG-T1341: Push, unless a screen of the same kind is already on the stack: then pop back to it in one
        /// fade, so the stack never holds two copies (the Shard Store from a currency strip over the Shard Store).</summary>
        public void PushOrReturn(GameScreen s)
        {
            foreach (var on in stack)
            {
                if (on.GetType() != s.GetType() || on.Id != s.Id) continue;
                if (on == Current) return;
                var target = on;
                Go(() => { while (stack.Count > 1 && stack.Peek() != target) Leave(stack.Pop()); });
                return;
            }
            Push(s);
        }

        /// <summary>GG-T1341: true when every screen above the <see cref="HubScreen"/> is a <see cref="HubPage"/>, so the nav
        /// and Home can drop back to the hub without abandoning a battle, its results or the pause menu's settings.</summary>
        public bool OnHubTrail
        {
            get
            {
                foreach (var s in stack)
                {
                    if (s is HubScreen) return true;
                    if (!(s is HubPage)) return false;
                }
                return false;
            }
        }

        /// <summary>How many screens sit above the <see cref="HubScreen"/> on the stack, or -1 with no hub on it.</summary>
        public int PagesAboveHub
        {
            get
            {
                int n = 0;
                foreach (var s in stack)
                {
                    if (s is HubScreen) return n;
                    n++;
                }
                return -1;
            }
        }

        public void Pop()
        {
            if (stack.Count <= 1 || busy) return;
            if (!Current.OnBack()) return;
            Go(PopOne);
        }

        /// <summary>Back action from the hardware/escape key or a header back button. False when there was nothing to
        /// go back to (the bottom screen didn't swallow it) — Android's back button then offers to quit (GG-T480).</summary>
        public bool Back()
        {
            // Mid-transition the top of the stack is a screen on its way out (or one not yet mounted): asking it, or
            // queueing a pop behind a Push, would pop the screen just pushed. Swallowed, like Pop() does.
            if (busy) return true;
            if (Current != null && !Current.OnBack()) return true;
            if (stack.Count <= 1) return false;
            Go(PopOne);
            return true;
        }

        // A queued pop runs after earlier queued changes, so it re-checks there is still a screen under the top.
        private void PopOne()
        {
            if (stack.Count > 1) Leave(stack.Pop());
        }

        /// <summary>A screen leaves the stack: it is told, and its Screen art token (GG-T1206) is released.</summary>
        private static void Leave(GameScreen s)
        {
            s.OnHidden();
            if (s.ArtToken == 0) return;
            Art.ReleaseScreenScope(s.ArtToken);
            s.ArtToken = 0;
        }

        private void Go(Action mutate)
        {
            // A Confirm veil answers a question about the screen it was raised on; if we're navigating
            // away (however that happened — a real Cancel/Yes click already removes it, so this only
            // ever catches a screen change that left one stranded) it must not outlive that screen, or
            // it silently blocks every future click with no error anywhere (see Confirm.DismissStale).
            Confirm.DismissStale();
            // During a transition nothing is dropped: before its own mutation lands, the change queues
            // behind it (so Push(A) then Push(B) mid-fade leaves A under B, as it would without the fade);
            // after it, the change applies and mounts at once, still under the fade.
            if (busy)
            {
                if (applied) { mutate(); MountTop(); }
                else queued.Enqueue(mutate);
                return;
            }
            GameApp.I.StartCoroutine(Transition(mutate));
        }

        private IEnumerator Transition(Action mutate)
        {
            busy = true;
            applied = false;
            bool hasScreen = host.childCount > 0;
            if (hasScreen)
            {
                fade.AddToClassList("fade--on");
                fade.pickingMode = PickingMode.Position;
                yield return new WaitForSecondsRealtime(Timing.ScreenFade);
            }
            // Apply this change, then every change queued while the fade was going on, in call order,
            // and mount only the resulting top screen once.
            mutate();
            // GG-T1205: the screen about to show gets its Prepare (the battle's stage load) with the old screen
            // unmounted, so what only it drew can be unloaded; changes queued meanwhile apply after, and a new top
            // screen they leave is prepared in turn.
            while (true)
            {
                while (queued.Count > 0) queued.Dequeue()();
                var top = Current;
                if (top == null || top.Prepared) break;
                top.Prepared = true;
                IEnumerator prep;
                try { prep = top.Prepare(SetProgress); }
                catch (Exception e) { Debug.LogException(e); prep = null; }
                if (prep == null) continue;
                host.Clear();
                fade.AddToClassList("fade--on");
                fade.pickingMode = PickingMode.Position;
                SetProgress(0f);
                Preparing = true;
                while (true)
                {
                    object step;
                    try
                    {
                        if (!prep.MoveNext()) break;
                        step = prep.Current;
                    }
                    catch (Exception e) { Debug.LogException(e); break; } // a failed load still mounts the screen
                    yield return step;
                }
                Preparing = false;
            }
            if (loading != null) loading.style.display = DisplayStyle.None;
            applied = true;
            MountTop();
            yield return null;
            fade.RemoveFromClassList("fade--on");
            fade.pickingMode = PickingMode.Ignore;
            busy = false;
            if (resizedInTransition)
            {
                resizedInTransition = false;
                if (Current != null && Current.RebuildOnResize) Current.Refresh();
            }
        }

        /// <summary>The loading bar on the fade (GG-T1205), built on first use.</summary>
        private void SetProgress(float k)
        {
            if (loading == null)
            {
                loading = new VisualElement { name = "loading", pickingMode = PickingMode.Ignore };
                loading.style.position = Position.Absolute;
                loading.style.left = new Length(25, LengthUnit.Percent);
                loading.style.right = new Length(25, LengthUnit.Percent);
                loading.style.bottom = new Length(12, LengthUnit.Percent);
                loading.style.alignItems = Align.Center;
                var label = Ui.Text("Loading", "kit-caption", loading);
                label.style.color = Palette.GoldLight;
                label.style.marginBottom = Space.S8;
                var track = new VisualElement { name = "loading-track", pickingMode = PickingMode.Ignore };
                track.style.alignSelf = Align.Stretch;
                track.style.height = Size.Track;
                track.style.backgroundColor = new Color(1f, 1f, 1f, 0.12f);
                track.style.borderTopLeftRadius = track.style.borderTopRightRadius = Radius.R3;
                track.style.borderBottomLeftRadius = track.style.borderBottomRightRadius = Radius.R3;
                track.style.overflow = Overflow.Hidden;
                loading.Add(track);
                loadingFill = new VisualElement { name = "loading-fill", pickingMode = PickingMode.Ignore };
                loadingFill.style.height = new Length(100, LengthUnit.Percent);
                loadingFill.style.backgroundColor = Palette.Gold;
                track.Add(loadingFill);
                fade.Add(loading);
            }
            loading.style.display = DisplayStyle.Flex;
            loadingFill.style.width = new Length(Mathf.Clamp01(k) * 100f, LengthUnit.Percent);
        }

        private void MountTop()
        {
            host.Clear();
            resizedInTransition = false; // the screen mounted now is built for the stage as it is
            var top = Current;
            if (top == null) { Art.FocusScreenScope(0); return; }
            // GG-T1206: what the mounted screen loads belongs to its own Screen token, or to no token at all (the battle's
            // stays Stage), never to a screen it covers.
            if (!top.HoldsScreenArt) Art.FocusScreenScope(0);
            else if (top.ArtToken == 0) top.ArtToken = Art.OpenScreenScope();
            else Art.FocusScreenScope(top.ArtToken);
            host.Add(top.Mount());
            if (top.Music != null) GameApp.I.Audio.Music(top.Music);
            top.OnShown();
            Changed?.Invoke(top);
        }
    }
}
