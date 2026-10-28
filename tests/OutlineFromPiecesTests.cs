using System.Collections.Generic;
using System.Linq;
using BricsCadRc.Core;
using NUnit.Framework;

namespace BricsCadRc.Tests
{
    /// <summary>GA PLOT 167 (uwaga): obrys SD-PILED-RAFT z linii i otwartych polilinii, styki „na T”.</summary>
    public class OutlineFromPiecesTests
    {
        private static readonly ((double X, double Y) A, (double X, double Y) B)[] Plot167 =
        {
            ((153916, -359511), (148168, -359511)), ((148168, -352771), (153916, -352771)), ((147638, -353724), (145978, -353724)),
            ((145978, -358911), (147893, -358911)), ((153916, -353371), (148168, -353371)), ((148168, -358911), (153981, -358911)),
            ((145591, -359511), (143041, -359511)), ((143041, -358911), (145591, -358911)), ((143041, -359511), (142441, -359511)),
            ((142441, -359511), (142441, -352771)), ((148168, -359511), (145591, -359511)), ((145591, -358911), (145978, -358911)),
            ((145978, -358911), (145978, -353371)), ((145978, -353371), (143041, -353371)), ((143041, -353371), (143041, -358911)),
            ((148168, -353371), (148168, -353724)), ((148168, -353724), (147638, -353724)), ((147638, -353724), (147638, -356758)),
            ((147638, -356758), (147893, -356758)), ((147893, -356758), (147893, -358911)), ((147893, -358911), (148168, -358911)),
            ((153916, -353521), (153981, -353521)), ((153981, -353521), (153981, -358911)), ((142441, -352771), (148168, -352771)),
            ((153916, -353521), (153916, -353371)), ((153791, -359511), (154581, -359511)), ((154581, -359511), (154581, -352771)),
            ((154581, -352771), (153791, -352771)),
        };

        [Test]
        public void Plot167_OuterRectangle()
        {
            var r = OutlineFromPieces.OuterBoundary(Plot167, (148839, -356077));
            Assert.That(r, Is.Not.Null);
            Assert.That(r.Count, Is.EqualTo(4), string.Join(" ", r.Select(p => $"({p.X:F0},{p.Y:F0})")));
            Assert.That(r.Min(p => p.X), Is.EqualTo(142441).Within(1e-6));
            Assert.That(r.Max(p => p.X), Is.EqualTo(154581).Within(1e-6));
            Assert.That(r.Min(p => p.Y), Is.EqualTo(-359511).Within(1e-6));
            Assert.That(r.Max(p => p.Y), Is.EqualTo(-352771).Within(1e-6));
            Assert.That(OutlineFromPieces.OuterBoundary(Plot167, (100000, -356077)), Is.Null, "punkt poza obrysem");
        }

        [Test]
        public void OpenChain_NotClosed_Null()
            => Assert.That(OutlineFromPieces.OuterBoundary(new[] { ((0.0, 0.0), (10.0, 0.0)), ((10.0, 0.0), (10.0, 10.0)), ((10.0, 10.0), (0.0, 10.0)) },
                                                           (5, 5)), Is.Null);
    }
}
