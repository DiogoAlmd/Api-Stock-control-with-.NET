namespace SistemaMaquinas.Classes
{
    public class MoverParaEmTransito
    {
        public string? Serial { get; set; }
        public string? Local { get; set; }
        public string? Usuario { get; set; }
        public string? Transporte { get; set; }
    }

    public class MoverEmTransitoEmMassa
    {
        public string[] Seriais { get; set; }
        public string? Local { get; set; }
        public string? Usuario { get; set; }
        public string? Transporte { get; set; }
    }
}
