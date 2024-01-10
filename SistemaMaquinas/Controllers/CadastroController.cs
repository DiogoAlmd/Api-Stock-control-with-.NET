using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace SistemaMaquinas.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    //[Authorize]
    public class CadastroController : ControllerBase
    {
        private readonly ILogger<CadastroController> _logger;
        private readonly string _connectionString;


        public CadastroController(ILogger<CadastroController> logger)
        {
            var builder = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory()).AddJsonFile("appsettings.json", optional: false);
            IConfigurationRoot configuration = builder.Build();
            _connectionString = configuration.GetValue<string>("ConnectionStrings:SqlConnection");
            _logger = logger;
        }

        [HttpPost("[action]/{serial}/{modelo}/{status}/{situacao}/{local}/{operadora}/{armario}")]
        public async Task<IActionResult> Cadastro(string serial, string modelo, string status, string situacao, string local, string operadora, string armario)
        {
            try
            {
                using (var conexao = new SqlConnection(_connectionString))
                {
                    await conexao.OpenAsync();

                    using (var comando = new SqlCommand($@"INSERT INTO DEFEITOS(SERIAL, MODELO, CAIXA, DATA)
                                                          SELECT SERIAL, MODELO, GETDATE() FROM ARMARIO_1 WHERE SERIAL = '{serial}'
                                                          DELETE FROM ARMARIO_1 WHERE SERIAL = '{serial}';", conexao)
                                                        )
                    {
                        await comando.ExecuteNonQueryAsync();
                    }
                }

                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erro ao mover o serial {serial} para a tabela DEFEITOS");
                return StatusCode(500);
            }
        }
    }
}
