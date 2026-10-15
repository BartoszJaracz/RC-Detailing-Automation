using System.Text.RegularExpressions;
using BricsCadRc.Core;
using Rows = System.Collections.Generic.SortedDictionary<int, System.Collections.Generic.Dictionary<int, string>>;

namespace BricsCadRc.Tests
{
    /// <summary>
    /// PunchingReport.Parse — nagłówki GARAGE (POPRAWKI_WZORCA 22). Dane: ogon raportu DemoProject part 1
    /// (AB400CD004_ALL_260910_report_punching.xlsx, wiersze 274–322: PLOT 338 + 3 garaże).
    /// </summary>
    [TestFixture]
    public class PunchingReportParseTests
    {
        // ── budowanie arkusza ───────────────────────────────────────────

        private sealed class Sheet
        {
            public readonly Rows Rows = new Rows();
            private int _row = 1;
            public Sheet One(string a) { Rows[_row++] = new Dictionary<int, string> { [1] = a }; return this; }
            public Sheet Header()
            {
                Rows[_row++] = new Dictionary<int, string>
                {
                    [1] = "Node", [2] = "Pile", [3] = "Type", [4] = "Edge\n(mm)", [20] = "Util",
                    [21] = "T1\n(mm²/m)", [22] = "T2\n(mm²/m)", [23] = "Reinf.", [24] = "Status", [26] = "Source"
                };
                return this;
            }
            public Sheet Pile(string node, string pile, string type, string util, string reinf = "not required")
            {
                Rows[_row++] = new Dictionary<int, string>
                {
                    [1] = node, [2] = pile, [3] = type, [4] = "800", [20] = util, [21] = "400", [22] = "450",
                    [23] = reinf, [24] = "OK", [26] = "-"
                };
                return this;
            }
            public Sheet Blank() { _row++; return this; }
        }

        /// <summary>Ogon raportu p1: PLOT 338 (10 pali), GARAGE 121 (4), GARAGE 258 (4), GARAGE 326-327 (5), podsumowanie.</summary>
        private static Sheet DemoProjectP1Tail()
        {
            var s = new Sheet();
            s.One("Punching Check - AB400CD004_ALL_260910")
             .One("FAIL (util > 100%): P32306 (1 pile(s))").Blank();
            s.One("PLOT 338 (10 piles)").One("EDGE (5)").Header();
            foreach (var n in new[] { "33802", "33804", "33805", "33809", "33810" }) s.Pile(n, "P" + n, "Edge", "80");
            s.Blank().One("CORNER (5)").Header();
            foreach (var n in new[] { "33801", "33803", "33806", "33807", "33808" }) s.Pile(n, "P" + n, "Corner", "90", "ADD H12@200");
            s.Blank().One("GARAGE 121 (4 piles)").One("EDGE (4)").Header();
            for (int i = 1; i <= 4; i++) s.Pile("1215" + i, "G1210" + i, "Edge", "65");
            s.Blank().One("GARAGE 258 (4 piles)").One("EDGE (4)").Header();
            for (int i = 1; i <= 4; i++) s.Pile("2585" + i, "G2580" + i, "Edge", "63");
            s.Blank().One("GARAGE 326-327 (5 piles)").One("INTERNAL (1)").Header();
            s.Pile("32655", "G32605", "Internal", "66.4", "ADD H12@200");
            s.Blank().One("CORNER (4)").Header();
            for (int i = 1; i <= 4; i++) s.Pile("3265" + i, "G3260" + i, "Corner", "88.8", "ADD H16@200");
            s.Blank().Blank().One("Summary").One("Max utilization: 1.016 (Pile 32306)")
             .One("OK: 162  |  FAIL: 1  |  PCC: 0  |  NOT AFFECTED: 0  |  Total: 163");
            return s;
        }

        // ── testy ───────────────────────────────────────────────────────

        [Test]
        public void DemoProjectTail_GaragesAreSeparatePlots()
        {
            var w = new List<string>();
            var plots = PunchingReport.Parse(DemoProjectP1Tail().Rows, w);

            Assert.That(plots.Select(p => p.Label),
                Is.EqualTo(new[] { "PLOT 338", "GARAGE 121", "GARAGE 258", "GARAGE 326-327" }));
            Assert.That(plots.Select(p => p.Piles.Count), Is.EqualTo(new[] { 10, 4, 4, 5 }));
            Assert.That(plots.Select(p => p.DeclaredPiles), Is.EqualTo(new[] { 10, 4, 4, 5 }));
            Assert.That(w, Is.Empty, string.Join("; ", w));

            var g = plots[3];
            Assert.That(g.Piles.Select(p => p.PileId), Is.EqualTo(new[] { "G32605", "G32601", "G32602", "G32603", "G32604" }));
            Assert.That(g.Piles.All(p => p.Plot == "GARAGE 326-327"), Is.True);
            Assert.That(g.Piles[0].Type, Is.EqualTo("Internal"));
            Assert.That(g.Piles[1].Type, Is.EqualTo("Corner"));
            Assert.That(g.Piles[1].Reinf, Is.EqualTo("ADD H16@200"));
            Assert.That(plots[0].Piles.Any(p => p.PileId.StartsWith("G")), Is.False);
        }

        [Test]
        public void DemoProjectTail_LegacyParserAppendedGarages_ToPlot338()
        {
            // Dokumentacja błędu wzorca: dawne rozpoznanie nagłówka (tylko PLOT) — 23 pale w PLOT 338.
            var plots = LegacyHeaders(DemoProjectP1Tail().Rows);
            Assert.That(plots, Is.EqualTo(new[] { ("PLOT 338", 10, 23) }));
        }

