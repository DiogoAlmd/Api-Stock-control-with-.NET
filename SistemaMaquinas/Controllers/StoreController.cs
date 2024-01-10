using Microsoft.AspNetCore.Mvc;
using SistemaMaquinas.Models;
using SistemaMaquinas.Classes;
using Npgsql;
using Microsoft.AspNetCore.Authorization;

namespace SistemaMaquinas.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class StoreController : ControllerBase
    {
        private readonly ILogger<StoreController> _logger;
        private readonly string _connectionString;


        public StoreController(ILogger<StoreController> logger)
        {
            var builder = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory()).AddJsonFile("appsettings.json", optional: false);
            IConfigurationRoot configuration = builder.Build();
            _connectionString = configuration.GetValue<string>("ConnectionStrings:SqlConnection");
            _logger = logger;
        }


        [HttpGet("[action]/{id}")]
        public async Task<IActionResult> ObterDados(string id)
        {
            var dados = new List<EstoqueExterior>();
            using (var conexao = new NpgsqlConnection(_connectionString))
            {
                await conexao.OpenAsync();

                using (var comando = new NpgsqlCommand($@"select eE.""SERIAL"",s.""LOCAL"", m.""MODELO"" from db.""EstoqueEstrangeiro"" eE left join db.""Maquinas"" m on (eE.""SERIAL"" = m.""SERIAL"") left join db.""STORE"" s on (eE.""LOCAL""=s.""IDSTORE"") where (eE.""LOCAL""={id} and {id} <> 1) or ({id}=1);", conexao))
                {
                    using (var leitor = await comando.ExecuteReaderAsync())
                    {
                        while (await leitor.ReadAsync())
                        {
                            dados.Add(new EstoqueExterior
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
        public async Task<IActionResult> MoverParaDefeitoExterior([FromBody] MoverParaStoreDefeito request)
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
	                                                            INSERT INTO db.""DefeitoExterior""(""SERIAL"", ""LOCAL"")
                                                                SELECT eE.""SERIAL"", eE.""LOCAL"" FROM db.""EstoqueEstrangeiro"" eE WHERE eE.""SERIAL"" = '{request.Serial}';
   	                                                            INSERT INTO db.""Historico"" (""SERIAL"", ""ORIGEM"", ""DESTINO"", ""USUARIO"",""LOCAL"", ""DataAlteracao"")
                                                                SELECT ee.""SERIAL"", 'EstoqueEstrangeiro', 'DefeitoExterior', usuario, s.""LOCAL"", current_timestamp FROM db.""EstoqueEstrangeiro"" ee left join db.""STORE"" s on (ee.""LOCAL""=s.""IDSTORE"") 
                                                                WHERE ee.""SERIAL"" = '{request.Serial}';
   	                                                            DELETE FROM db.""EstoqueEstrangeiro"" WHERE ""SERIAL"" = '{request.Serial}';
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
                _logger.LogError(ex, $"Erro ao mover o serial {request.Serial} para a tabela Defeito Exterior");
                return StatusCode(500);
            }
        }

        [HttpPost]
        public async Task<IActionResult> MoverParaCliente([FromBody] MoverParaCliente request)
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
	                                                        INSERT INTO db.""MaquinasNosClientes""(""SERIAL"", ""CNPF"", ""EMPRESA"", ""store"", ""DATA"")
                                                            SELECT '{request.serial}', '{request.CNPF}', '{request.empresa}', eE.""LOCAL"", current_timestamp FROM db.""EstoqueEstrangeiro"" eE WHERE eE.""SERIAL"" = '{request.serial}';
   	                                                        INSERT INTO db.""Historico""(""SERIAL"", ""ORIGEM"", ""DESTINO"", ""USUARIO"", ""LOCAL"", ""DataAlteracao"")
                                                            SELECT eE.""SERIAL"", 'EstoqueEstrangeiro', 'MaquinasNosClientes', usuario, s.""LOCAL"", current_timestamp FROM db.""EstoqueEstrangeiro"" eE left join db.""STORE"" s on (eE.""LOCAL""=s.""IDSTORE"")
                                                            WHERE eE.""SERIAL"" = '{request.serial}';
   	                                                        DELETE FROM db.""EstoqueEstrangeiro"" WHERE ""SERIAL"" = '{request.serial}';
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
                _logger.LogError(ex, $"Erro ao mover o serial {request.serial} para a tabela MaquinasNoCliente");
                return StatusCode(500);
            }
        }

        [HttpPost("[action]")]
        public async Task<IActionResult> MoverParaEmTransito([FromBody] MoverParaEmTransito request)
        {
            try
            {
                using (var conexao = new NpgsqlConnection(_connectionString))
                {
                    await conexao.OpenAsync();

                    using (var comando = new NpgsqlCommand($@"do $$
                                                        declare
	                                                        usuario int;
                                                            lote character varying(100);
                                                        begin
	                                                        SELECT u.""idUsuario"" INTO usuario FROM db.users u WHERE u.""loginUsuario"" = '{request.Usuario}';
                                                            lote:= 'MTZ - ' || TO_CHAR(current_timestamp, 'DD/MM/YYYY HH24:MI:SS');
	                                                        INSERT INTO db.""Historico""(""SERIAL"", ""ORIGEM"", ""DESTINO"", ""USUARIO"", ""LOCAL"", ""DataAlteracao"")
                                                            SELECT eE.""SERIAL"", 'EstoqueEstrangeiro', 'EMTRANSITO', usuario, s.""LOCAL"",current_timestamp FROM db.""EstoqueEstrangeiro"" eE left join db.""STORE"" s on (eE.""LOCAL""=s.""IDSTORE"")
                                                            WHERE eE.""SERIAL"" = '{request.Serial}';
   	                                                        INSERT INTO db.""EMTRANSITO""(""SERIAL"", ""DESTINO"", ""REMETENTE"", ""DATAENVIO"", ""OPERADORA"", ""TRANSPORTE"",""LOTE"")
                                                            VALUES ('{request.Serial}', 1, usuario, current_TIMESTAMP, 'N/A', '{request.Transporte}',lote);
   	                                                        DELETE FROM db.""EstoqueEstrangeiro"" WHERE ""SERIAL"" = '{request.Serial}';
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
                _logger.LogError(ex, $"Erro ao mover o serial {request.Serial} para a tabela EMTRANSITO");
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
                                                       FROM db.""EstoqueEstrangeiro""", conexao))
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

                using (var comando = new NpgsqlCommand($@" SELECT
                                                         SUM(CASE WHEN m.""MODELO"" = 'D3 - PRO 1' THEN 1 ELSE 0 END) AS ""D3 - PRO 1"",
                                                         SUM(CASE WHEN m.""MODELO"" = 'D3 - PRO 2' THEN 1 ELSE 0 END) AS ""D3 - PRO 2"",
                                                         SUM(CASE WHEN m.""MODELO"" = 'D3 - PRO REFURBISHED' THEN 1 ELSE 0 END) AS ""D3 - PRO REFURBISHED"",
                                                         SUM(CASE WHEN m.""MODELO"" = 'D3 - SMART' THEN 1 ELSE 0 END) AS ""D3 - SMART"",
                                                         SUM(CASE WHEN m.""MODELO"" = 'D3 - X' THEN 1 ELSE 0 END) AS ""D3 - X"",
                                                         COUNT(a.""SERIAL"") AS Total
                                                       FROM
                                                         db.""EstoqueEstrangeiro"" a
                                                         LEFT JOIN db.""Maquinas"" m ON a.""SERIAL"" = m.""SERIAL"" WHERE (a.""LOCAL""={id} and {id}<>1) or ({id}=1);", conexao))
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
