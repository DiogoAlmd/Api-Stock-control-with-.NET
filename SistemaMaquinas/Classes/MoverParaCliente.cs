namespace SistemaMaquinas.Classes
{
    public class MoverParaCliente
    {
        public string? serial { get; set; }
        public string? CNPF { get; set; }
        public string? empresa { get; set; }
        public string? usuario { get; set; }
        public string? store { get; set; }
    }

    public class MoverParaClienteEmMassa
    {
        public string[] seriais { get; set; }
        public string? CNPF { get; set; }
        public string? empresa { get; set; }
        public string? usuario { get; set; }
        public string? store { get; set; }
    }


}
