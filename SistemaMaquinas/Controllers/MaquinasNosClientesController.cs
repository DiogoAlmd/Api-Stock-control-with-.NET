using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SistemaMaquinas.Classes;
using SistemaMaquinas.Models;
using Npgsql;
using Azure.Core;

namespace SistemaMaquinas.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class MaquinasNosClientesController : ControllerBase
    {
        private readonly ILogger<MaquinasNosClientesController> _logger;
        private readonly string _connectionString;

        public MaquinasNosClientesController(ILogger<MaquinasNosClientesController> logger)
        {
            var builder = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory()).AddJsonFile("appsettings.json", optional: false);
            IConfigurationRoot configuration = builder.Build();
            _connectionString = configuration.GetValue<string>("ConnectionStrings:SqlConnection");
            _logger = logger;
        }

        [HttpGet("[action]/{id}")]
        public async Task<IActionResult> ObterDados(string id)
        {
            var dados = new List<MaquinasNosClientes>();

            using (var conexao = new NpgsqlConnection(_connectionString))
            {
                await conexao.OpenAsync();

                using (var comando = new NpgsqlCommand($@"select mC.""SERIAL"", mC.""CNPF"", mC.""DATA"", mC.""EMPRESA"", s.""LOCAL"", m.""MODELO"" from db.""MaquinasNosClientes"" mC left join db.""Maquinas"" m on (mC.""SERIAL"" = m.""SERIAL"") left join db.""STORE"" s on (mC.""store"" = s.""IDSTORE"") where (mC.""store""={id} and {id} <> 1) or ({id}=1);", conexao))
                {
                    using (var leitor = await comando.ExecuteReaderAsync())
                    {
                        while (await leitor.ReadAsync())
                        {
                            dados.Add(new MaquinasNosClientes
                            {
                                Serial = leitor["SERIAL"].ToString(),
                                Modelo = leitor["MODELO"].ToString(),
                                CNPF = leitor["CNPF"].ToString(),
                                Data = leitor["DATA"].ToString(),
                                Empresa = leitor["EMPRESA"].ToString(),
                                Store = leitor["LOCAL"].ToString(),
                            });
                        }
                    }
                    return Ok(dados);
                }
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
                                                                nomeStore varchar(50);
                                                                lote character varying(100);
                                                            begin
                                                                lote:= 'MTZ - ' || TO_CHAR(current_timestamp, 'DD/MM/YYYY HH24:MI:SS');
                                                                SELECT s.""LOCAL"" INTO nomeStore FROM db.""MaquinasNosClientes"" m left join db.""STORE"" s on (m.""store""=s.""IDSTORE"") WHERE m.""SERIAL""='{request.Serial}';
	                                                            SELECT u.""idUsuario"" INTO usuario FROM db.users u WHERE u.""loginUsuario"" = '{request.Usuario}';
	                                                            INSERT INTO db.""Historico"" (""SERIAL"", ""ORIGEM"", ""DESTINO"", ""USUARIO"", ""EMPRESA"", ""DATA"", ""CNPF"", ""DataAlteracao"", ""LOCAL"")
                                                                SELECT m.""SERIAL"", 'MaquinasNosClientes', 'EMTRANSITO', usuario, m.""EMPRESA"", m.""DATA"", m.""CNPF"", current_timestamp, nomeStore FROM db.""MaquinasNosClientes"" m WHERE m.""SERIAL"" = '{request.Serial}';
                                                                INSERT INTO db.""EMTRANSITO""(""SERIAL"", ""DESTINO"", ""REMETENTE"", ""DATAENVIO"", ""OPERADORA"", ""TRANSPORTE"", ""LOTE"")
                                                                VALUES ('{request.Serial}', 1, usuario, current_timestamp, 'N/A', '{request.Transporte}', lote);                                                            
                                                                DELETE FROM db.""MaquinasNosClientes"" WHERE ""SERIAL"" = '{request.Serial}';
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


        [HttpPost("[action]")]
        public async Task<IActionResult> MoverParaStore(MoverParaStore request)
        {
            try
            {
                using (var conexao = new NpgsqlConnection(_connectionString))
                {
                    await conexao.OpenAsync();

                    using (var comando = new NpgsqlCommand($@"do $$
                                                        declare
	                                                        usuario int;
                                                            idStore int;
                                                            nomeStore varchar(50);
                                                        begin
                                                            SELECT s.""IDSTORE"" INTO idStore FROM db.""STORE"" s WHERE s.""LOCAL""='{request.Local}';
                                                            SELECT s.""LOCAL"" INTO nomeStore FROM db.""MaquinasNosClientes"" m left join db.""STORE"" s on (m.""store""=s.""IDSTORE"") WHERE m.""SERIAL""='{request.Serial}';
	                                                        SELECT u.""idUsuario"" INTO usuario FROM db.users u WHERE u.""loginUsuario"" = '{request.Usuario}';
                                                            INSERT INTO db.""Historico"" (""SERIAL"", ""ORIGEM"", ""DESTINO"", ""USUARIO"", ""EMPRESA"", ""DATA"", ""CNPF"", ""DataAlteracao"", ""LOCAL"")
                                                            SELECT m.""SERIAL"", 'MaquinasNosClientes', 'EstoqueEstrangeiro', usuario, m.""EMPRESA"", m.""DATA"", m.""CNPF"", current_timestamp, nomeStore FROM db.""MaquinasNosClientes"" m WHERE m.""SERIAL"" = '{request.Serial}';
                                                            INSERT INTO db.""EstoqueEstrangeiro"" (""SERIAL"", ""LOCAL"")
                                                            VALUES ('{request.Serial}',idStore);                                                            
                                                            DELETE FROM db.""MaquinasNosClientes"" WHERE ""SERIAL"" = '{request.Serial}';
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
                _logger.LogError(ex, $"Erro ao mover o serial {request.Serial} para a tabela Store");
                return StatusCode(500);
            }
        }

        [HttpPost("[action]")]
        public async Task<IActionResult> MoverParaStoreDefeito([FromBody] MoverParaStoreDefeito request)
        {
            try
            {
                using (var conexao = new NpgsqlConnection(_connectionString))
                {
                    await conexao.OpenAsync();

                    using (var comando = new NpgsqlCommand($@"do $$
                                                            declare
	                                                            usuario int;
                                                                idStore int;
								                                nomeStore varchar(50);
                                                            begin
								                                SELECT s.""LOCAL"" INTO nomeStore FROM db.""MaquinasNosClientes"" m left join db.""STORE"" s on (m.""store""=s.""IDSTORE"") WHERE m.""SERIAL""='{request.Serial}';
                                                                SELECT s.""IDSTORE"" INTO idStore FROM db.""STORE"" s WHERE s.""LOCAL""='{request.Local}';
	                                                            SELECT u.""idUsuario"" INTO usuario FROM db.users u WHERE u.""loginUsuario"" = '{request.Usuario}';
                                                                INSERT INTO db.""DefeitoExterior"" (""SERIAL"", ""LOCAL"") VALUES('{request.Serial}',idStore);
   	                                                            INSERT INTO db.""Historico"" (""SERIAL"", ""ORIGEM"", ""DESTINO"", ""USUARIO"", ""EMPRESA"", ""DATA"", ""CNPF"", ""DataAlteracao"", ""LOCAL"")
                                                                SELECT m.""SERIAL"", 'MaquinasNosClientes', 'DefeitoExterior', usuario, m.""EMPRESA"", m.""DATA"", m.""CNPF"", current_timestamp, nomeStore FROM db.""MaquinasNosClientes"" m WHERE m.""SERIAL"" = '{request.Serial}';
   	                                                            DELETE FROM db.""MaquinasNosClientes"" WHERE ""SERIAL"" = '{request.Serial}';
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

        [HttpGet("[action]/{dataInicial}/{dataFinal}/{id}")]
        public async Task<IActionResult> Modelos(string dataInicial, string dataFinal, string id)
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
                                                        SUM(CASE WHEN m.""MODELO"" = 'D3 - PROFIT' THEN 1 ELSE 0 END) AS ""D3 - PROFIT"",
                                                        COUNT(a.""SERIAL"") AS ""Total""
                                                    FROM
                                                        db.""MaquinasNosClientes"" a
                                                        left JOIN db.""Maquinas"" m ON a.""SERIAL"" = m.""SERIAL""
                                                    WHERE
                                                        (a.""DATA"" BETWEEN TIMESTAMP '{dataInicial}' AND TIMESTAMP '{dataFinal}') AND ((a.""store""={id} and {id} <> 1) or ({id}=1));", conexao))
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
                                d3FIT= leitor["D3 - PROFIT"].ToString(),
                                Total = leitor["Total"].ToString()
                            });
                        }
                    }
                    return Ok(modelos);
                }
            }
        }

        [HttpGet("[action]/{id}")]
        public async Task<IActionResult> ModeloTotal(string id)
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
                                                          SUM(CASE WHEN m.""MODELO"" = 'D3 - PROFIT' THEN 1 ELSE 0 END) AS ""D3 - PROFIT"",
                                                          COUNT(a.""SERIAL"") AS Total
                                                      FROM
                                                          db.""MaquinasNosClientes"" a
                                                          LEFT JOIN db.""Maquinas"" m ON a.""SERIAL"" = m.""SERIAL""
                                                          where (a.""store""={id} and {id} <> 1) or ({id}=1);", conexao))
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
                                d3FIT = leitor["D3 - PROFIT"].ToString(),
                                Total = leitor["Total"].ToString()
                            });
                        }
                    }
                    return Ok(modelos);
                }
            }
        }

        [HttpPost("[action]/{serial}/{cnpf}/{nome}/{usuario}/{store}")]
        public async Task<IActionResult> MigracaoCadastro(string serial, string cnpf, string nome,string usuario,string store)
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
                                                                idStore int;
                                                            begin
                                                                SELECT s.""IDSTORE"" INTO idStore FROM db.""STORE"" s WHERE s.""LOCAL""='{store}';
                                                                SELECT s.""LOCAL"" INTO nomeStore FROM db.""MaquinasNosClientes"" m left join db.""STORE"" s on (m.""store""=s.""IDSTORE"") WHERE m.""SERIAL""='{serial}';
	                                                            SELECT u.""idUsuario"" INTO usuario FROM db.users u WHERE u.""loginUsuario"" = '{usuario}';
	                                                            INSERT INTO db.""Historico"" (""SERIAL"", ""ORIGEM"", ""DESTINO"", ""USUARIO"", ""EMPRESA"", ""DATA"", ""CNPF"", ""DataAlteracao"", ""LOCAL"")
                                                                SELECT m.""SERIAL"", 'MaquinasNosClientes', 'MaquinasNosClientes', usuario, m.""EMPRESA"", m.""DATA"", m.""CNPF"", current_timestamp, nomeStore FROM db.""MaquinasNosClientes"" m WHERE m.""SERIAL"" = '{serial}';
                                                                update db.""MaquinasNosClientes"" set ""CNPF""='{cnpf}', ""EMPRESA""='{nome}', ""store""=idStore, ""DATA""=current_timestamp where ""SERIAL""='{serial}';
                                                            end $$;", conexao))
                    {
                        await comando.ExecuteNonQueryAsync();
                    }
                }

                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erro ao alterar o serial {serial}");
                return StatusCode(500);
            }
        }

        [HttpPost("[action]/{serial}/{usuario}")]
        public async Task<IActionResult> MoverParaArmario2(string serial, string usuario)
        {
            try
            {
                using (var conexao = new NpgsqlConnection(_connectionString))
                {
                    await conexao.OpenAsync();

                    using (var comando = new NpgsqlCommand($@"select db.spmovelotenoclientearmario2('{serial}', '{usuario}');", conexao)
                                                        )
                    {
                        string resultado = await comando.ExecuteScalarAsync() as string;

                        // Retorne o valor como resposta
                        return Ok(new {res=resultado});
                    }
                }
            }
            catch (NpgsqlException ex)
            {
                var errorMessage = ex.Message;

                if (errorMessage.Contains("O serial"))
                {
                    _logger.LogError(ex, $"Erro personalizado: {errorMessage}");
                    return StatusCode(409, new { Message = errorMessage });
                }
                else if(errorMessage.Contains("expressão FOREACH"))
                {
                    _logger.LogError(ex, $"Erro personalizado: {errorMessage}");
                    return StatusCode(422, new { Message = errorMessage });
                }
                _logger.LogError(ex, $"Erro ao mover para MaquinasNoCliente: {errorMessage}");
                return StatusCode(500, new { Message = errorMessage });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erro ao mover o serial para a tabela Armario2");
                return StatusCode(500);
            }
        }
    }
}