        [Test]
        public void PlotOnlyReport_SameAsBefore()
        {
            var s = new Sheet();
            s.One("PLOT 119 (9 piles)").One("INTERNAL (2)").Header()
             .Pile("11901", "P11901", "Internal", "50").Pile("11902", "P11902", "Internal", "60").Blank()
             .One("PLOT 259-260 (13 piles) + 332-333").One("EDGE (1)").Header()
             .Pile("25901", "P25901", "Edge", "70").Blank()
             .One("PLOT 330 (8 piles) + 345").Header().Pile("33001", "P33001", "Corner", "0.95").Blank()
             .One("PLOTS 3-4").Header().Pile("301", "P301", "Edge", "40").Blank()   // nierozpoznane — pale do PLOT 330
             .One("PLOT 6-8").Header().Pile("617", "P617", "Edge", "101", "FAIL");

            var w = new List<string>();
            var plots = PunchingReport.Parse(s.Rows, w);
            var now = plots.Select(p => (p.Label, p.DeclaredPiles, p.Piles.Count)).ToArray();
            Assert.That(now, Is.EqualTo(LegacyHeaders(s.Rows)));
            Assert.That(now, Is.EqualTo(new[]
            {
                ("PLOT 119", 9, 2), ("PLOT 259-260 (13 piles) + 332-333", 0, 1),
                ("PLOT 330 (8 piles) + 345", 0, 2), ("PLOT 6-8", 0, 1)
            }));
            Assert.That(w, Is.EqualTo(new[] { "PLOT 119: nagłówek mówi 9 pali, w tabeli 2." }));
            Assert.That(plots[2].Piles[0].Util, Is.EqualTo(95.0).Within(1e-9));   // ułamek → %
        }

        [Test]
        public void DeclaredPilesMismatch_Garage_Warns()
        {
            var s = new Sheet();
            s.One("GARAGE 121 (4 piles)").Header().Pile("1", "G12101", "Edge", "60");
            var w = new List<string>();
            var plots = PunchingReport.Parse(s.Rows, w);
            Assert.That(plots.Single().Label, Is.EqualTo("GARAGE 121"));
            Assert.That(w, Is.EqualTo(new[] { "GARAGE 121: nagłówek mówi 4 pali, w tabeli 1." }));
        }

        [Test]
        public void ReportWithoutPlotHeader_OneImplicitPlot()
        {
            var s = new Sheet();
            s.One("Punching Shear Verification EC2 §6.4").One("Punching Check - FEM_AB500CD001")
             .One(" CHECK PERIMETER: control perimeter in doubt at 5 pile(s): P305, P306, P309, P312, P313.").Blank();
            s.One("INTERNAL (2)").Header().Pile("314", "P314", "Internal", "88.2", "ADD H16@100")
             .Pile("315", "P315", "Internal", "89.6", "ADD H16@100").Blank();
            s.One("EDGE (1)").Header().Pile("302", "P302", "Edge", "95.1", "ADD H16@200").Blank();
            s.One("CORNER (1)").Header().Pile("301", "P301", "Corner", "93.1", "ADD H12@200").Blank().Blank();
            s.One("Summary").One("Max utilization: 0.961 (Pile 309)");

            var w = new List<string>();
            var plots = PunchingReport.Parse(s.Rows, w);
            var p = plots.Single();
            Assert.That(p.Implicit, Is.True);
            Assert.That(p.Label, Is.EqualTo(PunchingReport.ImplicitLabel));
            Assert.That(p.Piles.Select(x => x.PileId), Is.EqualTo(new[] { "P314", "P315", "P302", "P301" }));
            Assert.That(p.Piles.Select(x => x.Reinf), Is.EqualTo(new[] { "ADD H16@100", "ADD H16@100", "ADD H16@200", "ADD H12@200" }));
            Assert.That(w, Is.EqualTo(new[] { "Raport bez nagłówka PLOT / GARAGE — wszystkie pale jako jedna płyta." }));
        }

        /// <summary>Dawne rozpoznanie nagłówka (PunchingReport sprzed zmiany): (etykieta, zadeklarowane, pale w tabeli).</summary>
        private static (string, int, int)[] LegacyHeaders(Rows rows)
        {
            var rx = new Regex(@"^\s*(PLOT\b.*?)\s*(?:\(\s*(\d+)\s*piles?\s*\))?\s*$", RegexOptions.IgnoreCase);
            var res = new List<(string label, int decl, int n)>();
            bool header = false;
            foreach (var c in rows.Values)
            {
                string? a = c.TryGetValue(1, out var v) ? v : null;
                var m = a != null ? rx.Match(a) : Match.Empty;
                if (m.Success && a!.TrimStart().StartsWith("PLOT", StringComparison.OrdinalIgnoreCase) && c.Count == 1)
                {
                    res.Add((m.Groups[1].Value.Trim(), int.TryParse(m.Groups[2].Value, out int d) ? d : 0, 0));
                    continue;
                }
                if (c.Values.Any(x => x == "Pile")) { header = true; continue; }
                if (!header || res.Count == 0 || !c.TryGetValue(2, out var pile) || string.IsNullOrWhiteSpace(pile)) continue;
                var last = res[res.Count - 1];
                res[res.Count - 1] = (last.label, last.decl, last.n + 1);
            }
            return res.Select(r => (r.label, r.decl, r.n)).ToArray();
        }
    }
}
