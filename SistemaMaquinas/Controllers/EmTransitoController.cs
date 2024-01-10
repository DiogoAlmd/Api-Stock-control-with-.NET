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
    public class EmTransitoController : ControllerBase
    {
        private readonly ILogger<EmTransitoController> _logger;
        private readonly string _connectionString;


        public EmTransitoController(ILogger<EmTransitoController> logger)
        {
            var builder = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory()).AddJsonFile("appsettings.json", optional: false);
            IConfigurationRoot configuration = builder.Build();
            _connectionString = configuration.GetValue<string>("ConnectionStrings:SqlConnection");
            _logger = logger;
        }

        [HttpGet("[action]/{id}/{usuario}")]
        public async Task<IActionResult> ObterDados(string id, string usuario)
        {
            var dados = new List<EmTransito>();

            using (var conexao = new NpgsqlConnection(_connectionString))
            {
                await conexao.OpenAsync();

                using (var comando = new NpgsqlCommand($@"SELECT eT.""SERIAL"", m.""MODELO"", eT.""OPERADORA"", s.""LOCAL"", u.""loginUsuario"", eT.""DATAENVIO"", eT.""TRANSPORTE"", eT.""LOTE""
	                                                        FROM db.""EMTRANSITO"" eT
	                                                        LEFT JOIN db.""Maquinas"" m ON eT.""SERIAL"" = m.""SERIAL""
	                                                        LEFT JOIN db.""STORE"" s ON eT.""DESTINO"" = s.""IDSTORE""
	                                                        LEFT JOIN db.users u ON eT.""REMETENTE"" = u.""idUsuario""
	                                                        WHERE ((eT.""DESTINO"" = {id} or (eT.""DESTINO""=1 and eT.""REMETENTE""=(select u.""idUsuario"" from db.users u where u.""loginUsuario""='{usuario}'))) AND {id} <> 1) OR ({id} = 1);", conexao))
                {
                    using (var leitor = await comando.ExecuteReaderAsync())
                    {
                        while (await leitor.ReadAsync())
                        {
                            dados.Add(new EmTransito
                            {
                                Serial = leitor["SERIAL"].ToString(),
                                Modelo = leitor["MODELO"].ToString(),
                                Operadora = leitor["OPERADORA"].ToString(),
                                Destino = leitor["LOCAL"].ToString(),
                                Remetente = leitor["loginUsuario"].ToString(),
                                Transporte = leitor["TRANSPORTE"].ToString(),
                                DataEnvio = leitor["DATAENVIO"].ToString(),
                                Lote = leitor["LOTE"].ToString(),
                            });
                        }
                    }
                    return Ok(dados);
                }
            }
        }


        [HttpPost("[action]")]
        public async Task<IActionResult> MoverParaStore([FromBody] MoverParaEmTransito request)
        {
            try
            {
                using (var conexao = new NpgsqlConnection(_connectionString))
                {
                    await conexao.OpenAsync();
                    using (var comando = new NpgsqlCommand($@"CALL db.spmoveemtransitoparastore('{request.Usuario}', '{request.Serial}')", conexao))
                    {
                        await comando.ExecuteNonQueryAsync();
                    }
                }
                return Ok();
            }
            catch (NpgsqlException ex)
            {
                var errorMessage = ex.Message;

                if (errorMessage.Contains("É necessário"))
                {
                    _logger.LogError(ex, $"Erro personalizado: {errorMessage}");
                    return StatusCode(409, new { Message = errorMessage });
                }

                // Trate outros erros do PostgreSQL aqui, se necessário

                // Caso nenhum erro específico seja encontrado, você pode adicionar um tratamento padrão aqui
                _logger.LogError(ex, $"Erro ao mover para EMTRANSITO: {errorMessage}");
                return StatusCode(500, new { Message = errorMessage });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erro ao mover {request.Serial} para Store");
                return StatusCode(500);
            }
        }

        [HttpPost("[action]")]
        public async Task<IActionResult> MoverParaArmario2([FromBody] MoverParaArmario2 request)
        {
            try
            {
                using (var conexao = new NpgsqlConnection(_connectionString))
                {
                    await conexao.OpenAsync();

                    using (var comando = new NpgsqlCommand($@"call db.spmoveemtransitoparaarmario2('{request.usuario}', '{request.Serial}')", conexao)
                                                        )
                    {
                        await comando.ExecuteNonQueryAsync();
                    }
                }

                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erro ao mover o serial {request.Serial} para a tabela ARMARIO_2");
                return StatusCode(500);
            }
        }

        [HttpPost("[action]")]
        public async Task<IActionResult> AlterarCodigo(AlterarMotivoDefeitos request)
        {
            try
            {
                using (var conexao = new NpgsqlConnection(_connectionString))
                {
                    await conexao.OpenAsync();

                    using (var comando = new NpgsqlCommand($@"do $$
                                                        declare
                                                            usuario int;
   	                                                        nomeStore varchar(50);
   	                                                        LOTE varchar(100);
                                                        begin
                                                            SELECT u.""idUsuario"" INTO usuario FROM db.users u WHERE u.""loginUsuario"" = '{request.usuario}';
                                                            select s.""LOCAL"" into nomeStore FROM db.""STORE"" s right join db.""EMTRANSITO"" eT on(s.""IDSTORE""=eT.""DESTINO"") WHERE eT.""SERIAL""='{request.Serial}';
   	                                                        select e.""LOTE"" into LOTE from db.""EMTRANSITO"" e WHERE e.""SERIAL"" = '{request.Serial}';
   	                                                        INSERT INTO db.""Historico""(""SERIAL"", ""ORIGEM"", ""DESTINO"", ""USUARIO"", ""LOCAL"", ""OPERADORA"", ""DATA"", ""DataAlteracao"", ""TRANSPORTE"", ""LOTE"")
                                                            SELECT e.""SERIAL"", 'EMTRANSITO', 'EMTRANSITO', usuario, nomeStore, e.""OPERADORA"", e.""DATAENVIO"", current_timestamp , e.""TRANSPORTE"", e.""LOTE"" FROM db.""EMTRANSITO"" e 
                                                            WHERE e.""LOTE"" = LOTE;
                                                            UPDATE db.""EMTRANSITO"" SET ""TRANSPORTE"" = '{request.NovoMotivo}' WHERE ""LOTE"" = LOTE;
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
    }
}

