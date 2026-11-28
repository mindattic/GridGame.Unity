// GridGame design system (GG-T2044). A plain global so the page opens from file:// with no fetch.
// The `uss`, `space`, `size`, `timing` and `shapes` blocks were read from Resources/UI/Theme.uss, App/UI/Tokens.cs and
// tools/devices.json on the day this page was written; the rest is the spec in docs/DESIGN_SYSTEM.md.
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

  radii: [
    { what: "Window, toast", px: 10 }, { what: "Stage frame", px: 12 }, { what: "Button, tab top, card, thumbnail", px: 8 },
    { what: "Menu row, role badge", px: 6 }, { what: "Lab clamp, conduit", px: 0 }
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

  generated: {"colors":[{"var":"--color-text","hex":"#F4F4F6","alpha":1.0},
    {"var":"--color-dim","hex":"#C8C8D0","alpha":1.0},
    {"var":"--color-sub","hex":"#D0D0D6","alpha":1.0},
    {"var":"--color-white","hex":"#FFFFFF","alpha":1.0},
    {"var":"--color-ink","hex":"#0E0E12","alpha":1.0},
    {"var":"--color-frame","hex":"#EEEEF0","alpha":1.0},
    {"var":"--color-panel","hex":"#2C2C30","alpha":0.94},
    {"var":"--color-text-edge","hex":"#08080C","alpha":1.0},
    {"var":"--color-text-drop","hex":"#000000","alpha":0.85},
    {"var":"--color-gold","hex":"#FFD959","alpha":1.0},
    {"var":"--color-good","hex":"#73F294","alpha":1.0},
    {"var":"--color-bad","hex":"#FFAAA2","alpha":1.0},
    {"var":"--color-warn","hex":"#FFC45C","alpha":1.0},
    {"var":"--color-info","hex":"#DBE0FF","alpha":1.0},
    {"var":"--color-mana","hex":"#6BC7FF","alpha":1.0},
    {"var":"--color-hero","hex":"#78ABFF","alpha":1.0},
    {"var":"--color-enemy","hex":"#EB454A","alpha":1.0},
    {"var":"--color-boss","hex":"#DB66FF","alpha":1.0},
    {"var":"--color-guest","hex":"#6EF0A0","alpha":1.0},
    {"var":"--color-neutral","hex":"#FFCC5E","alpha":1.0},
    {"var":"--color-haste","hex":"#59FFE0","alpha":1.0},
    {"var":"--color-cast-hero","hex":"#26C4E6","alpha":1.0},
    {"var":"--color-cast-enemy","hex":"#FF8C1A","alpha":1.0},
    {"var":"--color-el-physical","hex":"#D9E0F2","alpha":1.0},
    {"var":"--color-el-fire","hex":"#FF7326","alpha":1.0},
    {"var":"--color-el-ice","hex":"#80D9FF","alpha":1.0},
    {"var":"--color-el-lightning","hex":"#FFF259","alpha":1.0},
    {"var":"--color-el-poison","hex":"#99FF4D","alpha":1.0},
    {"var":"--color-el-holy","hex":"#FFF7C7","alpha":1.0},
    {"var":"--color-el-dark","hex":"#9E4DF2","alpha":1.0},
    {"var":"--color-el-arcane","hex":"#FF73D9","alpha":1.0},
    {"var":"--color-rarity-junk","hex":"#C0C0C4","alpha":1.0},
    {"var":"--color-rarity-common","hex":"#ECECF0","alpha":1.0},
    {"var":"--color-rarity-uncommon","hex":"#6EE678","alpha":1.0},
    {"var":"--color-rarity-rare","hex":"#96C8FF","alpha":1.0},
    {"var":"--color-rarity-epic","hex":"#DCB2FF","alpha":1.0},
    {"var":"--color-rarity-legendary","hex":"#FFC846","alpha":1.0},
    {"var":"--color-gauge-full","hex":"#6EE678","alpha":1.0},
    {"var":"--color-gauge-mid","hex":"#F0C846","alpha":1.0},
    {"var":"--color-gauge-low","hex":"#F05046","alpha":1.0}],"space":[4,8,12,16,24,32,48,64,96],"size":[["Underline",2.0],["Hairline",3.0],["Track",6.0],["Bar",8.0],["Shard",13.0],["Gauge",14.0],["HpBar",18.0],["Crack",24.0],["Pip",34.0],["IconS",40.0],["Chip",50.0],["IconM",56.0],["Orb",60.0],["Icon",64.0],["IconL",76.0],["AvatarW",80.0],["Glyph",88.0],["Avatar",96.0],["PartySlot",180.0],["Thumb",120.0],["Medallion",140.0],["Gem",192.0],["PortraitW",360.0],["PortraitH",400.0],["ArtW",420.0],["ArtH",620.0],["TrailSpark",22.0],["TrailSwing",120.0],["TrailLift",160.0],["ClampArm",44.0],["ClampBar",6.0],["ClampBolt",14.0],["Conduit",8.0],["ConduitMinHeight",220.0],["Plate",92.0],["Target",132.0],["Cell",170.0],["Slot",196.0],["Card",300.0],["ColumnS",70.0],["Column",90.0],["ColumnL",100.0],["Label",180.0],["CodeBox",360.0]],"timing":[["Instant",0.08],["Quick",0.15],["Standard",0.25],["Emphasis",0.4],["Dramatic",0.7],["Cinematic",1.2],["TrailSparkEvery",0.03],["TrailSparkLife",0.35],["ConduitStep",0.1],["ConduitIdleMin",2.5],["ConduitIdleMax",9.0]],"shapes":[{"id":"iphone-mini","rep":"iphone13mini","label":"iPhone 12 mini / 13 mini (notch, 375 pt, native 2.88x)","inTarget":false,"reference":false,"px":[1080,2340],"pt":[375,812],"scale":2.88,"aspect":0.4615,"stage":[1170,2535],"framed":false,"k":0.9231,"safeTop":156,"safeBottom":106,"thumbTop":1406,"targetPt":42.3,"floorPt":9.6},
    {"id":"iphone-notch-390","rep":"iphone14","label":"iPhone 12 / 12 Pro / 13 / 13 Pro / 14 / 16e (notch, 390 pt)","inTarget":true,"reference":false,"px":[1170,2532],"pt":[390,844],"scale":3,"aspect":0.4621,"stage":[1170,2532],"framed":false,"k":1.0,"safeTop":141,"safeBottom":102,"thumbTop":1400,"targetPt":44.0,"floorPt":10.0},
    {"id":"iphone-notch-428","rep":"iphone14plus","label":"iPhone 12 Pro Max / 13 Pro Max / 14 Plus (notch, 428 pt)","inTarget":false,"reference":false,"px":[1284,2778],"pt":[428,926],"scale":3,"aspect":0.4622,"stage":[1170,2532],"framed":false,"k":1.0972,"safeTop":129,"safeBottom":93,"thumbTop":1400,"targetPt":48.3,"floorPt":11.0},
    {"id":"iphone-island-393","rep":"iphone16","label":"iPhone 14 Pro / 15 / 15 Pro / 16 (Dynamic Island, 393 pt)","inTarget":true,"reference":true,"px":[1179,2556],"pt":[393,852],"scale":3,"aspect":0.4613,"stage":[1170,2536],"framed":false,"k":1.0077,"safeTop":176,"safeBottom":101,"thumbTop":1418,"targetPt":44.3,"floorPt":10.1},
    {"id":"iphone-island-430","rep":"iphone16plus","label":"iPhone 14 Pro Max / 15 Plus / 15 Pro Max / 16 Plus (Dynamic Island, 430 pt)","inTarget":true,"reference":false,"px":[1290,2796],"pt":[430,932],"scale":3,"aspect":0.4614,"stage":[1170,2536],"framed":false,"k":1.1025,"safeTop":161,"safeBottom":93,"thumbTop":1416,"targetPt":48.5,"floorPt":11.0},
    {"id":"iphone-402","rep":"iphone17pro","label":"iPhone 16 Pro / 17 / 17 Pro (402 pt)","inTarget":true,"reference":false,"px":[1206,2622],"pt":[402,874],"scale":3,"aspect":0.46,"stage":[1170,2544],"framed":false,"k":1.0307,"safeTop":180,"safeBottom":99,"thumbTop":1426,"targetPt":45.3,"floorPt":10.3},
    {"id":"iphone-440","rep":"iphone17promax","label":"iPhone 16 Pro Max / 17 Pro Max (440 pt)","inTarget":true,"reference":false,"px":[1320,2868],"pt":[440,956],"scale":3,"aspect":0.4603,"stage":[1170,2542],"framed":false,"k":1.1282,"safeTop":165,"safeBottom":90,"thumbTop":1423,"targetPt":49.6,"floorPt":11.3},
    {"id":"iphone-air","rep":"iphoneair","label":"iPhone Air (420 pt, 68 pt top inset)","inTarget":true,"reference":false,"px":[1260,2736],"pt":[420,912],"scale":3,"aspect":0.4605,"stage":[1170,2541],"framed":false,"k":1.0767,"safeTop":189,"safeBottom":95,"thumbTop":1430,"targetPt":47.4,"floorPt":10.8},
    {"id":"galaxy-20x9","rep":"galaxys21","label":"Galaxy S21 / S21+ / S21 Ultra (20:9)","inTarget":false,"reference":false,"px":[1080,2400],"pt":[360,800],"scale":3,"aspect":0.45,"stage":[1170,2600],"framed":false,"k":0.9231,"safeTop":104,"safeBottom":78,"thumbTop":1434,"targetPt":40.6,"floorPt":9.2},
    {"id":"galaxy-fhd","rep":"galaxys25","label":"Galaxy S22-S25 base, S22+ / S23+ and the FEs (1080x2340, 19.5:9)","inTarget":true,"reference":false,"px":[1080,2340],"pt":[360,780],"scale":3,"aspect":0.4615,"stage":[1170,2535],"framed":false,"k":0.9231,"safeTop":104,"safeBottom":78,"thumbTop":1398,"targetPt":40.6,"floorPt":9.2},
    {"id":"galaxy-qhd","rep":"galaxys25ultra","label":"Galaxy S22-S25 Ultra, S24+ / S25+, S25 Edge (1440-wide QHD+, 19.5:9)","inTarget":true,"reference":false,"px":[1440,3120],"pt":[412,891],"scale":3.5,"aspect":0.4615,"stage":[1170,2535],"framed":false,"k":1.2308,"safeTop":91,"safeBottom":68,"thumbTop":1398,"targetPt":46.4,"floorPt":10.5},
    {"id":"galaxy-flip-main","rep":"galaxyzflip6","label":"Galaxy Z Flip5 / Flip6 main screen (22:9)","inTarget":true,"reference":false,"px":[1080,2640],"pt":[393,960],"scale":2.75,"aspect":0.4091,"stage":[1170,2860],"framed":false,"k":0.9231,"safeTop":95,"safeBottom":72,"thumbTop":1576,"targetPt":44.3,"floorPt":10.1},
    {"id":"galaxy-fold-cover","rep":"galaxyzfold6cover","label":"Galaxy Z Fold5 / Fold6 cover screen (about 9:23)","inTarget":true,"reference":false,"px":[968,2376],"pt":[323,792],"scale":3,"aspect":0.4074,"stage":[1170,2872],"framed":false,"k":0.8273,"safeTop":116,"safeBottom":87,"thumbTop":1584,"targetPt":36.4,"floorPt":8.3},
    {"id":"pixel-20x9","rep":"pixel9","label":"Pixel 9 (reference shape the tools already use)","inTarget":false,"reference":false,"px":[1080,2424],"pt":[412,923],"scale":2.625,"aspect":0.4455,"stage":[1170,2626],"framed":false,"k":0.9231,"safeTop":119,"safeBottom":68,"thumbTop":1460,"targetPt":46.4,"floorPt":10.5},
    {"id":"ipad","rep":"ipad","label":"iPad 10th gen (reference: letterboxed)","inTarget":false,"reference":false,"px":[1640,2360],"pt":[820,1180],"scale":2,"aspect":0.6949,"stage":[1424,2532],"framed":true,"k":0.9321,"safeTop":51,"safeBottom":43,"thumbTop":1392,"targetPt":61.5,"floorPt":14.0}],"reference":"iphone15pro","devicesChecked":"2026-11-28"}
};
