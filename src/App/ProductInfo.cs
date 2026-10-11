namespace BricsCadRc.App
{
    /// <summary>
    /// Nazwa produktu „RC Automation” — odróżnia tę wtyczkę od starej BricsCadRc (wersja bazowa).
    /// Obie mogą być zainstalowane; wczytana jest jedna naraz (autoload). Komendy i metki XData
    /// zostają wspólne, więc rysunki są zgodne w obie strony.
    /// </summary>
    public static class ProductInfo
    {
        public const string Name = "RC Automation";

        /// <summary>Folder w %LOCALAPPDATA% / %APPDATA% (log, ustawienia).</summary>
        public const string FolderName = "RcAutomation";

        /// <summary>Folder starej wtyczki — tylko do jednorazowego przejęcia ustawień.</summary>
        public const string LegacyFolderName = "BricsCadRc";
    }
}
