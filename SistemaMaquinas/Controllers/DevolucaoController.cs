using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SistemaMaquinas.Models;
using Npgsql;

namespace SistemaMaquinas.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class DevolucaoController : ControllerBase
    {
        private readonly ILogger<DevolucaoController> _logger;
        private readonly string _connectionString;

        public DevolucaoController(ILogger<DevolucaoController> logger)
        {
            var builder = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory()).AddJsonFile("appsettings.json", optional: false);
            IConfigurationRoot configuration = builder.Build();
            _connectionString = configuration.GetValue<string>("ConnectionStrings:SqlConnection");
            _logger = logger;
        }


        [HttpGet]
        public async Task<IActionResult> ObterDados()
        {
            var dados = new List<Devolucao>();

            using (var conexao = new NpgsqlConnection(_connectionString))
            {
                await conexao.OpenAsync();

                using (var comando = new NpgsqlCommand(@"select d.*, m.""MODELO"" from db.""DEVOLUCAO"" d left join db.""Maquinas"" m on (d.""SERIAL"" = m.""SERIAL"")", conexao))
                {
                    using (var leitor = await comando.ExecuteReaderAsync())
                    {
                        while (await leitor.ReadAsync())
                        {
                            dados.Add(new Devolucao
                            {
                                Serial = leitor["SERIAL"].ToString(),
                                Modelo = leitor["MODELO"].ToString(),
                                Caixa = leitor["CAIXA"].ToString(),
                                Data = leitor["DATA"].ToString()
                            });
                        }
                    }
                    return Ok(dados);
                }
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
                                                         SUM(CASE WHEN m.""MODELO"" = 'D3 - PRO REFURBISHED' THEN 1 ELSE 0 END) AS ""D3 - PRO REFURBISHED"",
                                                         SUM(CASE WHEN m.""MODELO"" = 'D3 - SMART' THEN 1 ELSE 0 END) AS ""D3 - SMART"",
                                                         SUM(CASE WHEN m.""MODELO"" = 'D3 - X' THEN 1 ELSE 0 END) AS ""D3 - X"",
                                                         COUNT(a.""SERIAL"") AS Total
                                                       FROM
                                                         db.""DEVOLUCAO"" a
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
