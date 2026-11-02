using System;
using System.Collections.Generic;
using System.Linq;
using Teigha.DatabaseServices;
using Teigha.Geometry;

namespace BricsCadRc.Core
{
    /// <summary>
    /// Wymiary pręta-szablonu (wymaganie, APARTMENT-TEST screen 4 / 5: „jak tworzysz pręty, to możesz je też jakoś
    /// wymiarować”): wymiary zewnętrzne obrysu pręta — poziomy pod prętem (bok z dłuższym odcinkiem obrysu przy krawędzi obwiedni,
    /// pręt prosty: cała długość) i pionowy z boku (bok z dłuższym odcinkiem, np. ramię U-bara). Pręt prosty pionowy — tylko pionowy.
    /// Styl wymiarów jak ADD (DEMOCLIENT-1-50 RC: linie żółte, tekst biały), warstwa SD-DIMS (brak — SD-PILED-RAFT, brak — bieżąca).
    /// XData RC_BAR_DIM: [1] uchwyt pręta (1005). Ponowne wymiarowanie (RC_BAR_DIMS, RC_EDIT_BAR) usuwa wymiary tego pręta w jego pobliżu.
    /// Wymiary nie są skojarzone — po przesunięciu / zmianie pręta: RC_BAR_DIMS.
    /// </summary>
    public static class BarDimEngine
    {
        public const string XApp = "RC_BAR_DIM";
        public const string DimStyle = "DEMOCLIENT-1-50 RC";
        public static readonly string[] Layers = { "SD-DIMS", "SD-PILED-RAFT" };
        /// <summary>Linia wymiarowa od obwiedni obrysu pręta [mm].</summary>
        public const double DimOff = 250.0;
        /// <summary>Wymiary pręta szukane w obwiedni pręta powiększonej o tyle [mm] (kopie pręta z wymiarami dalej — nie ruszane).</summary>
        public const double Near = 1500.0;

        /// <summary>Wymiaruje pręt; błąd — tylko log (tworzenie pręta nie jest przerywane).</summary>
        public static int TryDimension(Database db, ObjectId barId)
        {
            try { return Dimension(db, barId); }
            catch (System.Exception ex) { Log.Error("BarDimEngine.TryDimension", ex); return 0; }
        }

        /// <summary>RC_EDIT_BAR: odświeża wymiary, tylko gdy pręt je ma.</summary>
        public static void Refresh(Database db, ObjectId barId)
        {
            try { if (HasDims(db, barId)) Dimension(db, barId); }
            catch (System.Exception ex) { Log.Error("BarDimEngine.Refresh", ex); }
        }

        public static bool HasDims(Database db, ObjectId barId) => FindDims(db, barId).Count > 0;

        /// <summary>Usuwa wymiary pręta i rysuje nowe. Zwraca liczbę wymiarów.</summary>
        public static int Dimension(Database db, ObjectId barId)
        {
            if (db == null || barId.IsNull || barId.IsErased) return 0;
            List<(Point3d p1, Point3d p2, Point3d line, double rot)> dims;
            string h = barId.Handle.Value.ToString("X8");
            using (var tr = db.TransactionManager.StartOpenCloseTransaction())
            {
                var be = BarEntity.Open(tr, barId, OpenMode.ForRead, BarKinds.Polyline | BarKinds.Block);
                var bar = be?.Read();
                var outl = be?.OutlineVertices(tr);
                dims = bar != null && outl != null && outl.Count >= 2 ? Plan(outl.Select(p => (p.X, p.Y)).ToList(), Math.Max(6, bar.Diameter)) : null;
                tr.Commit();
            }
            if (dims == null) return 0;
            var old = FindDims(db, barId);
            RcTemplateEngine.EnsureStyles(db);
            int n = 0;
            using (var tr = db.TransactionManager.StartTransaction())
            {
                foreach (var id in old)
                    if (tr.GetObject(id, OpenMode.ForWrite) is Entity e) e.Erase();
                var rat = (RegAppTable)tr.GetObject(db.RegAppTableId, OpenMode.ForRead);
                if (!rat.Has(XApp))
                {
                    rat.UpgradeOpen();
                    var r = new RegAppTableRecord { Name = XApp };
                    rat.Add(r);
                    tr.AddNewlyCreatedDBObject(r, true);
                }
                var dst = (DimStyleTable)tr.GetObject(db.DimStyleTableId, OpenMode.ForRead);
                ObjectId styleId = dst.Has(DimStyle) ? dst[DimStyle] : db.Dimstyle;
                var dsr = (DimStyleTableRecord)tr.GetObject(styleId, OpenMode.ForRead);
                var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                string layer = Layers.FirstOrDefault(l => lt.Has(l));
                var space = (BlockTableRecord)tr.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForWrite);
                foreach (var (p1, p2, line, rot) in dims)
                {
                    var dim = new RotatedDimension(rot, p1, p2, line, "", styleId);
                    dim.SetDatabaseDefaults(db);
                    dim.DimensionStyle = styleId;
 // jak wymiary ADD: SetDatabaseDefaults w BricsCAD wpisuje nadpisania bieżącego stylu — wielkości i kolory jawnie ze stylu
                    dim.Dimscale = dsr.Dimscale; dim.Dimtxt = dsr.Dimtxt; dim.Dimasz = dsr.Dimasz; dim.Dimexo = dsr.Dimexo;
                    dim.Dimexe = dsr.Dimexe; dim.Dimgap = dsr.Dimgap; dim.Dimdli = dsr.Dimdli; dim.Dimcen = dsr.Dimcen;
                    dim.TextStyleId = dsr.Dimtxsty; dim.Dimtad = dsr.Dimtad; dim.Dimdec = dsr.Dimdec;
                    dim.Dimclrd = dsr.Dimclrd; dim.Dimclre = dsr.Dimclre; dim.Dimclrt = dsr.Dimclrt; dim.Dimtdec = dsr.Dimtdec;
                    if (layer != null) dim.Layer = layer;
                    space.AppendEntity(dim);
                    tr.AddNewlyCreatedDBObject(dim, true);
                    dim.XData = new ResultBuffer(new TypedValue((int)DxfCode.ExtendedDataRegAppName, XApp), XLink.Write(h));
                    n++;
                }
                tr.Commit();
            }
            return n;
        }

