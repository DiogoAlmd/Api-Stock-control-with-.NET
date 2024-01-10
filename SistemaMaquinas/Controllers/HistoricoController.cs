using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SistemaMaquinas.Models;
using Npgsql;

namespace SistemaMaquinas.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class HistoricoController : ControllerBase
    {
        private readonly ILogger<HistoricoController> _logger;
        private readonly string _connectionString;

        public HistoricoController(ILogger<HistoricoController> logger)
        {
            var builder = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory()).AddJsonFile("appsettings.json", optional: false);
            IConfigurationRoot configuration = builder.Build();
            _connectionString = configuration.GetValue<string>("ConnectionStrings:SqlConnection");
            _logger = logger;
        }



        [HttpGet("[action]/{role}/{user}")]
        public async Task<IActionResult> ObterDados(string role, string user)
        {
            var dados = new List<Historico>();

            using (var conexao = new NpgsqlConnection(_connectionString))
            {
                await conexao.OpenAsync();

                using (var comando = new NpgsqlCommand($@"SELECT h.""id"", h.""SERIAL"", h.""ORIGEM"", h.""DESTINO"", h.""STATUS"",
                                                    h.""SITUACAO"", h.""LOCAL"", h.""OPERADORA"", h.""DataRetirada"", h.""MaquinaPropriaDoCliente"",
                                                    h.""CAIXA"", h.""DATA"", h.""CNPF"", h.""DataAlteracao"", h.""MOTIVO"", h.""EMPRESA"", u.""loginUsuario"" AS ""USUARIO""
                                                    FROM db.""Historico"" h
                                                    LEFT JOIN db.users u ON h.""USUARIO"" = u.""idUsuario""
                                                    WHERE (CASE WHEN '{role}' = 'CONSULTOR' THEN u.""loginUsuario"" = '{user}' ELSE TRUE END);", conexao))
                {
                    using (var leitor = await comando.ExecuteReaderAsync())
                    {
                        while (await leitor.ReadAsync())
                        {
                            dados.Add(new Historico
                            {
                                Id = leitor["id"].ToString(),
                                Serial = leitor["SERIAL"].ToString(),
                                Origem = leitor["ORIGEM"].ToString(),
                                Destino= leitor["DESTINO"].ToString(),
                                Usuario = leitor["USUARIO"].ToString(),
                                Status = leitor["STATUS"].ToString(),
                                Situacao = leitor["SITUACAO"].ToString(),
                                Local = leitor["LOCAL"].ToString(),
                                Operadora = leitor["OPERADORA"].ToString(),
                                DataRetirada = leitor["DataRetirada"].ToString().Replace("00:00:00", ""),
                                MaquinaPropriaDoCliente = leitor["MaquinaPropriaDoCliente"].ToString(),
                                Caixa = leitor["CAIXA"].ToString(),
                                Motivo = leitor["MOTIVO"].ToString(),
                                Data = leitor["DATA"].ToString(),
                                CNPF = leitor["CNPF"].ToString(),
                                Empresa = leitor["EMPRESA"].ToString(),
                                DataAlteracao = leitor["DataAlteracao"].ToString()
                            });
                        }
                    }
                    return Ok(dados);
                }
            }
        }


        [HttpPost("[action]/{id}/{serial}/{origem}/{destino}")]
        public async Task<IActionResult> desfazer(int id, string serial, string origem, string destino)
        {
            try
            {
                using (var conexao = new NpgsqlConnection(_connectionString))
                {
                    await conexao.OpenAsync();

                    switch (origem)
                    {
                        case "ARMARIO_1":
                            if(destino == "ARMARIO_1")
                            {
                                using (var comando = new NpgsqlCommand($@"do $$
                                                                    begin	
                                                                       DELETE FROM db.""ARMARIO_1"" WHERE ""SERIAL"" = '{serial}';
                                                                       INSERT INTO db.""ARMARIO_1"" (""SERIAL"", ""STATUS"", ""SITUACAO"", ""LOCAL"", ""OPERADORA"", ""MaquinaPropriaDoCliente"")
                                                                       SELECT h.""SERIAL"", h.""STATUS"", h.""SITUACAO"", h.""LOCAL"", h.""OPERADORA"", h.""MaquinaPropriaDoCliente"" FROM db.""Historico"" h WHERE h.""id"" = '{id}' and h.""SERIAL"" = '{serial}';
                                                                       DELETE FROM db.""Historico"" WHERE ""SERIAL"" = '{serial}';
                                                                    end $$;", conexao)
                                                                    )
                                {
                                    await comando.ExecuteNonQueryAsync();
                                }
                                break;
                            }
                            else if(destino == "EMTRANSITO")
                            {
                                using (var comando = new NpgsqlCommand($@"do $$
                                                                        declare
	                                                                        IDSOLICITACAO INT;
                                                                        begin	
                                                                           DELETE FROM db.""EMTRANSITO"" WHERE ""SERIAL"" = '{serial}';
                                                                           INSERT INTO db.""ARMARIO_1""(""SERIAL"", ""STATUS"", ""SITUACAO"", ""LOCAL"", ""OPERADORA"", ""MaquinaPropriaDoCliente"")
                                                                           SELECT h.""SERIAL"", h.""STATUS"", h.""SITUACAO"", h.""LOCAL"", h.""OPERADORA"", h.""MaquinaPropriaDoCliente"" FROM db.""Historico"" h WHERE h.""id"" = '{id}' and h.""SERIAL"" = '{serial}';
                                                                           select h.""IDSOLICITACAO"" into IDSOLICITACAO from db.""Historico"" h where h.""SERIAL""='{serial}';
                                                                           UPDATE db.""SOLICITACAO"" SET ""ENVIADAS""=""ENVIADAS""-1 WHERE ""IDSOLICITACAO""=IDSOLICITACAO;
                                                                           DELETE FROM db.""Historico"" WHERE ""SERIAL"" = '{serial}';
                                                                        end $$;", conexao)
                                                                    )
                                {
                                    await comando.ExecuteNonQueryAsync();
                                }
                                break;
                            }
                            else
                            {
                                using (var comando = new NpgsqlCommand($@"do $$
                                                                        begin	
                                                                            DELETE FROM db.""ARMARIO_1"" WHERE ""SERIAL"" = '{serial}';
                                                                            INSERT INTO db.""ARMARIO_1""(""SERIAL"", ""STATUS"", ""SITUACAO"", ""LOCAL"", ""OPERADORA"", ""MaquinaPropriaDoCliente"")
                                                                            SELECT h.""SERIAL"", h.""STATUS"", h.""SITUACAO"", h.""LOCAL"", h.""OPERADORA"", h.""MaquinaPropriaDoCliente"" FROM db.""Historico"" h  WHERE h.""id"" = '{id}';
                                                                            DELETE FROM db.""{destino}"" WHERE ""SERIAL"" = '{serial}';
                                                                            DELETE FROM db.""Historico"" WHERE ""SERIAL"" = '{serial}';
                                                                        end $$;", conexao)
                                                                    )
                                {
                                    await comando.ExecuteNonQueryAsync();
                                }
                                break;
                            }

                        case "ARMARIO_2":
                            using (var comando = new NpgsqlCommand($@"do $$
                                                                    begin	
                                                                       INSERT INTO db.""ARMARIO_2""(""SERIAL"", ""STATUS"", ""SITUACAO"", ""LOCAL"")
                                                                       SELECT h.""SERIAL"", h.""STATUS"", h.""SITUACAO"", h.""LOCAL"" from db.""Historico"" h  WHERE h.""id"" = '{id}';
                                                                       DELETE FROM db.""{destino}"" WHERE ""SERIAL"" = '{serial}';
                                                                       DELETE FROM db.""Historico"" WHERE ""SERIAL"" = '{serial}';
                                                                    end $$;", conexao)
                                                                )
                            {
                                await comando.ExecuteNonQueryAsync();
                            }
                            break;
                        case "ARMARIO_3":
                            using (var comando = new NpgsqlCommand($@"do $$
                                                                    begin	
                                                                       INSERT INTO db.""ARMARIO_3""(""SERIAL"", ""STATUS"", ""SITUACAO"", ""LOCAL"")
                                                                       SELECT h.""SERIAL"", h.""STATUS"", h.""SITUACAO"", h.""LOCAL"" from db.""Historico"" h  WHERE h.""id"" = '{id}';
                                                                       DELETE FROM db.""{destino}"" WHERE ""SERIAL"" = '{serial}';
                                                                       DELETE FROM db.""Historico"" WHERE ""SERIAL"" = '{serial}';
                                                                    end $$;", conexao)
                                                                )
                            {
                                await comando.ExecuteNonQueryAsync();
                            }
                            break;
                        case "ESTOQUE_AB":
                            using (var comando = new NpgsqlCommand($@"do $$
                                                                    begin	
                                                                       INSERT INTO db.""ESTOQUE_AB""(""SERIAL"", ""STATUS"", ""SITUACAO"", ""LOCAL"")
                                                                       SELECT h.""SERIAL"", h.""STATUS"", h.""SITUACAO"", h.""LOCAL"" from db.""Historico"" h  WHERE h.""id"" = '{id}';
                                                                       DELETE FROM db.""{destino}"" WHERE ""SERIAL"" = '{serial}';
                                                                       DELETE FROM db.""Historico"" WHERE ""SERIAL"" = '{serial}';
                                                                    end $$;", conexao)
                                                                )
                            {
                                await comando.ExecuteNonQueryAsync();
                            }
                            break;
                        case "MaquinasNosClientes":
                            using (var comando = new NpgsqlCommand($@"do $$
                                                                    declare
                                                                        nomeStore varchar(50);
                                                                        idStore int;
                                                                    begin
                                                                       SELECT h.""LOCAL"" INTO nomeStore FROM db.""Historico"" h WHERE h.""id""='{id}';
                                                                       SELECT s.""IDSTORE"" INTO idStore FROM db.""STORE"" s WHERE s.""LOCAL""=nomeStore;
                                                                       DELETE FROM db.""{destino}"" WHERE ""SERIAL"" = '{serial}';
                                                                       INSERT INTO db.""MaquinasNosClientes"" (""SERIAL"", ""CNPF"", ""DATA"", ""EMPRESA"", ""store"")
                                                                       SELECT h.""SERIAL"", h.""CNPF"", h.""DATA"", h.""EMPRESA"", idstore FROM db.""Historico"" h  WHERE h.""id"" = '{id}';
                                                                       DELETE FROM db.""Historico"" WHERE ""SERIAL"" = '{serial}';
                                                                    end $$;", conexao)
                                                                )
                            {
                                await comando.ExecuteNonQueryAsync();
                            }
                            break;
                        case "DEFEITOS":
                            using (var comando = new NpgsqlCommand($@"do $$
                                                                    begin	
                                                                       DELETE FROM db.""{destino}"" WHERE ""SERIAL"" = '{serial}';
                                                                       INSERT INTO db.""DEFEITOS""(""SERIAL"", ""CAIXA"", ""DATA"", ""MOTIVO"")
                                                                       SELECT h.""SERIAL"", h.""CAIXA"", h.""DATA"", h.""MOTIVO"" FROM db.""Historico"" h WHERE h.""SERIAL"" = '{serial}';
                                                                       DELETE FROM db.""Historico"" WHERE ""SERIAL"" = '{serial}';
                                                                    end $$;", conexao)
                                                                )
                            {
                                await comando.ExecuteNonQueryAsync();
                            }
                            break;
                        case "EstoqueEstrangeiro":
                            using (var comando = new NpgsqlCommand($@"do $$
                                                                    declare
	                                                                    IDSTORE int;
                                                                    begin	
	                                                                    select s.""IDSTORE"" into IDSTORE from db.""STORE"" s right join db.""Historico"" h on h.""LOCAL""=s.""LOCAL"" WHERE h.""SERIAL""='{serial}';
	                                                                    DELETE FROM db.""{destino}"" WHERE ""SERIAL"" = '{serial}';
                                                                        INSERT INTO db.""EstoqueEstrangeiro""(""SERIAL"", ""LOCAL"") values('{serial}',IDSTORE);
                                                                        DELETE FROM db.""Historico"" WHERE ""SERIAL"" = '{serial}';
                                                                    end $$;", conexao)
                                                                )
                            {
                                await comando.ExecuteNonQueryAsync();
                            }
                            break;
                        case "DefeitoExterior":
                            using (var comando = new NpgsqlCommand($@"do $$
                                                                    declare
	                                                                    IDSTORE int;
                                                                    begin	
	                                                                    select s.""IDSTORE"" into IDSTORE from db.""STORE"" s right join db.""Historico"" h on h.""LOCAL""=s.""LOCAL"" WHERE h.""SERIAL""='{serial}';
	                                                                    DELETE FROM db.""{destino}"" WHERE ""SERIAL"" = '{serial}';
                                                                        INSERT INTO db.""DefeitoExterior""(""SERIAL"", ""LOCAL"") values('{serial}',IDSTORE);
                                                                        DELETE FROM db.""Historico"" WHERE ""SERIAL"" = '{serial}';
                                                                    end $$;", conexao)
                                                                )
                            {
                                await comando.ExecuteNonQueryAsync();
                            }
                            break;
                        case "DEVOLUCAO":
                            using (var comando = new NpgsqlCommand($@"do $$
                                                                    begin	
	                                                                    INSERT INTO db.""DEVOLUCAO""(""SERIAL"", ""CAIXA"", ""DATA"")
                                                                        SELECT h.""SERIAL"", h.""CAIXA"", h.""DATA"" FROM db.""Historico"" h WHERE h.""id"" = '{id}';
                                                                        DELETE FROM db.""{destino}"" WHERE ""SERIAL"" = '{serial}';
                                                                        DELETE FROM db.""Historico"" h WHERE ""SERIAL"" = '{serial}';
                                                                    end $$;", conexao)
                                                                )
                            {
                                await comando.ExecuteNonQueryAsync();
                            }
                            break;
                        case "EMTRANSITO":
                            if (destino == "EMTRANSITO")
                            {
                                using (var comando = new NpgsqlCommand($@"do $$
                                                                        declare
	                                                                        TRANSPORTE VARCHAR(100);
                                                                        begin	
	                                                                        select h.""TRANSPORTE"" into TRANSPORTE from db.""Historico"" h where h.""SERIAL""='{serial}';
	                                                                        UPDATE db.""EMTRANSITO"" SET ""TRANSPORTE""=TRANSPORTE WHERE ""SERIAL""='{serial}';
                                                                            DELETE from db.""Historico"" WHERE ""id"" = '{id}';
                                                                        end $$;", conexao)
                                                                    )
                                {
                                    await comando.ExecuteNonQueryAsync();
                                }
                                break;
                            }
                            else {
                                using (var comando = new NpgsqlCommand($@"do $$
                                                                        declare
	                                                                        IDSTORE int;
	                                                                        IDSOLICITACAO int;
	                                                                        RECEBIDAS int;
	                                                                        QTD int;
                                                                        begin	
	                                                                        SELECT s.""IDSTORE"" into IDSTORE FROM db.""STORE"" s right join db.""Historico"" h on h.""LOCAL""=s.""LOCAL"" WHERE h.""SERIAL""='{serial}';
	                                                                        SELECT h.""IDSOLICITACAO"" into IDSOLICITACAO FROM db.""Historico"" h WHERE h.""SERIAL"" = '{serial}';
	                                                                        INSERT INTO db.""EMTRANSITO""(""SERIAL"", ""OPERADORA"", ""DESTINO"", ""REMETENTE"", ""DATAENVIO"", ""TRANSPORTE"", ""LOTE"")
                                                                            SELECT h.""SERIAL"", h.""OPERADORA"", IDSTORE, h.""USUARIO"", h.""DataAlteracao"", h.""TRANSPORTE"", h.""LOTE"" FROM db.""Historico"" h WHERE h.""id"" = '{id}';
   	                                                                        DELETE FROM db.""{destino}"" WHERE ""SERIAL"" = '{serial}';
   	                                                                        IF IDSOLICITACAO IS NOT null then
   		                                                                          UPDATE db.""SOLICITACAO"" SET ""RECEBIDAS""=""RECEBIDAS""-1 WHERE ""IDSOLICITACAO""=IDSOLICITACAO;
	                                                                              SELECT s.""RECEBIDAS"" into RECEBIDAS FROM db.""SOLICITACAO"" s WHERE s.""IDSOLICITACAO""=IDSOLICITACAO;
	                                                                              SELECT s.""QTD"" into QTD FROM db.""SOLICITACAO"" s WHERE s.""IDSOLICITACAO""=IDSOLICITACAO;
	                                                                              IF QTD <> RECEBIDAS then
	                                                                               UPDATE db.""SOLICITACAO"" SET ""FINALIZADA"" = false WHERE ""IDSOLICITACAO""=IDSOLICITACAO;
	                                                                              end if;
   	                                                                        end if;
                                                                            DELETE FROM db.""Historico"" WHERE ""SERIAL"" = '{serial}';
                                                                        end $$;", conexao)
                                                                    )
                                {
                                    await comando.ExecuteNonQueryAsync();
                                }
                                break; 
                            }
                        default: return StatusCode(404);
                    }
                }
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erro ao mover o serial {serial} para {origem}");
                return StatusCode(500);
            }
        }
    }
}
