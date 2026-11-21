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
            return Root;
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

        /// <summary>Rebuilds in place (after a purchase etc.).</summary>
        public void Refresh()
        {
            if (Root == null) return;
            Root.Clear();
            Build(Root);
            Ui.ApplyHeavy(Root);
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

        public GameScreen Current => stack.Count > 0 ? stack.Peek() : null;
        public IEnumerable<GameScreen> Stack => stack;
        public event Action<GameScreen> Changed;

        public ScreenRouter(VisualElement host, VisualElement fade)
        {
            this.host = host;
            this.fade = fade;
            GameApp.StageResized += () => { var c = Current; if (c != null && !busy && c.RebuildOnResize) c.Refresh(); };
        }

        public void Push(GameScreen s) => Go(() => stack.Push(s));
        public void Replace(GameScreen s) => Go(() => { if (stack.Count > 0) stack.Pop().OnHidden(); stack.Push(s); });
        public void ResetTo(GameScreen s) => Go(() => { while (stack.Count > 0) stack.Pop().OnHidden(); stack.Push(s); });

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
            if (Current != null && !Current.OnBack()) return true;
            if (stack.Count <= 1) return false;
            Go(PopOne);
            return true;
        }

        // A queued pop runs after earlier queued changes, so it re-checks there is still a screen under the top.
        private void PopOne()
        {
            if (stack.Count > 1) stack.Pop().OnHidden();
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
                yield return new WaitForSecondsRealtime(0.2f);
            }
            // Apply this change, then every change queued while the fade was going on, in call order,
            // and mount only the resulting top screen once.
            mutate();
            while (queued.Count > 0) queued.Dequeue()();
            applied = true;
            MountTop();
            yield return null;
            fade.RemoveFromClassList("fade--on");
            fade.pickingMode = PickingMode.Ignore;
            busy = false;
        }

        private void MountTop()
        {
            host.Clear();
            var top = Current;
            if (top == null) return;
            host.Add(top.Mount());
            if (top.Music != null) GameApp.I.Audio.Music(top.Music);
            top.OnShown();
            Changed?.Invoke(top);
        }
    }
}