        /// <summary>
        /// Wymiary z obrysu (czysta geometria): obwiednia; poziomy — gdy szerokość &gt; 3·d, po stronie (dół / góra) z dłuższym odcinkiem obrysu
        /// przy krawędzi obwiedni (remis — dół); pionowy — gdy wysokość &gt; 3·d, po stronie (lewo / prawo) z dłuższym odcinkiem (remis — lewo).
        /// </summary>
        public static List<(Point3d p1, Point3d p2, Point3d line, double rot)> Plan(List<(double X, double Y)> outl, int d)
        {
            var res = new List<(Point3d, Point3d, Point3d, double)>();
            if (outl == null || outl.Count < 2) return res;
            double x0 = outl.Min(p => p.X), x1 = outl.Max(p => p.X), y0 = outl.Min(p => p.Y), y1 = outl.Max(p => p.Y);
            double tol = Math.Max(2.0, 1.5 * d);
            double OnEdge(Func<(double X, double Y), bool> at)
            {
                double s = 0;
                for (int i = 0; i + 1 < outl.Count; i++)
                    if (at(outl[i]) && at(outl[i + 1]))
                        s += Math.Sqrt((outl[i + 1].X - outl[i].X) * (outl[i + 1].X - outl[i].X) + (outl[i + 1].Y - outl[i].Y) * (outl[i + 1].Y - outl[i].Y));
                return s;
            }
            if (x1 - x0 > 3 * d)
            {
                bool bottom = OnEdge(p => p.Y <= y0 + tol) >= OnEdge(p => p.Y >= y1 - tol);
                double yl = bottom ? y0 - DimOff : y1 + DimOff, ye = bottom ? y0 : y1;
                res.Add((new Point3d(x0, ye, 0), new Point3d(x1, ye, 0), new Point3d((x0 + x1) / 2, yl, 0), 0.0));
            }
            if (y1 - y0 > 3 * d)
            {
                bool left = OnEdge(p => p.X <= x0 + tol) >= OnEdge(p => p.X >= x1 - tol);
                double xl = left ? x0 - DimOff : x1 + DimOff, xe = left ? x0 : x1;
                res.Add((new Point3d(xe, y0, 0), new Point3d(xe, y1, 0), new Point3d(xl, (y0 + y1) / 2, 0), Math.PI / 2));
            }
            return res;
        }

        /// <summary>Wymiary pręta (XData RC_BAR_DIM z jego uchwytem) w pobliżu pręta — kopie pręta z wymiarami dalej zostają.</summary>
        private static List<ObjectId> FindDims(Database db, ObjectId barId)
        {
            var res = new List<ObjectId>();
            if (barId.IsNull || barId.IsErased) return res;
            string h = barId.Handle.Value.ToString("X8");
            using var tr = db.TransactionManager.StartOpenCloseTransaction();
            Extents3d? box = null;
            try { if (tr.GetObject(barId, OpenMode.ForRead) is Entity be) box = be.GeometricExtents; }
            catch (System.Exception ex) { Log.Error("BarDimEngine.FindDims.Extents", ex); }
            var ms = (BlockTableRecord)tr.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForRead);
            foreach (ObjectId id in ms)
            {
                if (id.IsErased || !(tr.GetObject(id, OpenMode.ForRead) is Dimension dim)) continue;
                var rb = dim.GetXDataForApplication(XApp)?.AsArray();
                if (rb == null || rb.Length < 2 || !XLink.Same(XLink.Read(rb[1]), h)) continue;
                if (box.HasValue && dim is RotatedDimension rd)
                {
                    var b = box.Value; var p = rd.XLine1Point;
                    if (p.X < b.MinPoint.X - Near || p.X > b.MaxPoint.X + Near || p.Y < b.MinPoint.Y - Near || p.Y > b.MaxPoint.Y + Near) continue;
                }
                res.Add(id);
            }
            tr.Commit();
            return res;
        }
    }
}
