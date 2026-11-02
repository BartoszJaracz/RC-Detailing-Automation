using System;
using System.Collections.Generic;
using System.Linq;
using Bricscad.ApplicationServices;
using Teigha.DatabaseServices;
using Teigha.Runtime;
using BricsCadRc.Core;

namespace BricsCadRc.Commands
{
    /// <summary>
    /// RC_ZONES (uwaga, APARTMENT-TEST screen 3: „fioletowe obramówki — może warstwa niewidoczna, którą można jednym
    /// klikiem, komendą odblokować”): pokazuje / ukrywa obramówki stref pomocniczych — wsporniki i wypustki (RC-CANTILEVER) i belki
    /// z pogrubienia w narożniku (RC-BEAM-AUX). Gdy którakolwiek warstwa jest włączona — obie wyłączane, inaczej obie włączane.
    /// Warstwy zostają niedrukowane. Strefy działają tak samo przy warstwach wyłączonych (RC_GENERATE, RC_BEAM czytają rysunek).
    /// </summary>
    public static class ZoneLayerCommands
    {
        private const string T = "[RC ZONES]";

        [CommandMethod("RC_ZONES", CommandFlags.Modal)]
        public static void ToggleZones()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var ed = doc.Editor;
            var db = doc.Database;
            try
            {
                var names = new[] { CantileverEngine.LayerName, BeamStore.AuxLayerName };
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                    var recs = names.Where(n => lt.Has(n)).Select(n => (LayerTableRecord)tr.GetObject(lt[n], OpenMode.ForWrite)).ToList();
                    if (recs.Count == 0)
                    {
                        ed.WriteMessage($"\n{T} Brak warstw stref pomocniczych ({string.Join(", ", names)}) — nic do pokazania.\n");
                        tr.Commit();
                        return;
                    }
                    bool hide = recs.Any(r => !r.IsOff);
                    if (hide && recs.Any(r => r.ObjectId == db.Clayer) && lt.Has("0")) db.Clayer = lt["0"];   // bieżącej warstwy nie wyłączamy
                    foreach (var r in recs)
                    {
                        r.IsOff = hide;
                        r.IsPlottable = false;
                    }
                    tr.Commit();
                    ed.WriteMessage($"\n{T} Obramówki stref ({string.Join(", ", recs.Select(r => r.Name))}): " +
                                    (hide ? "UKRYTE" : "WIDOCZNE (bez wydruku)") + " — ponownie RC_ZONES przełącza.\n");
                }
                ed.Regen();
            }
            catch (System.Exception ex)
            {
                Log.Error("RC_ZONES", ex);
                ed.WriteMessage($"\n*** ERROR *** {T} {ex.Message}\n");
            }
        }
    }
}
