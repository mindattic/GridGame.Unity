// GridGame design system (GG-T2044). A plain global so the page opens from file:// with no fetch.
// `generated` is tokens.js, written by tools/design_tokens.py from Theme.uss, Tokens.cs, GameApp.cs and devices.json
// (GG-T2047); the rest is the spec in docs/DESIGN_SYSTEM.md.
window.DS = {
  links: {
    spec: "../../DESIGN_SYSTEM.md",
    bible: "../../BIBLE.md",
    review: "../ux-review/REVIEW.md",
    copy: { label: "Copy guide (GG-T2045)", md: "../../COPY_STYLE.md", page: "../copy-style/index.html" },
    icons: { label: "Icon style (GG-T2046)", md: "../../ICONS.md", page: "../icon-set/index.html" },
    thumb: "../../../Assets/GridGame/Resources/Thumbs/knight-female-00.png"
  },

  rules: [
    { id: "R1", title: "Hamburger top-left, gold top-right", body: "The hub and the battle carry the hamburger in the top-left corner and the gold count in the top-right corner.", ref: "BIBLE 9.0 · GG-T1808, GG-T1904", code: "RowIcon.HamburgerButton · Ui.Currencies", demo: "corners" },
    { id: "R2", title: "Sub-pages keep Back top-left", body: "Every sub-page has Back in the top-left corner and no hamburger. Home sits after Back only more than one page above the hub.", ref: "BIBLE 9.0, 11.3 · GG-T1904, GG-T1335", code: "Ui.Header(title, onBack, …, onHome)", demo: "back" },
    { id: "R3", title: "Select, then confirm, pressed in", body: "The first tap selects, a tap on the selected thing runs it. Selected reads as pressed in: an inset well, a 3-px frame with a dark top edge, a 3-px sink. Nothing starts selected on touch.", ref: "BIBLE 11.5 · GG-T1923, GG-T1942", code: "Ui.MenuRow · Ui.ConfirmTap · .tap--selected", demo: "confirm" },
    { id: "R4", title: "No hand cursor on mobile", body: "The pointing hand is for PC builds and the editor. On a phone no row keeps a gutter for it; the kind icon sits at the left edge.", ref: "BIBLE 11.5 · GG-T1923", code: "Ui.ShowsCursor · .no-cursor · Kit.Indent", demo: "cursor" },
    { id: "R5", title: "No truncation", body: "Shrink to fit down to the floor, then wrap with clean hyphenation, or for one line a marquee. No ellipsis (except under Reduce Motion), no clipped glyph.", ref: "BIBLE 26.6 · GG-T305, GG-T711", code: "TextFit · Marquee · LineBreaker", demo: "trunc" },
    { id: "R6", title: "Arrows are filled, outlined shapes", body: "Every arrow is a Bezier curve or an orthogonal path, filled with an outline, its head part of the same outline. Never a stroked line, a double stroke or a font arrow.", ref: "BIBLE 11.6 · GG-T735 to GG-T737", code: "Shapes.BezierArrow · Shapes.OrthoArrow", demo: "arrows" },
    { id: "R7", title: "132 px / 44 pt touch targets", body: "Every tappable is at least 132 × 132 design px, with 24 px between neighbours.", ref: "POLISH POL-C2, C3 · BIBLE 26.7", code: "Size.Target · --size-target · Kit.Row", demo: "target" },
    { id: "R8", title: "30-px text floor, one size", body: "Nothing under 30 design px, and the floor rises live on small screens. All GUI text is TypeScale.Base (40 to 56); emphasis is weight and colour.", ref: "BIBLE 26.6 · GG-T770, GG-T1878", code: "TypeFloor.ShrinkMin · GuiType", demo: "type" },
    { id: "R9", title: "Thumbnails; full art on tap", body: "Every small actor image is the baked 320-px thumbnail. The full portrait shows only in story, reveals and when a thumbnail is tapped.", ref: "BIBLE 12.1 · GG-T713, GG-T1804", code: "Ui.Thumb · ArtPreview.Open", demo: "thumb" }
  ],

  roles: [
    { role: "Stage", hex: "#0C0C16", token: ".theme-manapunk .stage", use: "behind everything" },
    { role: "Pane top", hex: "#22203A", token: "theme gradient", use: "windows, sheets, dialogs (top)" },
    { role: "Pane bottom", hex: "#100F1C", token: "theme gradient", use: "windows, sheets, dialogs (bottom)" },
    { role: "Pane flat", hex: "#1A1930", token: ".window--plain · .tab", use: "plain windows, inactive tabs" },
    { role: "Raised", hex: "#2A2840", token: ".btn · .tab--active", use: "button face, active tab" },
    { role: "Rail", hex: "#60646F", token: "Palette.Pewter", use: "frames, dividers, rims" },
    { role: "Rail dark", hex: "#34363F", token: "Palette.PewterDark", use: "a window's lower edge" },
    { role: "Rail light", hex: "#8C909C", token: "Palette.PewterLight", use: "a gauge sleeve's lit top" },
    { role: "Accent", hex: "#7590C8", token: "Palette.OrbLit", use: "primary button, lit top edges, active tab" },
    { role: "Accent light", hex: "#ACA5D3", token: "Palette.OrbLitHighlight", use: "primary rim" },
    { role: "Accent deep", hex: "#41386F", token: "theme", use: "primary lip" },
    { role: "Text", hex: "#E6E6EE", token: "--color-text (Manapunk)", use: "all body text" },
    { role: "Text dim", hex: "#A4A3BC", token: ".dim", use: "meta and captions, 3:1 floor" },
    { role: "Gold", hex: "#FFD959", token: "--color-gold", use: "currency, header subtitle" },
    { role: "Danger face", hex: "#3C1A26", token: ".btn--danger", use: "destructive button" },
    { role: "Danger rim", hex: "#963659", token: ".btn--danger", use: "destructive button rim" },
    { role: "Veil", hex: "#05050C", alpha: 0.72, token: ".veil", use: "behind modals and sheets" }
  ],

  elevation: [
    { level: 0, layer: "Stage", holds: "backdrop, board", edge: "none" },
    { level: 1, layer: "Page", holds: "windows, lists, cards", edge: "rail rim, lit mana top" },
    { level: 2, layer: "Chrome", holds: "header, nav, footer, action row", edge: "2-px rail or mana border" },
    { level: 3, layer: "Raised control", holds: "buttons", edge: "the dark lip (7 px; small 5)" },
    { level: 4, layer: "Overlay", holds: "toast, Inspect card", edge: "rail rim, mana top" },
    { level: 5, layer: "Modal", holds: "veil + sheet, Confirm, Art preview", edge: "veil at 0.72 behind" }
  ],

  eases: [
    { name: "Out", css: "cubic-bezier(0.22, 1, 0.36, 1)" },
    { name: "InOut", css: "cubic-bezier(0.65, 0, 0.35, 1)" },
    { name: "Back", css: "cubic-bezier(0.34, 1.56, 0.64, 1)" },
    { name: "Snap", css: "cubic-bezier(0.165, 0.84, 0.44, 1)" }
  ],

  components: [
    { name: "Button", builder: "Ui.Button(text, onClick, classes, parent, confirmTap)", classes: ".btn", confirm: "yes for an action with no confirm of its own; no for navigation, steppers, a dialog's answer, the battle commit and buttons that open their own Confirm", demo: "btn" },
    { name: "Primary button", builder: "Ui.Button(…, \"btn--primary\")", classes: ".btn--primary", confirm: "as Button; one per screen", demo: "btn primary" },
    { name: "Danger button", builder: "Ui.Button(…, \"btn--danger\")", classes: ".btn--danger", confirm: "usually opens its own Confirm, so single", demo: "btn danger" },
    { name: "Menu row", builder: "Ui.MenuRow(label, value, onClick, parent, icon, enabled)", classes: ".menu-row · --selected · --disabled", confirm: "always", demo: "row" },
    { name: "Card", builder: "Ui.Card(parent, classId, name, out col)", classes: ".card · .tap--selected", confirm: "when its tap runs an action (Ui.Tappable(…, confirmTap: true))", demo: "card" },
    { name: "Tab", builder: "Ui.Tabs(names, active, onPick, parent, locked)", classes: ".tab · .tab--active", confirm: "no (navigation)", demo: "tab" },
    { name: "Nav tab", builder: "Ui.NavBar(parent, current)", classes: ".kit-navtab · --active · --locked", confirm: "no (navigation)", demo: "nav" }
  ],

  kit: [
    ["Header (sub-page)", "Ui.Header(title, onBack, parent, out currencies, subtitle, onHome)", "Back top-left, title, gold subtitle, currencies top-right"],
    ["Hub / battle top bar", "RowIcon.HamburgerButton + Ui.Currencies", "hamburger top-left, gold top-right"],
    ["Bottom nav", "Ui.NavBar(parent, current)", "Home · Campaign · Heroes · Summon · Shop"],
    ["Page body", "Ui.PageBody", "the column: 24-px gutter, 16-px gaps"],
    ["Window", "Ui.Panel(parent, title, fill) · Ui.Window", "padding 16 / 24; fill takes the leftover height"],
    ["List", "Ui.ListPanel · Ui.List", "scrolls inside itself"],
    ["Row note", "Ui.Note", "dim sentence, indented Kit.Indent"],
    ["Section", "Ui.Section", "heavy gold, flush left"],
    ["Icon button", "RowIcon.IconButton(kind, …)", "132 square; Confirm kind is select-then-confirm"],
    ["Stepper", "Ui.Stepper(parent, value, onMinus, onPlus, …)", "minus, value, plus; single taps"],
    ["Field", "Ui.Field(label, value, parent)", "dim label, body value"],
    ["Detail pane", "Ui.DetailPanel", "to 45 % of the column, then scrolls"],
    ["Footer", "Ui.Footer", "total left, commit right, pinned above the nav"],
    ["Empty state", "Ui.EmptyState(list, icon, text) · Ui.Empty", "icon, one sentence, the next step"],
    ["Gauge", "Ui.Gauge · Ui.SetGauge", "glass tube in a pewter sleeve"],
    ["Thumbnail", "Ui.Thumb(id, parent, size, thumbOnly) · Ui.PortraitThumb", "320-px thumb; a tap opens ArtPreview"],
    ["Role badge", "Ui.RoleBadge", "role rim plus a letter"],
    ["Text roles", "Ui.Heading · Ui.Name · Ui.Para · Ui.Caption · Ui.Display", "one size; weight and colour differ"]
  ],

  overlays: [
    { name: "Confirm dialog", builder: "Confirm.Show(question, yes, onYes, onNo)", shape: "centred window, 76 % wide; Cancel then Yes, right-aligned", dismiss: "Cancel, Back", rule: "single tap: the dialog is the confirm" },
    { name: "Bottom sheet", builder: "BattleScreen.AbilitySheet (GG-T1914, GG-T1920)", shape: "full width from the thumb zone; thumbnail header, tabs, breadcrumb; board visible behind", dismiss: "Close, Back, veil tap", rule: "rows select-then-confirm; commit button single" },
    { name: "Inspect card", builder: "InspectCard.Attach(e, build) · InspectCard.Tap", shape: "floating card by its anchor", dismiss: "lift, tap elsewhere", rule: "press and hold inspects anything" },
    { name: "Art preview", builder: "ArtPreview.Open(classId)", shape: "full portrait over the veil", dismiss: "tap, Back", rule: "the only place full art shows outside story" },
    { name: "Toast", builder: "GameApp.Toast(text, seconds)", shape: "small pane above the nav", dismiss: "times out (1.8 s)", rule: "never over content" },
    { name: "Pause menu", builder: "hamburger → OpenMenu", shape: "sheet of menu rows", dismiss: "Resume, Back", rule: "rows select-then-confirm" }
  ],

  dos: [
    ["Hamburger top-left, gold top-right on the hub and in battle", "Menu in the thumb's corner, gold on the left", "R1"],
    ["Back top-left on every sub-page", "A hamburger on a sub-page", "R2"],
    ["First tap selects, second runs; selection pressed in", "Run on the first tap; selection by colour or glow alone", "R3"],
    ["Kind icon at the row's left edge", "A cursor gutter or the pointing hand on a phone", "R4"],
    ["Shrink, then wrap, then marquee", "An ellipsis, a value clipped at the edge, a word broken by hand", "R5"],
    ["Arrows from Shapes: filled, outlined, Bezier or orthogonal", "A double stroke, a font arrow, a bare stroked line", "R6"],
    ["132 × 132 targets, 24 px apart", "A 20-px talent node, a text link as the tap target", "R7"],
    ["TypeScale.Base; weight or colour for emphasis", "A smaller size for meta; anything under 30 px", "R8"],
    ["Hero thumbnails; full art on tap", "A shrunken 1024 portrait in a list or card", "R9"],
    ["One primary button, in the thumb zone", "Three price buttons per row; the commit button at the top", "POL-C4, C7"],
    ["Every colour, space and duration from a token", "new Color(…), 12px or 0.3f in a screen", "POL-A1, B1, C1, E1"],
    ["Footer and toasts pinned above the nav", "A footer under the nav, a toast over a row", "REVIEW 2.3"]
  ],

  generated: window.DS_TOKENS
};
