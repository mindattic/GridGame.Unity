using GridGame.Meta;
using NUnit.Framework;

namespace GridGame.Tests
{
    /// <summary>
    /// GG-T563: a window theme's Brightness/Contrast/Gamma. 0.5 everywhere is the theme exactly as authored; each bar
    /// moves the look either way within ranges that stay usable; a theme with no values reads 0.5; text stays 4.5:1.
    /// </summary>
    public class WindowToneTests
    {
        [Test]
        public void Neutral_is_the_identity()
        {
            var t = WindowTone.Default;
            Assert.That(t.IsNeutral);
            for (int i = 0; i <= 255; i++) Assert.That(t.Map(i / 255f), Is.EqualTo(i / 255f));
            Assert.That(t.MapHex("#2C46AA"), Is.EqualTo("#2C46AA"));
        }

        [Test]
        public void The_ranges_at_the_ends()
        {
            Assert.That(WindowTone.Offset(0f), Is.EqualTo(-0.25f).Within(1e-6));
            Assert.That(WindowTone.Offset(1f), Is.EqualTo(0.25f).Within(1e-6));
            Assert.That(WindowTone.Factor(0f), Is.EqualTo(0.5f).Within(1e-6));
            Assert.That(WindowTone.Factor(1f), Is.EqualTo(2f).Within(1e-6));
            Assert.That(WindowTone.Exponent(0f), Is.EqualTo(2f).Within(1e-6));
            Assert.That(WindowTone.Exponent(1f), Is.EqualTo(0.5f).Within(1e-6));
            Assert.That(WindowTone.Offset(0.5f), Is.EqualTo(0f));
            Assert.That(WindowTone.Factor(0.5f), Is.EqualTo(1f));
            Assert.That(WindowTone.Exponent(0.5f), Is.EqualTo(1f));
        }

        [Test]
        public void Each_bar_moves_both_ways()
        {
            float mid = 0.4f;
            Assert.That(new WindowTone(1f, 0.5f, 0.5f).Map(mid), Is.GreaterThan(mid));
            Assert.That(new WindowTone(0f, 0.5f, 0.5f).Map(mid), Is.LessThan(mid));
            Assert.That(new WindowTone(0.5f, 1f, 0.5f).Map(0.2f), Is.LessThan(0.2f), "more contrast pushes darks darker");
            Assert.That(new WindowTone(0.5f, 0f, 0.5f).Map(0.2f), Is.GreaterThan(0.2f), "less contrast pulls darks toward gray");
            Assert.That(new WindowTone(0.5f, 0.5f, 1f).Map(mid), Is.GreaterThan(mid), "higher gamma lightens mid-tones");
            Assert.That(new WindowTone(0.5f, 0.5f, 0f).Map(mid), Is.LessThan(mid));
            Assert.That(new WindowTone(0.5f, 0.5f, 0f).Map(0f), Is.EqualTo(0f), "gamma keeps black");
            Assert.That(new WindowTone(0.5f, 0.5f, 0f).Map(1f), Is.EqualTo(1f), "gamma keeps white");
        }

        [Test]
        public void Usable_at_the_extremes_no_sheet_of_white_or_black()
        {
            // The Classic blue's two ends stay distinct colours even with every bar at an end.
            foreach (var t in new[] { new WindowTone(1f, 1f, 1f), new WindowTone(0f, 0f, 0f), new WindowTone(1f, 0f, 1f), new WindowTone(0f, 1f, 0f) })
                Assert.That(t.MapHex("#2C46AA"), Is.Not.EqualTo(t.MapHex("#0A1246")), t.ToString());
            Assert.That(new WindowTone(0f, 0f, 0f).Map(1f), Is.GreaterThan(0.4f));
            Assert.That(new WindowTone(1f, 0f, 1f).Map(0f), Is.LessThan(0.6f));
        }

        [Test]
        public void A_theme_without_values_reads_half_and_saves_as_before()
        {
            const string json = "{\"name\":\"Classic\",\"gradient\":{\"mode\":\"linear\",\"angle\":180,\"stops\":[{\"at\":0,\"color\":\"#2C46AA\"},{\"at\":1,\"color\":\"#0A1246\"}],\"grain\":0},\"frame\":{\"style\":\"classic\"}}";
            var t = WindowThemeDef.FromJson(json);
            Assert.That(t.Tone.IsNeutral);
            Assert.That(t.ToJson(), Does.Not.Contain("brightness").And.Not.Contain("gamma"));
            t.Tone = new WindowTone(0.7f, 0.5f, 0.3f);
            var back = WindowThemeDef.FromJson(t.ToJson());
            Assert.That(back.Tone.Brightness, Is.EqualTo(0.7f).Within(1e-6));
            Assert.That(back.Tone.Contrast, Is.EqualTo(0.5f));
            Assert.That(back.Tone.Gamma, Is.EqualTo(0.3f).Within(1e-6));
            Assert.That(back.Contrast, Is.Null, "a neutral value stays out of the JSON");
            Assert.That(WindowThemeCatalog.Validate(back), Is.Empty);
            back.Gamma = 1.5f;
            Assert.That(WindowThemeCatalog.Validate(back), Has.Some.Contains("gamma"));
        }

        [Test]
        public void Text_keeps_four_and_a_half_to_one()
        {
            var bright = (0.6f, 0.7f, 1f);   // a blue window pushed bright
            var white = (0.957f, 0.957f, 0.965f);
            var r = WindowTone.Readable(white, bright);
            float lw = WindowTone.Luminance(bright.Item1, bright.Item2, bright.Item3);
            Assert.That(WindowTone.Ratio(WindowTone.Luminance(r.r, r.g, r.b), lw), Is.GreaterThanOrEqualTo(4.5f));
            var dark = (0.1f, 0.15f, 0.4f);
            Assert.That(WindowTone.Readable(white, dark), Is.EqualTo(white), "already readable: unchanged");
        }

        [Test]
        public void Dim_captions_keep_three_to_one()
        {
            // GG-T564: the Dark theme's dim gray on its window pushed to full brightness and gamma.
            var tone = new WindowTone(1f, 0.5f, 1f);
            float w = tone.Map(28 / 255f);
            var window = (w, w, tone.Map(34 / 255f));
            var dim = (0.6f, 0.6f, 0.6f);
            float lw = WindowTone.Luminance(window.Item1, window.Item2, window.Item3);
            Assert.That(WindowTone.Ratio(WindowTone.Luminance(0.6f, 0.6f, 0.6f), lw), Is.LessThan(WindowTone.CaptionContrast), "the case needs the floor");
            var r = WindowTone.Readable(dim, window, WindowTone.CaptionContrast);
            Assert.That(WindowTone.Ratio(WindowTone.Luminance(r.r, r.g, r.b), lw), Is.GreaterThanOrEqualTo(WindowTone.CaptionContrast));
        }
    }
}
