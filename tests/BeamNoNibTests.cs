using BricsCadRc.Core;

namespace BricsCadRc.Tests
{
    /// <summary>
 /// Decyzja użytkownika „w generatorze dodaj opcję dodania lub nie dodawania nibu, bo czasem może nie być”: okno RC_BEAM
    /// „Nib przy belce”, rekord RC_BEAM [38] (0 = wg geometrii, 1 = bez nibu). Belka bez nibu: lico zewnętrzne = krawędź płyty
    /// (BeamGeometry.ZoneWithoutNib), bez prętów w nibie, krawędź nibu przy belce usunięta (NibSpansOutside, AddClipped linii uskoku),
 /// pręty siatki prostopadłe do 40 od krawędzi płyty. Decyzja (później): szerokość z GA zostaje — belka przesunięta do krawędzi. Dane: DemoProject 328 (GA P02) — płyta 326764..337395 × 49647..60150, nib 160,
    /// strefa 326924..327524 × 49807..59990 (nib zawinięty na obu końcach).
    /// </summary>
    public class BeamNoNibTests
    {
        private static List<(double X, double Y)> Box(double x0, double y0, double x1, double y1)
            => new List<(double X, double Y)> { (x0, y0), (x1, y0), (x1, y1), (x0, y1) };

        private static readonly List<(double X, double Y)> Outl328 = Box(326764, 49647, 337395, 60150);
        private static readonly List<(double X, double Y)> Zone328 = Box(326924, 49807, 327524, 59990);

        [Test]
        public void Params_NoNib_DefaultFalse_CloneAndMigrationKeepGeometryDefault()
        {
            var p = new BeamParams();
            Assert.That(p.NoNib, Is.False, "domyślnie wg geometrii (brak pola [38] = jak dotąd)");
            p.NoNib = true;
            Assert.That(p.Clone().NoNib, Is.True);
            Assert.That(BeamParams.MigrateV1(new BeamParams { NoNib = true }).NoNib, Is.False, "v1 nie ma pola [38]");
        }

        [Test]
        public void Geometry_328_HasNib160()
        {
            var an = BeamGeometry.Analyze(Zone328, Outl328);
            Assert.That(BeamGeometry.HasNibGeometry(an, Outl328), Is.True);
            var s = an.Segments.Single(x => !x.Manual);
            Assert.That(s.OuterSide, Is.EqualTo(-1));
            Assert.That(BeamGeometry.OuterGap(s, Outl328), Is.EqualTo(160).Within(1e-9));
        }

        [Test]
        public void ZoneWithoutNib_328_ShiftedToSlabEdge_Width600()
        {
            var msgs = new List<string>();
            var z = BeamGeometry.ZoneWithoutNib(Zone328, Outl328, msgs);
            Assert.That(msgs, Is.Empty);
            Assert.That(z.Count, Is.EqualTo(4));
            Assert.That(z.Min(p => p.X), Is.EqualTo(326764).Within(1e-9), "lico zewnętrzne = krawędź płyty");
            Assert.That(z.Max(p => p.X), Is.EqualTo(327364).Within(1e-9), "lico wewnętrzne przesunięte o nib — szerokość z GA (600)");
            Assert.That(z.Min(p => p.Y), Is.EqualTo(49647).Within(1e-9), "koniec przy nibie zawiniętym — do krawędzi");
            Assert.That(z.Max(p => p.Y), Is.EqualTo(60150).Within(1e-9));
            var an = BeamGeometry.Analyze(z, Outl328);
            var s = an.Segments.Single(x => !x.Manual);
            Assert.That(s.Width, Is.EqualTo(600).Within(1e-9), "decyzja: szerokość 600 z GA (przesunięcie, nie poszerzenie)");
            Assert.That(s.Length, Is.EqualTo(60150 - 49647).Within(1e-9), "końce przy nibie zawiniętym — do krawędzi płyty");
            Assert.That(s.OuterSide, Is.EqualTo(-1), "nadal belka krawędziowa (Auto 4-cięte, UB 01/02 brak)");
            Assert.That(s.LoKind, Is.EqualTo(BeamGeometry.EndKind.Edge));
            Assert.That(s.HiKind, Is.EqualTo(BeamGeometry.EndKind.Edge));
            Assert.That(BeamGeometry.HasNibGeometry(an, Outl328), Is.False);
            Assert.That(BeamGeometry.NibBarLine(s, Outl328, 10), Is.Null, "bez prętów w nibie (2+1 T+B IN NIB)");
        }

