namespace OneWare.Vhdl
{
    internal class VhdlFoldingStrategy : VhdpFoldingStrategy
    {
        public VhdlFoldingStrategy()
        {
            Foldings.Add(new FoldingEntry("ARCHITECTURE BEHAVIORAL", "END BEHAVIORAL;",
                StringComparison.CurrentCultureIgnoreCase));
        }
    }
}