using BricsCadRc.Core;

namespace BricsCadRc.Tests
{
    /// <summary>
    /// POPRAWKI_WZORCA 24 — etykieta płyty z nagłówka mapy = pierwszy AKAPIT MText (nie MText.Text, który BricsCAD
    /// skleja: „PLOT 326h = 225 mm | …”). Teksty nagłówków — dosłownie z DXF map DemoProject
    /// (AB400CD004_ALL_260910_punching_reinf_maps.dxf, AB400CD004_part_2_…, AB400CD004_PLOT_266_…, warstwa PH-SLAB-HEADER).
    /// </summary>
    [TestFixture]
    public class MTextParagraphsTests
    {
        private const string H326 =
            @"\C1;\LPLOT 326\l\P\C7;h = 225 mm  |  fck = 28 MPa  |  KWL 2155  |  n_piles = 10\P\C8;TOP: base H12@200 (540) + add H12@200 (540) = 1080 mm2/m\P\C8;BOT: base H10@200 (385) + add H10@200 (385) = 770 mm2/m\P\C8;AB400CD004_ALL_260910";
        private const string HGarage =
            @"\C1;\LGARAGE 326-327\l\P\C7;h = 300 mm  |  fck = 28 MPa  |  KWL 2155  |  n_piles = 5\P\C8;TOP: base H12@200 (540) + add H12@200 (540) = 1080 mm2/m\P\C8;BOT: base H10@200 (385) + add H10@200 (385) = 770 mm2/m\P\C8;AB400CD004_ALL_260910";
        private const string H129 =
            @"\C1;\LPLOT 129-130, 320-321\l\P\C7;h = 225 mm  |  fck = 28 MPa  |  KWL 2155  |  n_piles = 13\P\C8;TOP: base H12@200 (540) + add H12@200 (540) = 1080 mm2/m\P\C8;BOT: base H10@200 (385) + add H10@200 (385) = 770 mm2/m\P\C8;AB400CD004_part_2";
        private const string H323 =
            @"\C1;\LPLOT 323\l\P\C7;h = 225 mm  |  fck = 28 MPa  |  KWL 2155  |  n_piles = 11\P\C8;TOP: base H12@200 (540) + add H12@200 (540) = 1080 mm2/m\P\C8;BOT: base H10@200 (385) + add H10@200 (385) = 770 mm2/m\P\C8;AB400CD004_part_2";
        private const string H119 =
            @"\C1;\LPLOT 119\l\P\C7;h = 450 mm  |  fck = 28 MPa  |  KWL 2155  |  n_piles = 9\P\C8;TOP: base H12@200 (540) + add H12@200 (540) = 1080 mm2/m\P\C8;BOT: base H10@200 (385) + add H10@200 (385) = 770 mm2/m\P\C8;AB400CD004_ALL_260910";
        private const string H266 =
            @"\C1;\LPLOT 266\l\P\C7;h = 225 mm  |  fck = 28 MPa  |  KWL 2155  |  n_piles = 11\P\C8;TOP: base H12@200 (540) + add H12@200 (540) = 1080 mm2/m\P\C8;BOT: base H10@200 (385) + add H10@200 (385) = 770 mm2/m\P\C8;AB400CD004_PLOT_266";

        [TestCase(H326, "PLOT 326")]
        [TestCase(HGarage, "GARAGE 326-327")]
        [TestCase(H129, "PLOT 129-130, 320-321")]
        [TestCase(H323, "PLOT 323")]
        [TestCase(H119, "PLOT 119")]
        [TestCase(H266, "PLOT 266")]
        public void FirstParagraph_RealMapHeaders(string contents, string expected)
            => Assert.That(MTextParagraphs.First(contents), Is.EqualTo(expected));

        [Test]
        public void Split_RealHeader_AllParagraphs()
        {
            var p = MTextParagraphs.Split(H326);
            Assert.That(p, Has.Count.EqualTo(5));
            Assert.That(p[0], Is.EqualTo("PLOT 326"));
            Assert.That(p[1], Is.EqualTo("h = 225 mm  |  fck = 28 MPa  |  KWL 2155  |  n_piles = 10"));
            Assert.That(p[4], Is.EqualTo("AB400CD004_ALL_260910"));
        }

        // Etykiety jednolinijkowe — wynik jak dawny FirstLine(MText.Text) (bez zmian)
        [TestCase("PLOT 6-8", "PLOT 6-8")]
        [TestCase("  PLOT 258  ", "PLOT 258")]
        [TestCase("PLOT  9-10", "PLOT  9-10")]          // białe znaki wewnątrz bez zmian (jak dawniej)
        [TestCase(@"{\fArial|b0|i0|c238|p34;PLOT 211 & 322}", "PLOT 211 & 322")]
        [TestCase(@"\pxqc;PLOT 1", "PLOT 1")]
        [TestCase("", "")]
        [TestCase(null, "")]
        public void FirstParagraph_SingleLine_Unchanged(string? contents, string expected)
            => Assert.That(MTextParagraphs.First(contents!), Is.EqualTo(expected));

        // Opis płyty w rysunku RC (GaImportEngine.RebuildLabel: „PLOT …\PSSL=…\P225mm THK SLAB”) — GuessPlot
        [Test]
        public void FirstParagraph_RcSlabDescription()
            => Assert.That(MTextParagraphs.First(@"PLOT 326\PSSL=21.925\P225mm THK SLAB"), Is.EqualTo("PLOT 326"));

        [Test]
        public void Split_EscapesAndNewlines()
        {
            Assert.That(MTextParagraphs.Split("A\r\nB\nC"), Is.EqualTo(new[] { "A", "B", "C" }));
            Assert.That(MTextParagraphs.First(@"PLOT 5\\6"), Is.EqualTo(@"PLOT 5\6"));
            Assert.That(MTextParagraphs.First(@"PLOT\~7"), Is.EqualTo("PLOT 7"));
            Assert.That(MTextParagraphs.First(@"\P\PPLOT 8\Pdalej"), Is.EqualTo("PLOT 8"));
        }

        // Etykiety zapisane dawnym odczytem (XData RC_MAP [1], AnalysisStore) — dopasowanie i naprawa
        [TestCase("PLOT 326h = 225 mm  |  fck = 28 MPa  |  KWL 2155  |  n_piles = 10TOP: base H12@200 (540)", "PLOT 326")]
        [TestCase("GARAGE 326-327h = 300 mm  |  fck = 28 MPa", "GARAGE 326-327")]
        [TestCase("PLOT 129-130, 320-321h = 225 mm  |  fck = 28 MPa", "PLOT 129-130, 320-321")]
        [TestCase("PLOT 326", "PLOT 326")]
        [TestCase("PLOT 6-8", "PLOT 6-8")]
        [TestCase("SSL=21.925", "SSL=21.925")]
        [TestCase("", "")]
        public void RepairGluedMapLabel(string label, string expected)
            => Assert.That(MTextParagraphs.RepairGluedMapLabel(label), Is.EqualTo(expected));

        [Test]
        public void SameMapLabel()
        {
            Assert.That(MTextParagraphs.SameMapLabel("PLOT 326h = 225 mm  |  fck = 28 MPa", "PLOT 326"), Is.True);
            Assert.That(MTextParagraphs.SameMapLabel("plot  326", "PLOT 326"), Is.True);
            Assert.That(MTextParagraphs.SameMapLabel("PLOT 32", "PLOT 326"), Is.False);
            Assert.That(MTextParagraphs.SameMapLabel("PLOT 326h = 225 mm", "PLOT 327"), Is.False);
        }
    }
}