        [Test]
        public void ZoneWithoutNib_328_PerpendicularMeshTo40FromSlabEdge()
        {
            var an = BeamGeometry.Analyze(BeamGeometry.ZoneWithoutNib(Zone328, Outl328), Outl328);
            foreach (bool top in new[] { false, true })
            {
                var rs = BeamGeometry.MeshCutRects(an, top, barsAlongX: true, d: top ? 12 : 10);
                Assert.That(rs.Count, Is.EqualTo(1));
                Assert.That(rs[0].X1 + 40, Is.EqualTo(326764 + BeamGeometry.MeshBeamSideCover).Within(1e-9),
                            "koniec pręta prostopadłego 40 od lica zewnętrznego = 40 od krawędzi płyty");
            }
            // B2 równoległe: stop przed NOWYM licem wewnętrznym (327364) + 40 + d
            var b2 = BeamGeometry.MeshCutRects(an, top: false, barsAlongX: false, d: 10);
            Assert.That(b2[0].X1, Is.EqualTo(327374).Within(1e-9));
            // krawędź płyty przy belce bez UB 01/02 (pionowe U-bary BU zamykają koniec, strzemiona — krawędź)
            var ub = RectilinearPolygons.Union(BeamGeometry.UbExclusionRects(an));
            Assert.That(RectilinearPolygons.UncoveredParts(49647, 60150, ub, false, 326764), Is.Empty);
        }

        [Test]
        public void ZoneWithoutNib_FlushZoneAndInternalBeam_Unchanged()
        {
            var flush = Box(326764, 52000, 327364, 56000);   // RC-BEAM-MANUAL przy krawędzi (bez linii uskoku)
            Assert.That(BeamGeometry.HasNibGeometry(BeamGeometry.Analyze(flush, Outl328), Outl328), Is.False);
            Assert.That(BeamGeometry.ZoneWithoutNib(flush, Outl328), Is.EqualTo(flush));
            var inner = Box(330000, 52000, 330600, 56000);   // belka w płycie
            Assert.That(BeamGeometry.HasNibGeometry(BeamGeometry.Analyze(inner, Outl328), Outl328), Is.False);
            Assert.That(BeamGeometry.ZoneWithoutNib(inner, Outl328), Is.EqualTo(inner));
        }

        [Test]
        public void ZoneWithoutNib_LShape_BothLegsShifted()
        {
            // L: noga pionowa przy lewej krawędzi (nib 150) i pozioma przy dolnej (nib 150), wewnętrzne końce
            var outl = Box(0, 0, 10000, 8000);
            var zone = new List<(double X, double Y)> { (150, 150), (5000, 150), (5000, 750), (750, 750), (750, 5000), (150, 5000) };
            var z = BeamGeometry.ZoneWithoutNib(zone, outl);
            Assert.That(z, Is.EquivalentTo(new List<(double X, double Y)> { (0, 0), (5000, 0), (5000, 600), (600, 600), (600, 5000), (0, 5000) }));
            var an = BeamGeometry.Analyze(z, outl);
            Assert.That(an.Segments.Where(s => !s.Manual).All(s => Math.Abs(s.Width - 600) < 1e-9), Is.True, "obie nogi przesunięte, szerokość 600");
            Assert.That(BeamGeometry.HasNibGeometry(an, outl), Is.False);
        }

