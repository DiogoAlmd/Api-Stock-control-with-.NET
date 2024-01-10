using Microsoft.AspNetCore.Mvc;
using SistemaMaquinas.Models;
using SistemaMaquinas.Classes;
using Microsoft.AspNetCore.Authorization;
using Npgsql;

namespace SistemaMaquinas.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class SolicitacaoController : ControllerBase
    {
        private readonly ILogger<SolicitacaoController> _logger;
        private readonly string _connectionString;


        public SolicitacaoController(ILogger<SolicitacaoController> logger)
        {
            var builder = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory()).AddJsonFile("appsettings.json", optional: false);
            IConfigurationRoot configuration = builder.Build();
            _connectionString = configuration.GetValue<string>("ConnectionStrings:SqlConnection");
            _logger = logger;
        }

        [HttpGet("[action]/{id}")]
        public async Task<IActionResult> ObterDados(string id)
        {
            var dados = new List<Solicitacao>();

            using (var conexao = new NpgsqlConnection(_connectionString))
            {
                await conexao.OpenAsync();

                using (var comando = new NpgsqlCommand($@"SELECT sO.""IDSOLICITACAO"", u.""loginUsuario"", sT.""LOCAL"", sO.""MODELO"", sO.""QTD"", sO.""DATASOLICITACAO"", sO.""ENVIADAS"", sO.""RECEBIDAS""
                                                        FROM db.""SOLICITACAO"" sO
                                                        LEFT JOIN db.""STORE"" sT ON sO.""IDSTORE"" = sT.""IDSTORE""
                                                        LEFT JOIN db.users u ON sO.""IdUsuario"" = u.""idUsuario""
                                                        WHERE (sO.""IDSTORE"" = {id} OR {id} = 1)
                                                          AND sO.""FINALIZADA"" = false;", conexao))
                {
                    using (var leitor = await comando.ExecuteReaderAsync())
                    {
                        while (await leitor.ReadAsync())
                        {
                            dados.Add(new Solicitacao
                            {
                                Id = leitor["IDSOLICITACAO"].ToString(),
                                Usuario = leitor["loginUsuario"].ToString(),
                                Store = leitor["LOCAL"].ToString(),
                                Modelo = leitor["MODELO"].ToString(),
                                Quantidade = leitor["QTD"].ToString(),
                                Recebidas = leitor["RECEBIDAS"].ToString(),
                                Enviadas = leitor["ENVIADAS"].ToString(),
                                Data = leitor["DATASOLICITACAO"].ToString()
                            });
                        }
                    }
                    return Ok(dados);
                }
            }
        }

        [HttpGet("[action]/{id}")]
        public async Task<IActionResult> VerificaNaoFinalizadas(string id)
        {
            var dados = new List<Solicitacao>();

            using (var conexao = new NpgsqlConnection(_connectionString))
            {
                await conexao.OpenAsync();

                using (var comando = new NpgsqlCommand($@"SELECT COUNT(*) AS ""QTD""
                                                        FROM db.""SOLICITACAO""
                                                        WHERE (""IDSTORE"" = {id} AND ""FINALIZADA"" = false) OR ({id} = 1 AND ""FINALIZADA"" = false)", conexao))
                {
                    int contador = Convert.ToInt32(await comando.ExecuteScalarAsync());

                    // Agora você pode usar o valor do contador conforme necessário.
                    // Por exemplo, retornar um objeto JSON com o valor:
                    return Ok(new { Contador = contador });
                }
            }
        }


        [HttpPost("[action]")]
        public async Task<IActionResult> SolicitarMaquina([FromBody] SolicitaMaquina request)
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
	                                                            SELECT u.""idUsuario"" INTO usuario FROM db.users u WHERE u.""loginUsuario"" = '{request.Usuario}';
	                                                            INSERT INTO db.""SOLICITACAO""(""IDSTORE"", ""IdUsuario"", ""MODELO"", ""QTD"", ""DATASOLICITACAO"", ""RECEBIDAS"", ""ENVIADAS"", ""FINALIZADA"")
                                                                values ({request.Store}, usuario, '{request.Modelo}', {request.Quantidade}, current_timestamp, 0, 0, false);
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
                _logger.LogError(ex, $"Erro ao solicitar uma maquina para {request.Usuario}");
                return StatusCode(500);
            }
        }


        [HttpDelete("[action]/{Id}")]
        public async Task<IActionResult> DeleteSolicitacao(string Id)
        {
            try
            {
                using (var conexao = new NpgsqlConnection(_connectionString))
                {
                    await conexao.OpenAsync();

                    using (var comando = new NpgsqlCommand($@"do $$
                                                            declare
	                                                            ENVIADAS int;
	                                                            RECEBIDAS int;
                                                            begin
	                                                            SELECT s.""ENVIADAS"" into ENVIADAS FROM db.""SOLICITACAO"" s WHERE s.""IDSOLICITACAO"" = {Id};
	                                                            SELECT s.""RECEBIDAS"" into RECEBIDAS FROM db.""SOLICITACAO"" s WHERE s.""IDSOLICITACAO"" = {Id};
	                                                            if ENVIADAS <> RECEBIDAS then
		                                                            RAISE EXCEPTION 'Não é possivel excluir a solicitação';
	                                                            else
		                                                            DELETE from db.""SOLICITACAO"" WHERE ""IDSOLICITACAO"" = {Id};
	                                                            end if;
                                                            end $$;", conexao)
                                                           )
                    {
                        await comando.ExecuteNonQueryAsync();
                    }
                }
                return Ok();
            }
            catch (NpgsqlException ex)
            {
                var errorMessage = ex.Message;

                if (errorMessage.Contains("Não é"))
                {
                    _logger.LogError(ex, $"Erro personalizado: {errorMessage}");
                    return StatusCode(422, new { Message = errorMessage });
                }

                // Trate outros erros do PostgreSQL aqui, se necessário

                // Caso nenhum erro específico seja encontrado, você pode adicionar um tratamento padrão aqui
                _logger.LogError(ex, $"Erro ao mover para EMTRANSITO: {errorMessage}");
                return StatusCode(500, new { Message = errorMessage });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erro ao deletar solicitação");
                return StatusCode(500);
            }
        }
    }
}
