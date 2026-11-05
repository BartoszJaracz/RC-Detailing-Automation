using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using BricsCadRc.Core;

namespace BricsCadRc.Tests
{
    /// <summary>
    /// Złoty test SectionPlanner (plan bezpiecznika przekrojów, etap 1 krok 1): pełny zrzut Result (wszystkie pola sprzed
    /// bezpiecznika, liczby znormalizowane do siatki 1e-6 mm — <see cref="N"/>, niezależne od platformy) dla 5 wejść, skrót SHA-256 zapisany na NIEZMIENIONYM kodzie planera.
    /// Pola dopisane później (UsedDists, UsedPiles, LevelDiameters, Source*) NIE wchodzą do zrzutu — nie mogą zmienić wyniku.
    /// Zmiana geometrii przekroju = zmiana wyniku względem wzorca baseline (wymaga zgody użytkownika) — wtedy też podbić
    /// <see cref="SectionPlanner.Version"/>.
    /// </summary>
    [TestFixture]
    public class SectionPlannerGoldenTests
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // ── zrzut ───────────────────────────────────────────────────────

        private static string R(double v) => v.ToString("R", Inv);

        /// <summary>
        /// Liczba do skrótu złotego: siatka 1e-6 mm (= podstawowa tolerancja SectionPlanner), format „F6” w InvariantCulture,
        /// bez zer końcowych, „-0” → „0”. Usuwa różnice platformowe rzędu 1 ulp (Math.Sin / Math.Atan2: Windows UCRT vs Linux
        /// glibc), a każda zmiana wartości ≥ 1e-6 mm zmienia zapis.
        /// </summary>
        internal static string N(double v)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) return R(v);
            string s = v.ToString("F6", Inv);
            if (s.IndexOf('.') >= 0) s = s.TrimEnd('0').TrimEnd('.');
            return s == "-0" ? "0" : s;
        }

        /// <summary>Pełny zrzut bit w bit (format „R”) — do porównań w obrębie jednego uruchomienia.</summary>
        public static string Dump(SectionPlanner.Result r) => Dump(r, R);

        /// <summary>Zrzut znormalizowany (<see cref="N"/>) — podstawa skrótów złotych, niezależny od platformy.</summary>
        public static string DumpNormalized(SectionPlanner.Result r) => Dump(r, N);

        private static string Dump(SectionPlanner.Result r, Func<double, string> num)
        {
            var sb = new StringBuilder();
            void P(IEnumerable<(double U, double V)>? pts, string name)
            {
                sb.Append(name).Append('=');
                if (pts == null) { sb.Append("null"); return; }
                foreach (var p in pts) sb.Append(num(p.U)).Append(',').Append(num(p.V)).Append(';');
            }
            sb.Append("UMin=").Append(num(r.UMin)).Append(" UMax=").Append(num(r.UMax)).Append(" NibTop=").Append(num(r.NibTop)).Append('\n');
            foreach (var s in r.Segments)
            {
                sb.Append("SEG ").Append(num(s.U0)).Append(' ').Append(num(s.U1)).Append(' ').Append(s.End0).Append(' ').Append(s.End1)
                  .Append(' ').Append(num(s.Nib0)).Append(' ').Append(num(s.Nib1)).Append(' ');
                P(s.Polygon, "poly");
                sb.Append('\n');
            }
            foreach (var d in r.Dots)
                sb.Append("DOT ").Append(num(d.U)).Append(' ').Append(num(d.V)).Append(' ').Append(d.Diameter).Append(' ').Append(d.PosNr)
                  .Append(' ').Append(d.Top).Append(' ').Append(d.Mesh).Append(' ').Append(d.TinNib).Append(' ').Append(num(d.NomSpacing)).Append('\n');
            foreach (var b in r.Bars)
            {
                sb.Append("BAR ").Append(b.Diameter).Append(' ').Append(b.PosNr).Append(' ').Append(b.Top).Append(' ').Append(b.Kind).Append(' ');
                P(b.Path, "path"); sb.Append(' ');
                P(b.Outline, "outline");
                sb.Append('\n');
            }
            foreach (var p in r.Piles)
                sb.Append("PILE ").Append(num(p.U)).Append(' ').Append(num(p.Width)).Append(' ').Append(num(p.Top)).Append(' ').Append(num(p.Bottom)).Append('\n');
            foreach (var m in r.Dims)
                sb.Append("DIM ").Append(num(m.U1)).Append(' ').Append(num(m.V1)).Append(' ').Append(num(m.U2)).Append(' ').Append(num(m.V2))
                  .Append(' ').Append(num(m.LineU)).Append(' ').Append(num(m.LineV)).Append(' ').Append(m.Vertical).Append('\n');
            foreach (var l in r.Labels)
                sb.Append("LBL ").Append(l.Text).Append(' ').Append(num(l.U)).Append(' ').Append(num(l.V)).Append(' ').Append(num(l.Height))
                  .Append(' ').Append(l.HasLeader).Append(' ').Append(num(l.LeaderU)).Append(' ').Append(num(l.LeaderV)).Append('\n');
            sb.Append("SSL ").Append(r.SslMark.HasValue ? num(r.SslMark.Value.U) + "," + num(r.SslMark.Value.V) : "null")
              .Append(' ').Append(r.SslText ?? "null").Append('\n');
            foreach (var w in r.Warnings) sb.Append("WARN ").Append(w).Append('\n');
            return sb.ToString();
        }

        private static string Sha(string s)
        {
            using var sha = SHA256.Create();
            return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(s)).Select(b => b.ToString("x2")));
        }

        // ── wejścia ─────────────────────────────────────────────────────

        internal static List<(double X, double Y)> Rect(double x0, double y0, double x1, double y1)
            => new List<(double X, double Y)> { (x0, y0), (x1, y0), (x1, y1), (x0, y1) };

        internal static SectionPlanner.Dist D(string mark, string layer, bool alongX, double x0, double y0, double lenA, double span,
                                             int count, double spacing, int dia, string shape = "00", string side = "",
                                             List<(double X, double Y)>? local = null)
        {
            string pos = SectionPlanner.PosNrOf(mark);
            return new SectionPlanner.Dist
            {
                Mark = mark, PosNr = pos, LayerCode = layer, Kind = SectionPlanner.KindOf(mark, pos, 3), AlongX = alongX,
                X0 = x0, Y0 = y0, LengthA = lenA, BarsSpan = span, Count = count, Spacing = spacing, Diameter = dia,
                ShapeCode = shape, SymbolSide = side, ShapeLocal = local!
            };
        }

        /// <summary>1. Nib z obu stron (krawędzie pionowe), UB 01 i UB 03 (kopia pręta wzorcowego), T IN NIB, SSL.</summary>
        public static SectionPlanner.Input CaseNib()
        {
            var inp = new SectionPlanner.Input
            {
                Outline = Rect(0, 0, 6000, 4000), CutAlongX = true, CutCoord = 2000, S0 = -500, S1 = 6500, LookSign = 1,
                Thickness = 225, NibHeight = 150, Ssl = "21.925"
            };
            inp.Nibs.Add(new SectionPlanner.NibEdge { Vertical = true, OuterCoord = 0, InnerCoord = 130, Lo = 0, Hi = 4000 });
            inp.Nibs.Add(new SectionPlanner.NibEdge { Vertical = true, OuterCoord = 6000, InnerCoord = 5870, Lo = 0, Hi = 4000 });
            inp.Dists.Add(D("H10-05-200 B1", "B1", true, 40, 40, 5920, 3920, 20, 200, 10));
            inp.Dists.Add(D("H10-06-200 B2", "B2", false, 40, 40, 3920, 5920, 30, 200, 10));
            inp.Dists.Add(D("H12-104-200 T1", "T1", true, 170, 40, 5660, 3920, 20, 200, 12));
            inp.Dists.Add(D("H12-105-200 T2", "T2", false, 40, 170, 3660, 5920, 30, 200, 12));
            inp.Dists.Add(D("20 H12-01-200 UB", "T1", true, 130, 100, 700, 3800, 20, 200, 12, "21", "Left",
                            new List<(double X, double Y)> { (700, 0), (0, 0), (0, 128), (700, 128) }));
            inp.Dists.Add(D("20 H10-03-200 UB", "B1", true, 40, 100, 610, 3800, 20, 200, 10, "21", "Left",
                            new List<(double X, double Y)> { (610, 0), (0, 0), (0, 60), (610, 60) }));
            inp.Dists.Add(D("2 H12-101-150 T IN NIB", "T1", false, 40, 40, 3920, 150, 2, 150, 12));
            return inp;
        }

        /// <summary>2. Otwór przecięty linią, końce cięcia w płycie (urwanie), bez SSL.</summary>
        public static SectionPlanner.Input CaseHole()
        {
            var inp = new SectionPlanner.Input
            {
                Outline = Rect(0, 0, 8000, 5000), CutAlongX = true, CutCoord = 2500, S0 = 1000, S1 = 7000, LookSign = 1, Thickness = 300
            };
            inp.Holes.Add((3000, 2000, 4000, 3000));
            inp.Dists.Add(D("H10-07-200 B1", "B1", false, 40, 40, 4920, 7920, 40, 200, 10));
            inp.Dists.Add(D("H10-08-200 B2", "B2", true, 40, 40, 7920, 4920, 25, 200, 10));
            inp.Dists.Add(D("H12-109-150 T1", "T1", false, 40, 40, 4920, 7920, 53, 150, 12));
            inp.Dists.Add(D("H12-110-200 T2", "T2", true, 40, 40, 7920, 4920, 25, 200, 12));
            return inp;
        }

        /// <summary>3. Pale: dwa przecięte, jeden daleko.</summary>
        public static SectionPlanner.Input CasePiles()
        {
            var inp = new SectionPlanner.Input
            {
                Outline = Rect(0, 0, 5000, 5000), CutAlongX = true, CutCoord = 2500, S0 = -100, S1 = 5100, LookSign = 1, Thickness = 225
            };
            inp.Piles.Add((1500, 2500, 300));
            inp.Piles.Add((3500, 2600, 300));
            inp.Piles.Add((2500, 4000, 300));
            inp.Dists.Add(D("H10-11-200 B1", "B1", false, 40, 40, 4920, 4920, 25, 200, 10));
            inp.Dists.Add(D("H10-12-200 B2", "B2", true, 40, 40, 4920, 4920, 25, 200, 10));
            return inp;
        }

        /// <summary>4. Cięcie pionowe, patrzenie w stronę malejącego X (LookSign −1), nib na krawędzi dolnej.</summary>
        public static SectionPlanner.Input CaseVertical()
        {
            var inp = new SectionPlanner.Input
            {
                Outline = Rect(0, 0, 6000, 4000), CutAlongX = false, CutCoord = 3000, S0 = -200, S1 = 4200, LookSign = -1,
                Thickness = 225, Ssl = "20.500"
            };
            inp.Nibs.Add(new SectionPlanner.NibEdge { Vertical = false, OuterCoord = 0, InnerCoord = 125, Lo = 0, Hi = 6000 });
            inp.Dists.Add(D("H10-13-200 B1", "B1", true, 40, 40, 5920, 3920, 20, 200, 10));
            inp.Dists.Add(D("H10-14-200 B2", "B2", false, 40, 40, 3920, 5920, 30, 200, 10));
            inp.Dists.Add(D("H12-115-200 T1", "T1", true, 40, 165, 5920, 3795, 19, 200, 12));
            inp.Dists.Add(D("H12-116-200 T2", "T2", false, 40, 165, 3795, 5920, 30, 200, 12));
            return inp;
        }

        /// <summary>5. Pręty kształtowe w widoku (FitShape): L dołem, U górą.</summary>
        public static SectionPlanner.Input CaseFitShape()
        {
            var inp = new SectionPlanner.Input
            {
                Outline = Rect(0, 0, 6000, 3000), CutAlongX = true, CutCoord = 1500, S0 = -100, S1 = 6100, LookSign = 1, Thickness = 250
            };
            inp.Dists.Add(D("H12-17-200 B1", "B1", true, 40, 40, 1500, 2900, 15, 200, 12, "11", "",
                            new List<(double X, double Y)> { (0, 0), (1500, 0), (1500, 200) }));
            inp.Dists.Add(D("H12-118-200 T1", "T1", true, 3000, 40, 2000, 2900, 15, 200, 12, "21", "",
                            new List<(double X, double Y)> { (0, -150), (0, 0), (2000, 0), (2000, -150) }));
            inp.Dists.Add(D("H10-19-200 B2", "B2", false, 40, 40, 2920, 5920, 30, 200, 10));
            return inp;
        }

        /// <summary>Pręt belki (Kind = Beam, kody BB / BT / BL — plan belek E5).</summary>
        public static SectionPlanner.Dist B(string pos, string layer, bool alongX, double x0, double y0, double lenA, double span,
                                            int count, double spacing, int dia, string shape = "00", List<(double X, double Y)>? local = null,
                                            double[]? dims = null)
            => new SectionPlanner.Dist
            {
                Mark = "H" + dia + "-" + pos, PosNr = pos, LayerCode = layer, Kind = SectionPlanner.DistKind.Beam, AlongX = alongX,
                X0 = x0, Y0 = y0, LengthA = lenA, BarsSpan = span, Count = count, Spacing = spacing, Diameter = dia, ShapeCode = shape,
                ShapeLocal = local!, SourceDims = dims!
            };

 /// <summary>Oś strzemienia 51 (A = 265, B = 510, H10 — otuliny: boki 45, dół 50) — jak pręt wzorcowy z rysunku.</summary>
        public static List<(double X, double Y)> Link51() => BarGeometryBuilder.GetLocalPoints("51", new double[] { 265, 510, 0, 0, 0 }, 10);

        /// <summary>
        /// 6. Belka krawędziowa (plan belek E5, jak 528: H = 375, nib 160 × 150): strefa x 160…760 wzdłuż Y przy lewej krawędzi z nibem,
        /// cięcie poziome y = 2000 prostopadle do belki (koniec w płycie — urwanie), siatka B1 / B2 / T1 / T2, pręty belki 4 H16 dołem
        /// (BB, 05) i górą (BT, 103), strzemiona 51 (BL, 07) w widoku, pal pod belką; E4b: pręty boczne (BS, 20), w nibie (BN, 21 / 22)
        /// w narożach spinki UB 03 (03), T IN NIB (101) — pierwszy w nibie, drugi w trzonie belki.
        /// Otuliny belki (decyzja): boczna / czołowa 45, dolna 50 — położenia prętów belki jak z generowania.
        /// </summary>
        public static SectionPlanner.Input CaseBeam()
        {
            var inp = new SectionPlanner.Input
            {
                Outline = Rect(0, 0, 6000, 4000), CutAlongX = true, CutCoord = 2000, S0 = -500, S1 = 3000, LookSign = 1,
                Thickness = 225, NibHeight = 150, Ssl = "21.925"
            };
            inp.Nibs.Add(new SectionPlanner.NibEdge { Vertical = true, OuterCoord = 0, InnerCoord = 160, Lo = 0, Hi = 4000 });
            inp.Piles.Add((460, 2000, 300));
            inp.Dists.Add(D("H10-11-200 B1", "B1", true, 40, 40, 5920, 3920, 20, 200, 10));
            inp.Dists.Add(D("H10-12-200 B2", "B2", false, 40, 40, 3920, 5920, 30, 200, 10));
            inp.Dists.Add(D("H12-111-200 T1", "T1", true, 200, 40, 5760, 3920, 20, 200, 12));
            inp.Dists.Add(D("H12-112-200 T2", "T2", false, 200, 40, 3920, 5760, 29, 200, 12));
            double sp = (600 - 2 * 63.0) / 3;      // 45 + 10 + 8 = 63 od lic → co 158
            inp.Dists.Add(B("05", "BB", false, 223, 45, 3910, 3 * sp, 4, sp, 16));
            inp.Dists.Add(B("103", "BT", false, 223, 45, 3910, 3 * sp, 4, sp, 16));
            inp.Dists.Add(B("07", "BL", true, 205, 120, 510, 3760, 20, 3760 / 19.0, 10, "51", Link51(), new double[] { 265, 510, 0, 0, 0 }));
            // E4b: pręty boczne H10 (po 1 na bok przy ramionach strzemienia) i pręty w nibie (górą H10, dołem H16 w narożach spinki UB 03;
            // w poprzek od spinki płyty — otulina 40 bez zmian, wzdłuż — otulina czołowa 45)
            inp.Dists.Add(B("20", "BS", false, 220, 45, 3910, 480, 2, 480, 10));
            var nt = B("21", "BN", false, 55, 45, 3910, 0, 1, 200, 10); nt.Mark = "H10-21 BEAM NIB T"; inp.Dists.Add(nt);
            var nb = B("22", "BN", false, 58, 45, 3910, 0, 1, 200, 16); nb.Mark = "H16-22 BEAM NIB B"; inp.Dists.Add(nb);
            // poprawki recenzji E4b: spinka UB 03 w nibie belki (pręty BN w jej narożach) i T IN NIB płyty (drugi pręt w trzonie belki)
            inp.Dists.Add(D("20 H10-03-200 UB", "B1", true, 40, 100, 610, 3800, 20, 200, 10, "21", "Left",
                            new List<(double X, double Y)> { (610, 0), (0, 0), (0, 60), (610, 60) }));
            inp.Dists.Add(D("2 H12-101-150 T IN NIB", "T1", false, 40, 40, 3920, 150, 2, 150, 12));
            inp.Beams.Add(new SectionPlanner.BeamBand { Handle = "000002AA", S0 = 160, S1 = 760, Height = 375, NibHeight = 150, StirrupDia = 10, StirrupA = 265,
                                                     CoverBottom = 50 });   // rekord sprzed (otulina dolna 50) — złoty wynik bez zmian
            return inp;
        }

        public static IEnumerable<TestCaseData> Cases()
        {
            yield return new TestCaseData("nib", (Func<SectionPlanner.Input>)CaseNib);
            yield return new TestCaseData("hole", (Func<SectionPlanner.Input>)CaseHole);
            yield return new TestCaseData("piles", (Func<SectionPlanner.Input>)CasePiles);
            yield return new TestCaseData("vertical", (Func<SectionPlanner.Input>)CaseVertical);
            yield return new TestCaseData("fitshape", (Func<SectionPlanner.Input>)CaseFitShape);
            yield return new TestCaseData("beam", (Func<SectionPlanner.Input>)CaseBeam);
 // wersja 3 (zakończenia prętów Z1–Z6, decyzja użytkownika): te same wejścia w stylu S1 „UK tick” (kreska 30 mm, decyzja)
            foreach (var (name, make) in new (string, Func<SectionPlanner.Input>)[]
                     { ("nib", CaseNib), ("hole", CaseHole), ("fitshape", CaseFitShape), ("beam", CaseBeam), ("along", CaseBeamAlong) })
                yield return new TestCaseData(name + "-" + SectionPlanner.EndStyleShort(SectionPlanner.EndStyle.Tick), Styled(make, SectionPlanner.EndStyle.Tick));
        }

        /// <summary>Wejście w stylu zakończeń <paramref name="st"/>.</summary>
        public static Func<SectionPlanner.Input> Styled(Func<SectionPlanner.Input> make, SectionPlanner.EndStyle st)
            => () => { var i = make(); i.EndStyle = st; return i; };

        /// <summary>
        /// 7. (tylko style wersji 3) Przekrój WZDŁUŻ belki krawędziowej (jak CaseBeam, cięcie pionowe x = 460 w trzonie belki, pas belki na całej długości cięcia):
        /// pręty belki w widoku z rzeczywistymi końcami (BB / BT — 45 od lica końca), strzemiona jako pionowe ramiona, płyta urwana.
        /// </summary>
        public static SectionPlanner.Input CaseBeamAlong()
        {
            var inp = CaseBeam();
            inp.CutAlongX = false; inp.CutCoord = 460; inp.S0 = -200; inp.S1 = 2500; inp.LookSign = -1;
            inp.Beams[0].S0 = 0; inp.Beams[0].S1 = 4000;      // strefa belki wzdłuż krawędzi — pas na całej linii cięcia
            return inp;
        }

        /// <summary>Skróty zapisane na niezmienionym kodzie SectionPlanner (przed polami Used* / Source*).</summary>
        private static readonly Dictionary<string, string> Golden = new Dictionary<string, string>
        {
 // v6 (decyzja użytkownika): nib / nib-S1 — T IN NIB leży na górnym ramieniu UB 03 (−126 → −104); piles / beam / beam-S1 /
            // along-S1 — pal: głowica 30 (było 35), 226 pod spodem (było 230), opisy nie przecinają linii pala (boki, oś) — sprawdzone na zrzutach
            ["nib"]      = "e9f9e90b32207fc37236a58d8fa4ce05e4d79b2d0cdae5b94b7fc47090e141dc",
            ["hole"]     = "cf9c88950c7911e95abdc2279fcd0bd737b7f83bf76131f6beccaccf1d420164",
 // (uwaga „nasze pale mają 300”): pal przecięty poza osią — pełna średnica, nie cięciwa (drugi pal 566 → 600)
            ["piles"]    = "4b16fe5ed013879013ab00842283c38be96e2f93d29061b87548a537b2e82d5f",
            ["vertical"] = "b8ac945b3ea9cfd533288e3769b0542c6534a246a8c45351c9b9950fb01761c7",
            ["fitshape"] = "4e8701415eba0a00c0b334666e909fc43a3774ca413a915a789150d35c0ff7c8",
 // plan belek E5 + E4b: przekrój przez belkę — zapisany po przejrzeniu zrzutu (profil, kropki, strzemię, boczne, nib, wymiary);
            // poprawki recenzji: + UB 03 i T IN NIB w wejściu, BN w narożach spinki, T IN NIB w trzonie na poziomie płyty;
 // otuliny belki (boki 45, dół 50): strzemię 265 × 510, kropki 223 … 697 / −307; K2: bez
            // ostrzeżenia o otulinie UB 03 (jedyna różnica względem 644610bd… — sprawdzone: zrzut + dawna linia WARN = dawny skrót)
            ["beam"]     = "15139c3c6801ce02c6c7082ec0ff1300887bda7a2ed57b4c6a9ea654ed6a5287",
 // wersja 3 (zakończenia prętów Z1–Z6, decyzja użytkownika) — zapisane po przejrzeniu zrzutów (render: kreski
            // do wnętrza, w betonie, zasięg prętów bez zmian, zakład T1 104 / B1 05 odsunięty o 25, UB 01 / 03 — symbole na wolnych końcach,
            // strzemię bez symboli); „hole” w stylach = „hole” bez stylu (Z4: pręty przechodzą przez otwór / urwanie);
 // kreska 30 mm: względem 50 mm zmieniają się tylko linie BAR z kreskami (ramię skrócone o 30/√2), opisy bez zmian
            ["nib-S1"]      = "6a5412cbb4be25a255bff97259e23685faad1b88b4ceae3e904a012ab5c84146",   // v5: bez odsunięcia zakładów
            ["hole-S1"]     = "cf9c88950c7911e95abdc2279fcd0bd737b7f83bf76131f6beccaccf1d420164",
            ["fitshape-S1"] = "b19dd393e920929e86d8dc574782efebe65aad7f8dd9111e45ee22f0a84e25e8",
            ["beam-S1"]     = "a466ef6ed9a05bd0f9264785a09d182a375e7998c0de2e0e611cfffd8afe14ac",
            ["along-S1"]    = "dcbcf38dd72adcdb7f8885a5eab6c41c4c8196139761fe5f6d693b190d706823",   // (uwaga, PLOT 144-146): bez prętów bocznych SIDE nieprzeciętych linią cięcia (jedyna zmiana); //: UB 03 licuje się ze strzemieniem; kolejność poziomów siatki przy belce (górny pierwszy — te same kropki)
        };

        [TestCaseSource(nameof(Cases))]
        public void Plan_FullResult_MatchesGolden(string name, Func<SectionPlanner.Input> make)
        {
            var result = SectionPlanner.Plan(make());
            var dump = DumpNormalized(result);
            string sha = Sha(dump);
            string gold = Golden.TryGetValue(name, out var g) ? g : "(brak)";
            if (sha != gold)
            {
                string path = Path.Combine(Path.GetTempPath(), "section-golden-" + name + ".txt");
                File.WriteAllText(path, dump);
                File.WriteAllText(Path.ChangeExtension(path, ".raw.txt"), Dump(result));   // pełny zrzut „R” do diagnozy
                Assert.Fail($"{name}: SHA {sha} ≠ złoty {gold} (zrzut: {path})");
            }
        }

        // ── normalizacja N (rozwiązanie B): format, odporność na 1 ulp, czułość na zmiany, granice zaokrąglenia ──

        [TestCase(710.0, "710")]
        [TestCase(-710.0, "-710")]
        [TestCase(0.0, "0")]
        [TestCase(-0.0, "0")]
        [TestCase(4e-7, "0")]
        [TestCase(-4e-7, "0")]
        [TestCase(2.4e-6, "0.000002")]
        [TestCase(-312.67766952966366, "-312.67767")]   // Linux (= złoty, beam BAR 10 07)
        [TestCase(-312.6776695296637, "-312.67767")]    // Windows (+1 ulp) — ten sam zapis
        [TestCase(0.1, "0.1")]
        [TestCase(123456.789, "123456.789")]
        [TestCase(double.NaN, "NaN")]
        [TestCase(double.PositiveInfinity, "Infinity")]
        [TestCase(double.NegativeInfinity, "-Infinity")]
        public void N_FormatsDeterministically(double v, string expected)
            => Assert.That(N(v), Is.EqualTo(expected));

        [Test]
        public void N_NegativeZeroBits_IsZero()
            => Assert.That(N(BitConverter.Int64BitsToDouble(long.MinValue)), Is.EqualTo("0"));

        /// <summary>Wszystkie liczby double zrzutu (te same pola co <see cref="DumpNormalized"/>) dla każdego przypadku złotego.</summary>
        private static List<(string Name, double V)> GoldenValues()
        {
            var all = new List<(string Name, double V)>();
            foreach (var tc in Cases())
            {
                string name = (string)tc.Arguments[0]!;
                var make = (Func<SectionPlanner.Input>)tc.Arguments[1]!;
                Dump(SectionPlanner.Plan(make()), v => { all.Add((name, v)); return R(v); });
            }
            return all;
        }

        /// <summary>Różnica platformowa rzędu 1 ulp (Math.Sin / Math.Atan2: Windows vs Linux) nie zmienia zapisu żadnej wartości złotej.</summary>
        [Test]
        public void Normalized_IgnoresOneUlp_AllGoldenValues()
        {
            int n = 0;
            foreach (var (name, v) in GoldenValues())
            {
                if (double.IsNaN(v) || double.IsInfinity(v)) continue;
                string s = N(v); n++;
                Assert.That(N(Math.BitIncrement(v)), Is.EqualTo(s), $"{name}: {R(v)} +1 ulp");
                Assert.That(N(Math.BitDecrement(v)), Is.EqualTo(s), $"{name}: {R(v)} -1 ulp");
            }
            Assert.That(n, Is.GreaterThan(0));
        }

        /// <summary>Zapas od granicy zaokrąglenia siatki 1e-6 mm: ok. 2·10^4 × obserwowana różnica platformowa (5,7e-14 mm).</summary>
        private const double RoundingMargin = 1e-9;

        /// <summary>
        /// Strażnik granic zaokrąglenia: żadna wartość złota nie leży bliżej niż <see cref="RoundingMargin"/> od połowy kroku
        /// siatki 1e-6 mm. Gdyby przyszła geometria tam trafiła, test wskaże to wprost (zamiast losowej niezgodności SHA między
        /// platformami) — wtedy zmienić dane wejściowe przypadku albo przejrzeć normalizację.
        /// </summary>
        [Test]
        public void Normalized_GoldenValues_FarFromRoundingBoundary()
        {
            foreach (var (name, v) in GoldenValues())
            {
                if (double.IsNaN(v) || double.IsInfinity(v)) continue;
                Assert.That(N(v + RoundingMargin), Is.EqualTo(N(v - RoundingMargin)),
                    $"{name}: {R(v)} leży bliżej niż {RoundingMargin} mm od granicy zaokrąglenia 1e-6 mm");
            }
        }

        /// <summary>Zmiana dowolnej pojedynczej wartości zrzutu o ±0,01 / ±0,1 mm zmienia skrót każdego przypadku złotego.</summary>
        [TestCase(0.01)]
        [TestCase(0.1)]
        public void Normalized_DetectsGeometryChange_EveryValue(double delta)
        {
            foreach (var tc in Cases())
            {
                string name = (string)tc.Arguments[0]!;
                var r = SectionPlanner.Plan(((Func<SectionPlanner.Input>)tc.Arguments[1]!)());
                string baseSha = Sha(DumpNormalized(r));
                var vals = new List<double>();
                Dump(r, v => { vals.Add(v); return R(v); });
                for (int k = 0; k < vals.Count; k++)
                {
                    if (double.IsNaN(vals[k]) || double.IsInfinity(vals[k])) continue;
                    foreach (double d in new[] { delta, -delta })
                    {
                        int i = 0;
                        string changed = Dump(r, v => N(i++ == k ? v + d : v));
                        Assert.That(Sha(changed), Is.Not.EqualTo(baseSha), $"{name}: wartość #{k} ({R(vals[k])}) {d:+0.##;-0.##} mm");
                    }
                }
            }
        }

        /// <summary>Zmiana wejścia przypadku beam (pal pod belką, krawędź nibu) o 0,01 / 0,1 mm zmienia skrót; o 1e-12 mm — nie.</summary>
        [TestCase(0.01, true)]
        [TestCase(0.1, true)]
        [TestCase(1e-12, false)]
        public void Normalized_BeamInputShift(double delta, bool changes)
        {
            var a = CaseBeam(); var p = a.Piles[0]; a.Piles[0] = (p.Item1 + delta, p.Item2, p.Item3);
            var b = CaseBeam(); b.Nibs[0].InnerCoord += delta;
            Assert.That(Sha(DumpNormalized(SectionPlanner.Plan(a))) != Golden["beam"], Is.EqualTo(changes), "pal");
            Assert.That(Sha(DumpNormalized(SectionPlanner.Plan(b))) != Golden["beam"], Is.EqualTo(changes), "nib");
        }

        [Test]
        public void Used_MarkedAtAddTime_ByReference_InInputOrder()
        {
            var inp = CaseNib();
            var r = SectionPlanner.Plan(inp);
            // wszystkie rozkłady przypadku nib dają kropki albo pręt w widoku
            Assert.That(r.UsedDists.Select(d => d.PosNr), Is.EqualTo(new[] { "05", "06", "104", "105", "01", "03", "101" }));
            Assert.That(r.UsedDists.All(u => inp.Dists.Any(d => ReferenceEquals(d, u))), Is.True);
            Assert.That(r.LevelDiameters, Is.EqualTo((10.0, 12.0)));
            Assert.That(r.UsedPiles, Is.Empty);

            var p = SectionPlanner.Plan(CasePiles());
            Assert.That(p.UsedPiles, Is.EqualTo(new[] { (1500.0, 2500.0, 300.0), (3500.0, 2600.0, 300.0) }));
        }

        [Test]
        public void Used_FarDistribution_NotUsed()
        {
            var inp = CaseHole();
            // rozkład daleko od linii cięcia (pręty X przy y = 4500, równoległe do cięcia y = 2500)
            inp.Dists.Add(D("H10-30-200 B1", "B1", true, 40, 4500, 7920, 200, 2, 200, 10));
            var r = SectionPlanner.Plan(inp);
            Assert.That(r.UsedDists.Any(d => d.PosNr == "30"), Is.False);
            Assert.That(r.UsedDists.Count, Is.EqualTo(4));
        }

        [TestCaseSource(nameof(Cases))]
        public void Plan_IsNotTrivial(string name, Func<SectionPlanner.Input> make)
        {
            var r = SectionPlanner.Plan(make());
            Assert.That(r.Segments, Is.Not.Empty, name);
            Assert.That(r.Dots.Count + r.Bars.Count, Is.GreaterThan(0), name);
        }
    }
}