        [Test]
        public void ZoneWithoutNib_VaryingGap_UnchangedWithMessage()
        {
            // krawędź płyty z uskokiem wzdłuż lica (nib 160 / 220) — lico bez zmian + komunikat
            var outl = new List<(double X, double Y)> { (0, 0), (10000, 0), (10000, 8000), (-60, 8000), (-60, 4000), (0, 4000) };
            var zone = Box(160, 160, 760, 7000);
            var msgs = new List<string>();
            var z = BeamGeometry.ZoneWithoutNib(zone, outl, msgs);
            Assert.That(z.Min(p => p.X), Is.EqualTo(160).Within(1e-9));
            Assert.That(msgs.Count, Is.EqualTo(1));
            Assert.That(msgs[0], Does.Contain("różnej odległości"));
        }

        [Test]
        public void NibSpans_328_BeamEdgeRemoved_WrappedNibSplit()
        {
            // nib 528: lewa krawędź (całość), dolna i górna (zawinięty, do narożników)
            var spans = new List<BeamGeometry.NibSpan>
            {
                new BeamGeometry.NibSpan { Vertical = true, OuterCoord = 326764, InnerCoord = 326924, Lo = 49647, Hi = 60150, InwardSign = 1 },
                new BeamGeometry.NibSpan { Vertical = false, OuterCoord = 49647, InnerCoord = 49807, Lo = 326764, Hi = 337395, InwardSign = 1 },
                new BeamGeometry.NibSpan { Vertical = false, OuterCoord = 60150, InnerCoord = 59990, Lo = 326764, Hi = 337395, InwardSign = -1 },
            };
            var an = BeamGeometry.Analyze(Zone328, Outl328);
            var loops = RectilinearPolygons.Union(BeamGeometry.UbExclusionRects(an));
            var r = BeamGeometry.NibSpansOutside(spans, loops);
            Assert.That(r.Any(e => e.Vertical), Is.False, "nib przed belką usunięty");
            Assert.That(r.Count, Is.EqualTo(2));
            Assert.That(r.All(e => Math.Abs(e.Lo - 327524) < 1e-9 && Math.Abs(e.Hi - 337395) < 1e-9), Is.True,
                        "nib zawinięty — tylko za licem wewnętrznym belki");
            Assert.That(BeamGeometry.NibSpansOutside(spans, null).Count, Is.EqualTo(3), "bez belek bez nibu — bez zmian");
        }

        [Test]
        public void AddClipped_328_StepLineToSlabEdgeAtBeam()
        {
            // linia uskoku: nib 160 na lewej, dolnej i górnej krawędzi, prawa bez nibu
            var inner = new List<(double X, double Y)> { (326924, 49807), (337395, 49807), (337395, 59990), (326924, 59990) };
            var an = BeamGeometry.Analyze(Zone328, Outl328);
            var loops = RectilinearPolygons.AddClipped(inner, BeamGeometry.UbExclusionRects(an), Outl328);
            Assert.That(loops.Count, Is.EqualTo(1));
            var l = loops[0];
            Assert.That(RectilinearPolygons.SignedArea(l), Is.GreaterThan(0));
            Assert.That(l.Min(p => p.X), Is.EqualTo(326764).Within(1e-9), "przy belce linia uskoku = krawędź płyty");
            Assert.That(RectilinearPolygons.Inside(loops, 326800, 55000), Is.True, "pas nibu przed belką — w wielokącie siatki górnej");
            Assert.That(RectilinearPolygons.Inside(loops, 326800, 49700), Is.True, "nib zawinięty przy końcu belki — w wielokącie");
            Assert.That(RectilinearPolygons.Inside(loops, 330000, 49700), Is.False, "nib dolny za belką — zostaje");
            Assert.That(RectilinearPolygons.Inside(loops, 330000, 60100), Is.False, "nib górny za belką — zostaje");
            // bez prostokątów — wielokąt bez zmian
            var same = RectilinearPolygons.AddClipped(inner, new RectilinearPolygons.Rect[0], Outl328);
            Assert.That(RectilinearPolygons.Area(same), Is.EqualTo(RectilinearPolygons.SignedArea(inner)).Within(1e-6));
        }
    }
}
