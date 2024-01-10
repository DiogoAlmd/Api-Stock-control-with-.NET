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
    public class EstoqueABController : ControllerBase
    {
        private readonly ILogger<EstoqueABController> _logger;
        private readonly string _connectionString;

        public EstoqueABController(ILogger<EstoqueABController> logger)
        {
            var builder = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory()).AddJsonFile("appsettings.json", optional: false);
            IConfigurationRoot configuration = builder.Build();
            _connectionString = configuration.GetValue<string>("ConnectionStrings:SqlConnection");
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> ObterDados()
        {
            var dados = new List<EstoqueAB>();

            using (var conexao = new NpgsqlConnection(_connectionString))
            {
                await conexao.OpenAsync();

                using (var comando = new NpgsqlCommand(@"select ab.*, m.""MODELO"" from db.""ESTOQUE_AB"" ab left join db.""Maquinas"" m on (ab.""SERIAL"" = m.""SERIAL"")", conexao))
                {
                    using (var leitor = await comando.ExecuteReaderAsync())
                    {
                        while (await leitor.ReadAsync())
                        {
                            dados.Add(new EstoqueAB
                            {
                                Serial = leitor["SERIAL"].ToString(),
                                Modelo = leitor["MODELO"].ToString(),
                                Status = leitor["STATUS"].ToString(),
                                Situacao = leitor["SITUACAO"].ToString(),
                                Local = leitor["LOCAL"].ToString()
                            });
                        }
                    }
                    return Ok(dados);
                }
            }
        }
        [HttpPost("[action]/{serial}/{novaTabela}/{usuario}")]
        public async Task<IActionResult> MoverParaNovaTabela(string serial, string novaTabela, string usuario, string? propriedade, string? operadora)
        {
            try
            {
                using (var conexao = new NpgsqlConnection(_connectionString))
                {
                    await conexao.OpenAsync();

                    switch (novaTabela)
                    {
                        case "ARMARIO_1":
                            using (var comando = new NpgsqlCommand($@"do $$
                                                                    declare
                                                                        usuario int;
                                                                    begin
                                                                        SELECT u.""idUsuario"" INTO usuario FROM db.users u WHERE u.""loginUsuario"" = '{usuario}';
                                                                        INSERT INTO db.""Historico""(""SERIAL"", ""ORIGEM"", ""DESTINO"", ""USUARIO"", ""STATUS"", ""SITUACAO"", ""LOCAL"", ""DataAlteracao"")
                                                                        SELECT e.""SERIAL"", 'ESTOQUE_AB', 'ARMARIO_1', usuario, e.""STATUS"", e.""SITUACAO"", e.""LOCAL"", current_timestamp FROM db.""ESTOQUE_AB"" e WHERE e.""SERIAL"" = '{serial}';
                                                                        INSERT INTO db.""ARMARIO_1""(""SERIAL"", ""STATUS"", ""SITUACAO"", ""LOCAL"", ""OPERADORA"", ""MaquinaPropriaDoCliente"")
                                                                        SELECT e.""SERIAL"", 'ATIVAÇÃO', 'TRATADO', e.""LOCAL"", '{operadora}', '{propriedade}' FROM db.""ESTOQUE_AB"" e WHERE e.""SERIAL"" = '{serial}';
                                                                        DELETE FROM db.""ESTOQUE_AB"" WHERE ""SERIAL"" = '{serial}';
                                                                    end $$;", conexao)
                                                                )
                            {
                                await comando.ExecuteNonQueryAsync();
                            }
                            break;

                        case "ARMARIO_3":
                            using (var comando = new NpgsqlCommand($@"do $$
                                                                    declare
                                                                        usuario int;
                                                                    begin
                                                                        SELECT u.""idUsuario"" INTO usuario FROM db.users u WHERE u.""loginUsuario"" = '{usuario}';
                                                                        INSERT INTO db.""Historico""(""SERIAL"", ""ORIGEM"", ""DESTINO"", ""USUARIO"", ""STATUS"", ""SITUACAO"", ""LOCAL"", ""DataAlteracao"")
                                                                        SELECT e.""SERIAL"", 'ESTOQUE_AB', 'ARMARIO_3', usuario, e.""STATUS"", e.""SITUACAO"", e.""LOCAL"", current_timestamp FROM db.""ESTOQUE_AB"" e WHERE e.""SERIAL"" = '{serial}';
                                                                        INSERT INTO db.""ARMARIO_3""(""SERIAL"", ""STATUS"", ""SITUACAO"", ""LOCAL"")
                                                                        SELECT e.""SERIAL"", 'BRUTA', e.""SITUACAO"", 'D3'
                                                                        FROM db.""ESTOQUE_AB"" e
                                                                        WHERE e.""SERIAL"" = '{serial}';
                                                                        DELETE FROM db.""ESTOQUE_AB"" WHERE ""SERIAL"" = '{serial}';
                                                                    end $$;", conexao)
                                                                )
                            {
                                await comando.ExecuteNonQueryAsync();
                            }
                            break;
                        default: return StatusCode(404);
                    }
                }

                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erro ao mover o serial {serial} para a tabela {novaTabela}");
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
                                    SELECT e.""SERIAL"", 'ESTOQUE_AB', 'DEFEITOS', usuario, e.""STATUS"", e.""SITUACAO"", e.""LOCAL"", current_timestamp FROM db.""ESTOQUE_AB"" e
                                    WHERE e.""SERIAL"" = '{request.serial}';
                                    INSERT INTO db.""DEFEITOS""(""SERIAL"", ""CAIXA"", ""MOTIVO"", ""DATA"") values ('{request.serial}', '{request.caixa}', '{request.motivo}', current_timestamp);
	                                DELETE FROM db.""ESTOQUE_AB"" WHERE ""SERIAL"" = '{request.serial}';
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

                using (var comando = new NpgsqlCommand(@"SELECT
                                                        SUM(CASE WHEN m.""MODELO"" = 'D3 - PRO 1' THEN 1 ELSE 0 END) AS ""D3 - PRO 1"",
                                                        SUM(CASE WHEN m.""MODELO"" = 'D3 - PRO 2' THEN 1 ELSE 0 END) AS ""D3 - PRO 2"",
                                                        SUM(CASE WHEN m.""MODELO"" = 'D3 - SMART' THEN 1 ELSE 0 END) AS ""D3 - SMART"",
                                                        COUNT(a.""SERIAL"") AS Total
                                                     FROM
                                                        db.""ESTOQUE_AB"" a
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
                                d3Smart = leitor["D3 - SMART"].ToString(),
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
