namespace SistemaMaquinas.Models
{
    public class MotivoDefeito
    {
        public string? PedTampered { get; set; }
        public string? ErroNaLeituraDoCartao { get; set; }
        public string? Touch { get; set; }
        public string? ConectorComDefeito { get; set; }
        public string? ConectividadeDeChips { get; set; }
        public string? Estetica { get; set; }
        public string? DefeitoDeImpressao { get; set; }
        public string? Teclado { get; set; }
        public string? TelaQuebrada { get; set; }

        public string? Total { get; set; }
    }
}
