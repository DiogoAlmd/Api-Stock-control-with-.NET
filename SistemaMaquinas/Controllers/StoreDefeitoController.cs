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
    public class StoreDefeitoController : ControllerBase
    {
        private readonly ILogger<StoreDefeitoController> _logger;
        private readonly string _connectionString;


        public StoreDefeitoController(ILogger<StoreDefeitoController> logger)
        {
            var builder = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory()).AddJsonFile("appsettings.json", optional: false);
            IConfigurationRoot configuration = builder.Build();
            _connectionString = configuration.GetValue<string>("ConnectionStrings:SqlConnection");
            _logger = logger;
        }

        [HttpGet("[action]/{id}")]
        public async Task<IActionResult> ObterDados(string id)
        {
            var dados = new List<DefeitoExterior>();
            using (var conexao = new NpgsqlConnection(_connectionString))
            {
                await conexao.OpenAsync();

                using (var comando = new NpgsqlCommand($@"SELECT eE.""SERIAL"", s.""LOCAL"", m.""MODELO""
                                                        FROM db.""DefeitoExterior"" eE
                                                        LEFT JOIN db.""Maquinas"" m ON eE.""SERIAL"" = m.""SERIAL""
                                                        LEFT JOIN db.""STORE"" s ON eE.""LOCAL"" = s.""IDSTORE""
                                                        WHERE (eE.""LOCAL"" = {id} AND {id} != 1)
                                                            OR ({id} = 1);", conexao))
                {
                    using (var leitor = await comando.ExecuteReaderAsync())
                    {
                        while (await leitor.ReadAsync())
                        {
                            dados.Add(new DefeitoExterior
                            {
                                Serial = leitor["SERIAL"].ToString(),
                                Modelo = leitor["MODELO"].ToString(),
                                Local = leitor["LOCAL"].ToString()
                            });
                        }
                    }
                    return Ok(dados);
                }
            }
        }

        [HttpPost("[action]")]
        public async Task<IActionResult> MoverParaEmTransito([FromBody] MoverEmTransitoEmMassa request)
        {
            try
            {
                using (var conexao = new NpgsqlConnection(_connectionString))
                {
                    await conexao.OpenAsync();

                    var seriaisArray = request.Seriais.Select(s => $"'{s}'").ToArray();
                    var seriaisString = string.Join(",", seriaisArray);

                    using (var comando = new NpgsqlCommand($@"select db.spmovelotestoredefeitoemtransito(ARRAY[{seriaisString}], '{request.Usuario}', '{request.Local}', '{request.Transporte}')", conexao)
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

                if (errorMessage.Contains("O serial"))
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
                _logger.LogError(ex, $"Erro ao mover o serial para a tabela EMTRANSITO");
                return StatusCode(500);
            }
        }


        [HttpGet("[action]")]
        public async Task<IActionResult> Modelos()
        {
            var CidadesEstrangeiras = new List<CidadesEstrangeiras>();

            using (var conexao = new NpgsqlConnection(_connectionString))
            {
                await conexao.OpenAsync();

                using (var comando = new NpgsqlCommand(@"SELECT
                                                         SUM(CASE WHEN ""LOCAL"" = 4 THEN 1 ELSE 0 END) AS ""STORE RIO PRETO"",
                                                         SUM(CASE WHEN ""LOCAL""= 6 THEN 1 ELSE 0 END) AS ""STORE CAMPO GRANDE"",
                                                         SUM(CASE WHEN ""LOCAL"" = 8 THEN 1 ELSE 0 END) AS ""RIO DE JANEIRO"",
                                                         SUM(CASE WHEN ""LOCAL"" = 9 THEN 1 ELSE 0 END) AS ""STORE CAMPINAS"",
                                                         SUM(CASE WHEN ""LOCAL"" = 2 THEN 1 ELSE 0 END) AS ""STORE SOROCABA"",
                                                         COUNT(""SERIAL"") AS Total
                                                       FROM db.""DefeitoExterior""", conexao))
                {
                    using (var leitor = await comando.ExecuteReaderAsync())
                    {
                        while (await leitor.ReadAsync())
                        {
                            CidadesEstrangeiras.Add(new CidadesEstrangeiras
                            {
                                RioPreto = leitor["STORE RIO PRETO"].ToString(),
                                CampoGrande = leitor["STORE CAMPO GRANDE"].ToString(),
                                RioDeJaneiro = leitor["RIO DE JANEIRO"].ToString(),
                                Campinas = leitor["STORE CAMPINAS"].ToString(),
                                Sorocaba = leitor["STORE SOROCABA"].ToString(),
                                Total = leitor["Total"].ToString()
                            });
                        }
                    }
                    return Ok(CidadesEstrangeiras);
                }
            }
        }

        [HttpGet("[action]/{id}")]
        public async Task<IActionResult> ModelosReais(string id)
        {
            var modelos = new List<Modelos>();

            using (var conexao = new NpgsqlConnection(_connectionString))
            {
                await conexao.OpenAsync();

                using (var comando = new NpgsqlCommand($@"SELECT
                                                      SUM(CASE WHEN m.""MODELO"" = 'D3 - PRO 1' THEN 1 ELSE 0 END) AS ""D3 - PRO 1"",
                                                      SUM(CASE WHEN m.""MODELO"" = 'D3 - PRO 2' THEN 1 ELSE 0 END) AS ""D3 - PRO 2"",
                                                      SUM(CASE WHEN m.""MODELO"" = 'D3 - PRO REFURBISHED' THEN 1 ELSE 0 END) AS ""D3 - PRO REFURBISHED"",
                                                      SUM(CASE WHEN m.""MODELO"" = 'D3 - SMART' THEN 1 ELSE 0 END) AS ""D3 - SMART"",
                                                      SUM(CASE WHEN m.""MODELO"" = 'D3 - X' THEN 1 ELSE 0 END) AS ""D3 - X"",
                                                      COUNT(a.""SERIAL"") AS Total
                                                    FROM
                                                      db.""DefeitoExterior"" a
                                                      LEFT JOIN db.""Maquinas"" m ON a.""SERIAL"" = m.""SERIAL""
                                                    WHERE
                                                      (a.""LOCAL"" = {id} AND {id} != 1)
                                                      OR {id}= 1;", conexao))
                {
                    using (var leitor = await comando.ExecuteReaderAsync())
                    {
                        while (await leitor.ReadAsync())
                        {
                            modelos.Add(new Modelos
                            {
                                d3Pro1 = leitor["D3 - PRO 1"].ToString(),
                                d3Pro2 = leitor["D3 - PRO 2"].ToString(),
                                d3ProRefurbished = leitor["D3 - PRO REFURBISHED"].ToString(),
                                d3Smart = leitor["D3 - SMART"].ToString(),
                                d3X = leitor["D3 - X"].ToString(),
                                Total = leitor["Total"].ToString()
                            });
                        }
                    }
                    return Ok(modelos);
                }
            }
        }
    }
}
