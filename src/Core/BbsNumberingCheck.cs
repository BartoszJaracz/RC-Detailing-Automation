using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Teigha.DatabaseServices;

namespace BricsCadRc.Core
{
    /// <summary>
    /// RC_BBS — kontrola numeracji prętów przed wydaniem BBS (wymaganie, obok bezpiecznika przekrojów):
    ///   • luki w numeracji: seria dolna od 04 (01–03 zarezerwowane dla UB), seria górna od 101, seria osobna od 501 — brakujące numery
    ///     między pierwszym a najwyższym użytym (numery z PositionCounter.GetUsedPositionNumbers: pręty i rozkłady);
    ///   • te same pręty (średnica, kształt, wymiary kształtu, długość nadpisana) pod RÓŻNYMI numerami tej samej serii;
    ///   • ten sam numer: różne pręty pod jednym numerem albo kilka identycznych prętów (kopie pręta-szablonu).
    /// Tylko odczyt — lista wierszy komunikatu (pusta = bez uwag).
    /// </summary>
    public static class BbsNumberingCheck
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static List<string> Check(Database db)
        {
            var res = new List<string>();
            try
            {
                // ── luki ──
                var used = PositionCounter.GetUsedPositionNumbers(db);
                foreach (var (name, lo, hi) in new[]
                         {
                             ("dolna", PositionCounter.FirstAutoNumber, PositionCounter.TopSeriesStart - 1),
                             ("górna", PositionCounter.TopSeriesStart, PositionCounter.SeparateSeriesStart - 1),
                             ("osobna", PositionCounter.SeparateSeriesStart + 1, int.MaxValue)
                         })
                {
                    var inSeries = used.Where(n => n >= lo && n <= hi).ToList();
                    if (inSeries.Count == 0) continue;
                    int max = inSeries.Max();
                    var missing = Enumerable.Range(lo, max - lo + 1).Where(n => !used.Contains(n)).ToList();
                    if (missing.Count > 0)
                        res.Add($"Luka w numeracji (seria {name}): brak {Ranges(missing)} (użyte do {Nr(max)}).");
                }

                // ── pręty-szablony ──
                var bars = new List<(int nr, string sig, string text)>();
                using (var tr = db.TransactionManager.StartOpenCloseTransaction())
                {
                    foreach (var be in BarEntity.ScanModelSpace(tr, db, BarKinds.Polyline | BarKinds.Block))
                    {
                        var b = be.Read();
                        if (b == null) continue;
                        int nr = SingleBarEngine.ExtractPosNr(b.Mark);
                        if (nr <= 0) continue;
                        string code = string.IsNullOrWhiteSpace(b.ShapeCode) ? "00" : b.ShapeCode.Trim();
                        var p = ShapeCodeLibrary.ShapeParamValues(code, b.ParamValues);
                        var names = ShapeCodeLibrary.Get(code)?.Parameters ?? new[] { "A", "B", "C", "D", "E" };
                        string dims = string.Join(" ", names.Select((nm, i) => i < p.Length && p[i] != 0
                                                       ? nm + Math.Round(p[i]).ToString("0", Inv) : null).Where(x => x != null));
                        string sig = $"H{b.Diameter}|{code}|{string.Join(",", p.Select(v => Math.Round(v).ToString("0", Inv)))}" +
                                     (b.LengthOverridden ? "|L" + Math.Round(b.TotalLength).ToString("0", Inv) : "");
                        bars.Add((nr, sig, $"H{b.Diameter} kształt {code} {dims}".Trim()));
                    }
                    tr.Commit();
                }

                // te same pręty pod różnymi numerami (w obrębie serii — dół i góra mają osobne serie z założenia)
                foreach (var g in bars.GroupBy(x => (x.sig, Series(x.nr))))
                {
                    var nrs = g.Select(x => x.nr).Distinct().OrderBy(n => n).ToList();
                    if (nrs.Count > 1)
                        res.Add($"Te same pręty pod różnymi numerami: {g.First().text} — pozycje {string.Join(", ", nrs.Select(Nr))}.");
                }
                // ten sam numer: różne pręty / kilka identycznych
                foreach (var g in bars.GroupBy(x => x.nr).OrderBy(g => g.Key))
                {
                    var sigs = g.GroupBy(x => x.sig).ToList();
                    if (sigs.Count > 1)
                        res.Add($"Pozycja {Nr(g.Key)}: różne pręty pod jednym numerem — " +
                                string.Join("; ", sigs.Select(s => s.First().text + (s.Count() > 1 ? $" ({s.Count()} szt.)" : ""))) + ".");
                    else if (g.Count() > 1)
                        res.Add($"Pozycja {Nr(g.Key)}: {g.Count()} identyczne pręty (kopie pręta) — {g.First().text}.");
                }
            }
            catch (Exception ex) { Log.Error("BbsNumberingCheck.Check", ex); }
            return res;
        }

        private static int Series(int nr)
            => nr >= PositionCounter.SeparateSeriesStart ? 2 : nr >= PositionCounter.TopSeriesStart ? 1 : 0;

        private static string Nr(int n) => n.ToString("00", Inv);

        /// <summary>[5, 6, 7, 9] → „05–07, 09”.</summary>
        private static string Ranges(List<int> ns)
        {
            var parts = new List<string>();
            for (int i = 0; i < ns.Count; i++)
            {
                int a = ns[i], b = a;
                while (i + 1 < ns.Count && ns[i + 1] == b + 1) b = ns[++i];
                parts.Add(a == b ? Nr(a) : b == a + 1 ? $"{Nr(a)}, {Nr(b)}" : $"{Nr(a)}–{Nr(b)}");
            }
            return string.Join(", ", parts);
        }
    }
}
