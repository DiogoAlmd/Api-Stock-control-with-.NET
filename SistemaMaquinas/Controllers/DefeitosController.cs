using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SistemaMaquinas.Classes;
using SistemaMaquinas.Models;
using Npgsql;

namespace SistemaMaquinas.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class DefeitosController : ControllerBase
    {
        private readonly ILogger<DefeitosController> _logger;
        private readonly string _connectionString;

        public DefeitosController(ILogger<DefeitosController> logger)
        {
            var builder = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory()).AddJsonFile("appsettings.json", optional: false);
            IConfigurationRoot configuration = builder.Build();
            _connectionString = configuration.GetValue<string>("ConnectionStrings:SqlConnection");
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> ObterDados()
        {
            var dados = new List<Defeitos>();

            using (var conexao = new NpgsqlConnection(_connectionString))
            {
                await conexao.OpenAsync();

                using (var comando = new NpgsqlCommand(@"select d.*, m.""MODELO"" from db.""DEFEITOS"" d left join db.""Maquinas"" m on (d.""SERIAL"" = m.""SERIAL"")", conexao))
                {
                    using (var leitor = await comando.ExecuteReaderAsync())
                    {
                        while (await leitor.ReadAsync())
                        {
                            dados.Add(new Defeitos
                            {
                                Serial = leitor["SERIAL"].ToString(),
                                Modelo = leitor["MODELO"].ToString(),
                                Caixa = leitor["CAIXA"].ToString(),
                                Data = leitor["DATA"].ToString().Replace("00:00:00", ""),
                                Motivo = leitor["MOTIVO"].ToString()
                            });
                        }
                    }
                    return Ok(dados);
                }
            }
        }

        [HttpPost("[action]")]
        public async Task<IActionResult> AlterarMotivo(AlterarMotivoDefeitos request)
        {
            try
            {
                using (var conexao = new NpgsqlConnection(_connectionString))
                {
                    await conexao.OpenAsync();

                    using (var comando = new NpgsqlCommand($@"do $$
                                                            declare
                                                                usuario int;
                                                            begin
                                                                SELECT u.""idUsuario"" INTO usuario FROM db.users u WHERE u.""loginUsuario"" = '{request.usuario}';
                                                                INSERT INTO db.""Historico""(""SERIAL"", ""ORIGEM"", ""DESTINO"", ""USUARIO"", ""MOTIVO"", ""CAIXA"", ""DATA"", ""DataAlteracao"")
                                                                SELECT d.""SERIAL"", 'DEFEITOS', 'DEFEITOS', usuario, d.""MOTIVO"", d.""CAIXA"", d.""DATA"", current_timestamp FROM db.""DEFEITOS"" d 
                                                                WHERE d.""SERIAL"" = '{request.Serial}';
   	                                                            UPDATE db.""DEFEITOS"" SET ""MOTIVO"" = '{request.NovoMotivo}' WHERE ""SERIAL"" = '{request.Serial}';
                                                            end $$;", conexao)
                                                        )
                    {
                        await comando.ExecuteNonQueryAsync();
                    }
                }

                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erro ao alterar o motivo do serial {request.Serial} da tabela DEFEITOS");
                return StatusCode(500);
            }
        }

        [HttpPost("[action]")]
        public async Task<IActionResult> MoverParaDevolucao([FromBody] MoverParaDevolucao request)
        {
            try
            {
                using (var conexao = new NpgsqlConnection(_connectionString))
                {
                    await conexao.OpenAsync();

                    using (var comando = new NpgsqlCommand($@"do $$
                                                            declare
                                                                usuario int;
                                                            begin
                                                                SELECT u.""idUsuario"" INTO usuario FROM db.users u WHERE u.""loginUsuario"" = '{request.usuario}';
                                                                INSERT INTO db.""Historico""(""SERIAL"", ""ORIGEM"", ""DESTINO"", ""USUARIO"", ""MOTIVO"", ""CAIXA"", ""DATA"", ""DataAlteracao"")
                                                                SELECT d.""SERIAL"", 'DEFEITOS', 'DEVOLUCAO', usuario, d.""MOTIVO"", d.""CAIXA"", d.""DATA"", current_timestamp FROM db.""DEFEITOS"" d 
                                                                WHERE d.""SERIAL"" = '{request.Serial}';
   	                                                            INSERT into db.""DEVOLUCAO"" (""SERIAL"", ""CAIXA"", ""DATA"") values ('{request.Serial}', '{request.Caixa}', current_timestamp);
                                                                DELETE FROM db.""DEFEITOS"" WHERE ""SERIAL"" = '{request.Serial}';
                                                            end $$;", conexao)
                                                         )
                    {
                        await comando.ExecuteNonQueryAsync();
                    }
                }

                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erro ao mover o serial {request.Serial} para a tabela DEVOLUCAO");
                return StatusCode(500);
            }
        }

        [HttpGet("[action]")]
        public async Task<IActionResult> Motivos()
        {
            var motivodefeito = new List<MotivoDefeito>();

            using (var conexao = new NpgsqlConnection(_connectionString))
            {
                await conexao.OpenAsync();

                using (var comando = new NpgsqlCommand(@"SELECT
                                                         SUM(CASE WHEN ""MOTIVO"" = 'Ped Tampered' THEN 1 ELSE 0 END) AS ""Ped Tampered"",
                                                         SUM(CASE WHEN ""MOTIVO"" = 'Erro na leitura do cartão' THEN 1 ELSE 0 END) AS ""Erro na leitura do cartão"",
                                                         SUM(CASE WHEN ""MOTIVO"" = 'Touch' THEN 1 ELSE 0 END) AS ""Touch"",
                                                         SUM(CASE WHEN ""MOTIVO"" = 'Conector com defeito' THEN 1 ELSE 0 END) AS ""Conector com defeito"",
                                                         SUM(CASE WHEN ""MOTIVO"" = 'Conectividade de chips' THEN 1 ELSE 0 END) AS ""Conectividade de chips"",
                                                         SUM(CASE WHEN ""MOTIVO"" = 'Estética' THEN 1 ELSE 0 END) AS ""Estética"",
                                                         SUM(CASE WHEN ""MOTIVO"" = 'Defeito de Impressão' THEN 1 ELSE 0 END) AS ""Defeito de Impressão"",
                                                         SUM(CASE WHEN ""MOTIVO"" = 'Teclado' THEN 1 ELSE 0 END) AS ""Teclado"",
                                                         SUM(CASE WHEN ""MOTIVO"" = 'Tela quebrada' THEN 1 ELSE 0 END) AS ""Tela quebrada"",
                                                         COUNT(""SERIAL"") AS Total
                                                       FROM db.""DEFEITOS""", conexao))
                {
                    using (var leitor = await comando.ExecuteReaderAsync())
                    {
                        while (await leitor.ReadAsync())
                        {
                            motivodefeito.Add(new MotivoDefeito
                            {
                                PedTampered = leitor["Ped Tampered"].ToString(),
                                ErroNaLeituraDoCartao = leitor["Erro na leitura do cartão"].ToString(),
                                Touch = leitor["Touch"].ToString(),
                                ConectorComDefeito = leitor["Conector com defeito"].ToString(),
                                ConectividadeDeChips = leitor["Conectividade de chips"].ToString(),
                                Estetica = leitor["Estética"].ToString(),
                                DefeitoDeImpressao = leitor["Defeito de Impressão"].ToString(),
                                Teclado = leitor["Teclado"].ToString(),
                                TelaQuebrada = leitor["Tela quebrada"].ToString(),
                                Total = leitor["Total"].ToString()
                            });
                        }
                    }
                    return Ok(motivodefeito);
                }
            }
        }
    }
}
