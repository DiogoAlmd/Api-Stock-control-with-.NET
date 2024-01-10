using System.Security.Cryptography.X509Certificates;

namespace SistemaMaquinas.Models
{
    public class Historico
    {
        public string? Id { get; set; }
        public string? Serial { get; set; }
        public string? Origem { get; set; }
        public string? Destino { get; set; }
        public string? Usuario { get; set; }
        public string? Status { get; set; }
        public string? Situacao { get; set; }
        public string? Local { get; set; }
        public string? Operadora { get; set; }
        public string? DataRetirada { get; set; }
        public string? MaquinaPropriaDoCliente { get; set; }
        public string? Caixa { get; set; }
        public string? Motivo { get; set; }
        public string? Data { get; set; }
        public string? CNPF { get; set; }
        public string? Empresa { get; set; }
        public string? DataAlteracao { get; set; }
        public string? Transporte { get; set; }
    }
}
