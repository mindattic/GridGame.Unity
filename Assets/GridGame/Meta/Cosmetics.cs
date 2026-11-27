using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace GridGame.Meta
{
    /// <summary>What a cosmetic dresses: a unit's portrait frame, the menu windows, or the hub's backdrop.</summary>
    public enum CosmeticKind
    {
        Frame,
        Theme,
        Backdrop,
    }

    /// <summary>
    /// One cosmetic (GG-T1456), from <c>Resources/UI/cosmetics.json</c>. Every look is procedural (gradients, colours and
    /// <see cref="WindowThemeCatalog.ProceduralTextures"/>), never a file. Ownership lives on the account wallet
    /// (<see cref="AccountWallet.Cosmetics"/>), the equipped one per kind in the profile's settings
    /// (<see cref="SettingsSave.Cosmetics"/>).
    /// </summary>
    public sealed class CosmeticDef
    {
        [JsonProperty("id")] public string Id;
        [JsonProperty("kind"), JsonConverter(typeof(StringEnumConverter))] public CosmeticKind Kind;
        [JsonProperty("name")] public string Name;
        /// <summary>How it is got: <see cref="Cosmetics.Pass"/>, <see cref="Cosmetics.Shards"/> and/or <see cref="Cosmetics.Offer"/>.</summary>
        [JsonProperty("sources")] public string[] Sources = Array.Empty<string>();
        /// <summary>The Shard price when <see cref="Cosmetics.Shards"/> is a source.</summary>
        [JsonProperty("shards", NullValueHandling = NullValueHandling.Ignore)] public int? ShardPrice;
        /// <summary>The season pass tier that grants it when <see cref="Cosmetics.Pass"/> is a source.</summary>
        [JsonProperty("passTier", NullValueHandling = NullValueHandling.Ignore)] public int? PassTier;
        /// <summary>Frame: 1-3 colours (solid, bevel, light/mid/dark), as <see cref="WindowFrameDef.Colors"/>.</summary>
        [JsonProperty("colors", NullValueHandling = NullValueHandling.Ignore)] public string[] Colors;
        /// <summary>Frame: an optional glow colour.</summary>
        [JsonProperty("glow", NullValueHandling = NullValueHandling.Ignore)] public string Glow;
        /// <summary>Theme: the window theme it unlocks.</summary>
        [JsonProperty("theme", NullValueHandling = NullValueHandling.Ignore)] public WindowThemeDef Theme;
        /// <summary>Backdrop: the hub's gradient.</summary>
        [JsonProperty("gradient", NullValueHandling = NullValueHandling.Ignore)] public WindowGradientDef Gradient;
        /// <summary>Backdrop: an optional procedural texture over the gradient.</summary>
        [JsonProperty("texture", NullValueHandling = NullValueHandling.Ignore)] public WindowTextureDef Texture;

        public bool Has(string source) => Sources != null && Sources.Contains(source);
    }

    /// <summary>The cosmetic catalogue's parsing, validation, ownership and equipping rules (GG-T1456). Pure.</summary>
    public static class Cosmetics
    {
        public const string Pass = "pass", Shards = "shards", Offer = "offer";
        public static readonly string[] SourceNames = { Pass, Shards, Offer };
        public const int MaxNameLength = 24;

        private sealed class CosmeticsFile
        {
            [JsonProperty("comment")] public string Comment { get; set; }
            [JsonProperty("cosmetics")] public List<CosmeticDef> Cosmetics;
        }

        /// <summary>Reads a cosmetics.json. Every cosmetic that fails <see cref="Validate"/> is left out and named in
        /// <paramref name="errors"/>; a file that doesn't parse yields none and one error. Catalogue-wide checks
        /// (<see cref="ValidateCatalog"/>) are added to <paramref name="errors"/> too.</summary>
        public static List<CosmeticDef> Parse(string json, out List<string> errors)
        {
            errors = new List<string>();
            var ok = new List<CosmeticDef>();
            CosmeticsFile file;
            try { file = JsonConvert.DeserializeObject<CosmeticsFile>(json ?? "", WindowThemeCatalog.Json); }
            catch (Exception e) { errors.Add("cosmetics.json: " + e.Message); return ok; }
            if (file?.Cosmetics == null || file.Cosmetics.Count == 0) { errors.Add("cosmetics.json: no \"cosmetics\" array"); return ok; }
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < file.Cosmetics.Count; i++)
            {
                var c = file.Cosmetics[i];
                var probs = Validate(c);
                if (c != null && !string.IsNullOrEmpty(c.Id) && !ids.Add(c.Id)) probs.Add("duplicate id");
                if (probs.Count > 0) { errors.Add($"cosmetics.json #{i + 1} '{c?.Id}': {string.Join("; ", probs)}"); continue; }
                ok.Add(c);
            }
            errors.AddRange(ValidateCatalog(ok, StoreCatalog.Products));
            return ok;
        }

        /// <summary>Every problem with <paramref name="c"/> on its own (empty = valid).</summary>
        public static List<string> Validate(CosmeticDef c)
        {
            var p = new List<string>();
            if (c == null) { p.Add("null cosmetic"); return p; }
            if (string.IsNullOrWhiteSpace(c.Id)) p.Add("missing id");
            if (string.IsNullOrWhiteSpace(c.Name)) p.Add("missing name");
            else if (c.Name.Length > MaxNameLength) p.Add($"name longer than {MaxNameLength}");
            if (!Enum.IsDefined(typeof(CosmeticKind), c.Kind)) p.Add($"unknown kind {c.Kind}");
            if (c.Sources == null || c.Sources.Length == 0) p.Add("needs a source (pass, shards or offer)");
            else foreach (var s in c.Sources) if (!SourceNames.Contains(s)) p.Add($"source '{s}' is not one of {string.Join("/", SourceNames)}");
            if (c.Has(Shards) != (c.ShardPrice != null)) p.Add("a shards source and a shards price go together");
            if (c.ShardPrice <= 0) p.Add($"shards price {c.ShardPrice} must be positive");
            if (c.Has(Pass) != (c.PassTier != null)) p.Add("a pass source and a passTier go together");
            if (c.PassTier <= 0) p.Add($"passTier {c.PassTier} must be positive");
            bool frame = c.Kind == CosmeticKind.Frame, theme = c.Kind == CosmeticKind.Theme, backdrop = c.Kind == CosmeticKind.Backdrop;
            if (frame != (c.Colors != null)) p.Add("a frame, and only a frame, has colors");
            if (c.Colors != null)
            {
                if (c.Colors.Length < 1 || c.Colors.Length > 3) p.Add("a frame takes 1-3 colours");
                foreach (var col in c.Colors) if (!WindowThemeCatalog.TryHex(col, out _)) p.Add($"bad frame colour '{col}'");
            }
            if (c.Glow != null && (!frame || !WindowThemeCatalog.TryHex(c.Glow, out _))) p.Add($"glow '{c.Glow}' is a frame's #RRGGBB");
            if (theme != (c.Theme != null)) p.Add("a theme, and only a theme, has a theme");
            if (c.Theme != null)
            {
                foreach (var e in WindowThemeCatalog.Validate(c.Theme)) p.Add("theme: " + e);
                if (IsFile(c.Theme.Texture)) p.Add("theme texture must be procedural");
            }
            if (backdrop != (c.Gradient != null)) p.Add("a backdrop, and only a backdrop, has a gradient");
            if (c.Texture != null && !backdrop) p.Add("only a backdrop has a texture");
            if (backdrop && c.Gradient != null)
            {
                var probe = new WindowThemeDef { Name = c.Id ?? "backdrop", Gradient = c.Gradient, Texture = c.Texture };
                foreach (var e in WindowThemeCatalog.Validate(probe)) p.Add("backdrop: " + e);
                if (IsFile(c.Texture)) p.Add("backdrop texture must be procedural");
            }
            return p;
        }

        /// <summary>Catalogue-wide checks: every cosmetic a store product grants is in the catalogue with the offer source,
        /// every offer cosmetic is sold by some product, and no two themes share a name.</summary>
        public static List<string> ValidateCatalog(IReadOnlyList<CosmeticDef> all, IEnumerable<StoreProduct> products)
        {
            var p = new List<string>();
            var byId = all.Where(c => c?.Id != null).GroupBy(c => c.Id).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
            var sold = new HashSet<string>(StringComparer.Ordinal);
            foreach (var product in products ?? Enumerable.Empty<StoreProduct>())
                foreach (var id in product?.Cosmetics ?? Array.Empty<string>())
                {
                    sold.Add(id);
                    if (!byId.TryGetValue(id ?? "", out var c)) p.Add($"store product {product.Id}: unknown cosmetic '{id}'");
                    else if (!c.Has(Offer)) p.Add($"store product {product.Id}: cosmetic '{id}' lacks the offer source");
                }
            foreach (var c in byId.Values)
                if (c.Has(Offer) && !sold.Contains(c.Id)) p.Add($"cosmetic {c.Id}: offer source but no store product sells it");
            foreach (var g in all.Where(c => c?.Theme?.Name != null).GroupBy(c => c.Theme.Name, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1))
                p.Add($"theme name '{g.Key}' used by {string.Join(", ", g.Select(c => c.Id))}");
            return p;
        }

        private static bool IsFile(WindowTextureDef t) => t?.Id != null && t.Id.StartsWith(WindowThemeCatalog.FilePrefix, StringComparison.Ordinal);

        public static bool Owns(AccountWallet w, CosmeticDef c) => c != null && StoreCatalog.OwnsCosmetic(w, c.Id);

        /// <summary>Gives <paramref name="c"/> to the wallet without payment (a season pass tier). False when already owned.</summary>
        public static bool Grant(AccountWallet w, CosmeticDef c)
        {
            if (w == null || c?.Id == null) return false;
            w.Normalize();
            return w.Cosmetics.Add(c.Id);
        }

        public static bool CanBuyWithShards(AccountWallet w, CosmeticDef c) =>
            c != null && c.Has(Shards) && c.ShardPrice > 0 && !Owns(w, c) && AccountWallets.CanSpend(w, c.ShardPrice.Value);

        /// <summary>Buys <paramref name="c"/> with Shards from the wallet, all or nothing.</summary>
        public static bool BuyWithShards(AccountWallet w, CosmeticDef c)
        {
            if (!CanBuyWithShards(w, c) || !AccountWallets.Spend(w, c.ShardPrice.Value)) return false;
            return Grant(w, c);
        }

        /// <summary>The cosmetic id equipped for <paramref name="kind"/>, or null for the plain look.</summary>
        public static string Equipped(SettingsSave s, CosmeticKind kind) =>
            s?.Cosmetics != null && s.Cosmetics.TryGetValue(kind.ToString(), out var id) && !string.IsNullOrEmpty(id) ? id : null;

        /// <summary>Equips <paramref name="c"/> in its kind's slot when the wallet owns it. A theme also becomes the menu
        /// theme. False (nothing changed) when it isn't owned.</summary>
        public static bool Equip(SettingsSave s, AccountWallet w, CosmeticDef c)
        {
            if (s == null || !Owns(w, c)) return false;
            s.Cosmetics ??= new Dictionary<string, string>(StringComparer.Ordinal);
            s.Cosmetics[c.Kind.ToString()] = c.Id;
            if (c.Kind == CosmeticKind.Theme && c.Theme?.Name != null) s.MenuTheme = c.Theme.Name;
            return true;
        }

        /// <summary>Back to the plain look for <paramref name="kind"/>.</summary>
        public static void Unequip(SettingsSave s, CosmeticKind kind)
        {
            if (s?.Cosmetics == null) return;
            string id = Equipped(s, kind);
            s.Cosmetics.Remove(kind.ToString());
            if (kind == CosmeticKind.Theme && id != null) s.MenuTheme = SettingsSave.DefaultMenuTheme;
        }

        /// <summary>Drops any equipped cosmetic the wallet no longer owns (a refund, another login); true when something changed.</summary>
        public static bool Reconcile(SettingsSave s, AccountWallet w)
        {
            if (s?.Cosmetics == null) return false;
            bool changed = false;
            foreach (CosmeticKind kind in Enum.GetValues(typeof(CosmeticKind)))
            {
                string id = Equipped(s, kind);
                if (id != null && !StoreCatalog.OwnsCosmetic(w, id)) { Unequip(s, kind); changed = true; }
            }
            return changed;
        }
    }
}
