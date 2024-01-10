using SistemaMaquinas.Classes;
using Microsoft.Data.SqlClient;
using Npgsql;

namespace SistemaMaquinas.Repositories
{
    public class DefeitosRepository
    {
        private readonly string _connectionString;
        private readonly ILogger _logger;
        private readonly string _sqlQuery;

        public DefeitosRepository(string connectionString, ILogger logger, string sqlQuery)
        {
            _connectionString = connectionString;
            _logger = logger;
            _sqlQuery = sqlQuery;
        }

        public async Task MoverParaDefeito(MoverParaDefeito request)
        {
            try
            {
                using (var conexao = new NpgsqlConnection(_connectionString))
                {
                    await conexao.OpenAsync();

                    using (var comando = new NpgsqlCommand(_sqlQuery, conexao))
                    {
                        await comando.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erro ao mover o serial {request.serial} para a tabela DEFEITOS");
                throw;
            }
        }
    }
}
