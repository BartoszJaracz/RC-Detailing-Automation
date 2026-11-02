using System;
using System.Collections.Generic;
using Bricscad.ApplicationServices;
using Bricscad.EditorInput;
using Teigha.DatabaseServices;
using Teigha.Runtime;
using BricsCadRc.Core;

namespace BricsCadRc.Commands
{
    /// <summary>
 /// RC_BAR_DIMS: wymiary zewnętrzne wskazanych prętów (BarDimEngine) — nowe albo przerysowane po przesunięciu / zmianie
    /// pręta (wymiary nie są skojarzone). Nowe pręty (RC_BAR, szablony RC_GENERATE) dostają wymiary od razu.
    /// </summary>
    public static class BarDimCommands
    {
        private const string T = "[RC BAR DIMS]";

        [CommandMethod("RC_BAR_DIMS", CommandFlags.Modal)]
        public static void BarDims()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var ed = doc.Editor;
            var db = doc.Database;
            try
            {
                var sel = ed.GetSelection(new PromptSelectionOptions { MessageForAdding = "\nWskaż pręty do zwymiarowania: " });
                if (sel.Status != PromptStatus.OK) return;
                var bars = new List<ObjectId>();
                using (var tr = db.TransactionManager.StartOpenCloseTransaction())
                {
                    foreach (var id in sel.Value.GetObjectIds())
                    {
                        var be = BarEntity.Open(tr, id, OpenMode.ForRead, BarKinds.Polyline | BarKinds.Block);
                        if (be?.Read() != null) bars.Add(id);
                    }
                    tr.Commit();
                }
                if (bars.Count == 0) { ed.WriteMessage($"\n{T} W zaznaczeniu nie ma prętów RC.\n"); return; }
                int n = 0;
                using (doc.LockDocument())
                    foreach (var id in bars) n += BarDimEngine.Dimension(db, id);
                ed.WriteMessage($"\n{T} Pręty: {bars.Count}, wymiary: {n}.\n");
            }
            catch (System.Exception ex)
            {
                Log.Error("RC_BAR_DIMS", ex);
                ed.WriteMessage($"\n*** ERROR *** {T} {ex.Message}\n");
            }
        }
    }
}
