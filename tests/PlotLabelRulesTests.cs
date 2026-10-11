using BricsCadRc.Core;

namespace BricsCadRc.Tests
{
    /// <summary>Nazwy płyt PLOT / GARAGE (POPRAWKI_WZORCA 22) — opis w GA, nagłówek raportu przebicia, klucz RC_PH.</summary>
    [TestFixture]
    public class PlotLabelRulesTests
    {
        [TestCase("PLOT 258", true)]
        [TestCase("PLOT 9-10", true)]
        [TestCase("  plot 6-8 ", true)]
        [TestCase("PLOTS 124-125", true)]
        [TestCase("PLOT 211 & 322", true)]
        [TestCase("GARAGE 121", true)]
        [TestCase("Garage 326-327", true)]
        [TestCase("GARAGES 277-278 & 326-327", true)]
        [TestCase("GARAGE DOOR", false)]
        [TestCase("GARAGE", false)]
        [TestCase("SSL=21.925", false)]
        [TestCase("225mm THK SLAB", false)]
        [TestCase("PLOT", false)]
        [TestCase("", false)]
        [TestCase(null, false)]
        public void IsPlotLabel(string? s, bool expected)
            => Assert.That(PlotLabelRules.IsPlotLabel(s!), Is.EqualTo(expected));

        [TestCase("PLOT 119 (9 piles)", "PLOT 119", 9)]
        [TestCase("PLOT 338 (10 piles)", "PLOT 338", 10)]
        [TestCase("PLOT 6-8", "PLOT 6-8", 0)]
        [TestCase("  PLOT 1 (1 pile)  ", "PLOT 1", 1)]
        // nawias nie na końcu — dzisiejsza logika: cały tekst, 0 pali (1:1 jak przed zmianą)
        [TestCase("PLOT 259-260 (13 piles) + 332-333", "PLOT 259-260 (13 piles) + 332-333", 0)]
        [TestCase("PLOT 330 (8 piles) + 345", "PLOT 330 (8 piles) + 345", 0)]
        [TestCase("GARAGE 121 (4 piles)", "GARAGE 121", 4)]
        [TestCase("GARAGE 326-327 (5 piles)", "GARAGE 326-327", 5)]
        [TestCase("garage 258", "garage 258", 0)]
        public void ReportHeader_Recognized(string a, string label, int piles)
        {
            Assert.That(PlotLabelRules.TryParseReportHeader(a, out var l, out var n), Is.True);
            Assert.That(l, Is.EqualTo(label));
            Assert.That(n, Is.EqualTo(piles));
        }

        [TestCase("PLOTS 3-4")]                 // jak dziś — nierozpoznane
        [TestCase("GARAGE DOOR")]
        [TestCase("EDGE (5)")]
        [TestCase("INTERNAL (2)")]
        [TestCase("FAIL (util > 100%): P32306 (1 pile(s))")]
        [TestCase("Summary")]
        [TestCase("Punching Check - AB400CD004_ALL_260910")]
        [TestCase("")]
        [TestCase(null)]
        public void ReportHeader_NotRecognized(string? a)
            => Assert.That(PlotLabelRules.TryParseReportHeader(a!, out _, out _), Is.False);

        [TestCase("PLOT 338", true)]
        [TestCase("PLOT 259-260 (13 piles) + 332-333", true)]
        [TestCase("GARAGE 121", true)]
        [TestCase("garage 326-327", true)]
        [TestCase("", false)]
        [TestCase(null, false)]
        [TestCase("GARAGE DOOR", false)]
        [TestCase("P617", false)]
        public void IsReportPlotKey(string? key, bool expected)
            => Assert.That(PlotLabelRules.IsReportPlotKey(key!), Is.EqualTo(expected));

        // POPRAWKI_WZORCA 28: mapa ↔ płyta (AutoRebar Add) — cała etykieta, nie sam prefiks
        [TestCase("PLOT 326", "PLOT 326", true)]
        [TestCase("plot  326", "PLOT 326", true)]
        [TestCase("PLOT 326h = 225 mm | fck = 32", "PLOT 326", true)]
        [TestCase("PLOT 326 (B1)", "PLOT 326", true)]
        [TestCase("PLOT 326", "PLOT 32", false)]
        [TestCase("PLOT 32-33", "PLOT 32", false)]
        [TestCase("PLOT 32", "PLOT 326", false)]
        [TestCase("GARAGE 121", "GARAGE 12", false)]
        [TestCase("PLOT 326", null, false)]
        public void MapPlotMatches(string map, string? slab, bool expected)
            => Assert.That(PlotLabelRules.MapPlotMatches(map, slab!), Is.EqualTo(expected));
    }
}
