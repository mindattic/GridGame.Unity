using UnityEngine.UIElements;

namespace GridGame.App
{
    /// <summary>
    /// GG-T1330: About — the studio, the version and build, the copyright line, then rows opening the Privacy policy and
    /// the Terms (<see cref="Legal"/>) and the Third-party notices (Resources/Legal/ThirdPartyNotices.txt). A plain
    /// screen with its own header, so Settings can push it now and the Main Menu later.
    /// </summary>
    public sealed class AboutScreen : GameScreen
    {
        public override string Id => "about";

        protected override void Build(VisualElement root)
        {
            var col = Ui.El("fill col", root);
            Ui.Header("About", () => App.Router.Back(), col, out _);
            var list = Ui.ListPanel(Ui.PageBody(col), null, "about-list");
            Ui.Section(GameApp.Title, list).name = "about-title";
            Ui.Para(Legal.Studio, list).name = "about-studio";
            Ui.Para("Version " + Legal.VersionLine(), list).name = "about-version";
            Ui.Note(SplashScreen.Copyright, list).name = "about-copyright";
            Ui.Section("Legal", list);
            var privacy = Ui.MenuRow("Privacy policy", "Open", () => Legal.Open(Legal.PrivacyPolicyUrl), list);
            privacy.name = "about-privacy";
            privacy.KindIcon(RowKind.Settings);
            var terms = Ui.MenuRow("Terms of service", "Open", () => Legal.Open(Legal.TermsUrl), list);
            terms.name = "about-terms";
            terms.KindIcon(RowKind.Settings);
            var notices = Ui.MenuRow("Third-party notices", "", () => App.Router.Push(new ThirdPartyNoticesScreen()), list);
            notices.name = "about-notices";
            notices.KindIcon(RowKind.Settings);
        }
    }

    /// <summary>GG-T1330: the bundled third-party notices (<see cref="Legal.ThirdPartyNotices"/>), one scrolling page.</summary>
    public sealed class ThirdPartyNoticesScreen : GameScreen
    {
        public override string Id => "notices";

        protected override void Build(VisualElement root)
        {
            var col = Ui.El("fill col", root);
            Ui.Header("Third-party notices", () => App.Router.Back(), col, out _);
            var list = Ui.ListPanel(Ui.PageBody(col), null, "notices-list");
            Ui.Para(Legal.ThirdPartyNotices(), list).name = "notices-text";
        }
    }
}
