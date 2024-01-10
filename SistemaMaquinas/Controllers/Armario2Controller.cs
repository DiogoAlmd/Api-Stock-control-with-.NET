using Microsoft.AspNetCore.Mvc;
using SistemaMaquinas.Models;
using SistemaMaquinas.Classes;
using SistemaMaquinas.Repositories;
using Microsoft.AspNetCore.Authorization;
using Npgsql;

namespace SistemaMaquinas.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class Armario2Controller : ControllerBase
    {
        private readonly ILogger<Armario2Controller> _logger;
        private readonly string _connectionString;

        public Armario2Controller(ILogger<Armario2Controller> logger)
        {
            var builder = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory()).AddJsonFile("appsettings.json", optional: false);
            IConfigurationRoot configuration = builder.Build();
            _connectionString = configuration.GetValue<string>("ConnectionStrings:SqlConnection");
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> ObterDados()
        {
            var dados = new List<Armario2>();

            using (var conexao = new NpgsqlConnection(_connectionString))
            {
                await conexao.OpenAsync();

                using (var comando = new NpgsqlCommand(@$"select a2.*, m.""MODELO"" from db.""ARMARIO_2"" a2 left outer join db.""Maquinas"" m on (a2.""SERIAL"" = m.""SERIAL"")", conexao))
                {
                    using (var leitor = await comando.ExecuteReaderAsync())
                    {
                        while (await leitor.ReadAsync())
                        {
                            dados.Add(new Armario2
                            {
                                Serial = leitor["SERIAL"].ToString(),
                                Modelo = leitor["MODELO"].ToString(),
                                Status = leitor["STATUS"].ToString(),
                                Situacao = leitor["SITUACAO"].ToString(),
                                Local = leitor["LOCAL"].ToString(),
                            });
                        }
                    }
                    return Ok(dados);
                }
            }
        }

        [HttpPost("[action]")]
        public async Task<IActionResult> MoverParaArmario1([FromBody] MoverParaArmario1 request)
        {
            try
            {
                using (var conexao = new NpgsqlConnection(_connectionString))
                {
                    await conexao.OpenAsync();

                    string result = String.Join(",", request.Seriais);

                    using (var comando = new NpgsqlCommand($@"select db.spmovearmario2paraarmario1('{result}', '{request.Usuario}', '{request.Operadora}', '{request.Propriedade}');", conexao)
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
                _logger.LogError(ex, $"Erro ao mover o serial para a tabela ARMARIO_1");
                return StatusCode(500);
            }
        }

        [HttpPost("[action]")]
        public async Task<IActionResult> MoverParaDefeito([FromBody] MoverParaDefeito request)
        {
            try
            {
                var sqlQuery = $@"do $$
                                declare
	                                usuario int;
                                begin
	                                SELECT u.""idUsuario"" INTO usuario FROM db.users u WHERE u.""loginUsuario"" = '{request.usuario}';
	                                INSERT INTO db.""Historico""(""SERIAL"", ""ORIGEM"", ""DESTINO"", ""USUARIO"", ""STATUS"", ""SITUACAO"", ""LOCAL"", ""DataAlteracao"")
	                                SELECT a.""SERIAL"", 'ARMARIO_2', 'DEFEITOS', usuario, a.""STATUS"", a.""SITUACAO"", a.""LOCAL"", current_timestamp FROM db.""ARMARIO_2"" a 
	                                WHERE a.""SERIAL"" = '{request.serial}';
	                                INSERT INTO db.""DEFEITOS""(""SERIAL"", ""CAIXA"", ""MOTIVO"", ""DATA"")
  	                                SELECT a.""SERIAL"", '{request.caixa}', '{request.motivo}', current_timestamp FROM db.""ARMARIO_2"" a  WHERE a.""SERIAL"" = '{request.serial}';
  	                                DELETE FROM db.""ARMARIO_2"" WHERE ""SERIAL"" = '{request.serial}';
                                end $$;";
                var repository = new DefeitosRepository(_connectionString, _logger, sqlQuery);
                await repository.MoverParaDefeito(request);
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erro ao mover o serial {request.serial} para a tabela DEFEITOS");
                return StatusCode(500);
            }
        }

        [HttpGet("[action]")]
        public async Task<IActionResult> Modelos()
        {
            var modelos = new List<Modelos>();

            using (var conexao = new NpgsqlConnection(_connectionString))
            {
                await conexao.OpenAsync();

                using (var comando = new NpgsqlCommand(@" SELECT
                                                            SUM(CASE WHEN m.""MODELO"" = 'D3 - PRO 1' THEN 1 ELSE 0 END) AS ""D3 - PRO 1"",
                                                            SUM(CASE WHEN m.""MODELO"" = 'D3 - PRO 2' THEN 1 ELSE 0 END) AS ""D3 - PRO 2"",
                                                            SUM(CASE WHEN m.""MODELO"" = 'D3 - PRO REFURBISHED' THEN 1 ELSE 0 END) AS ""D3 - PRO REFURBISHED"",
                                                            SUM(CASE WHEN m.""MODELO"" = 'D3 - SMART' THEN 1 ELSE 0 END) AS ""D3 - SMART"",
                                                            SUM(CASE WHEN m.""MODELO"" = 'D3 - X' THEN 1 ELSE 0 END) AS ""D3 - X"",
                                                            COUNT(a.""SERIAL"") AS Total
                                                        FROM
                                                            db.""ARMARIO_2"" a
                                                            LEFT JOIN db.""Maquinas"" m ON a.""SERIAL"" = m.""SERIAL"";", conexao))
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

