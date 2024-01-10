namespace SistemaMaquinas.Models
{
    public class Solicitacao
    {
        public string? Id { get; set; }
        public string? Usuario { get; set; }
        public string? Store { get; set; }
        public string? Modelo { get; set; }
        public string? Quantidade { get; set; }
        public string? Enviadas { get; set; }

        public string? Recebidas { get; set; }

        public string? Finalizada { get; set; }
        public string? Data { get; set; }
    }
}
