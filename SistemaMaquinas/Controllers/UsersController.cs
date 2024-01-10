using Microsoft.AspNetCore.Mvc;
using SistemaMaquinas.Models;
using Microsoft.AspNetCore.Authorization;
using SistemaMaquinas.Services;
using Npgsql;

namespace SistemaMaquinas.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        private readonly ILogger<UsersController> _logger;
        private readonly string _connectionString;

        public UsersController(ILogger<UsersController> logger)
        {
            var builder = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory()).AddJsonFile("appsettings.json", optional: false);
            IConfigurationRoot configuration = builder.Build();
            _connectionString = configuration.GetValue<string>("ConnectionStrings:SqlConnection");
            _logger = logger;
        }

        [HttpPost]
        [Route("login")]
        [AllowAnonymous]
        public async Task<ActionResult<dynamic>> Authenticate([FromBody] Users model)
        {
            var dados = new List<Users>();
            using (var conexao = new NpgsqlConnection(_connectionString))
            {
                await conexao.OpenAsync();

                using (var comando = new NpgsqlCommand("SELECT * FROM db.users", conexao))
                {
                    using (var leitor = await comando.ExecuteReaderAsync())
                    {
                        while (await leitor.ReadAsync())
                        {
                            dados.Add(new Users
                            {
                                Login = leitor["loginUsuario"].ToString(),
                                Senha = leitor["senhaUsuario"].ToString(),
                                Funcao = leitor["funcao"].ToString(),
                                Store = leitor["store"].ToString()
                            });
                        }
                    }
                }

                var user = dados.FirstOrDefault(u => u.Login == model.Login && u.Senha == model.Senha);
                if (user != null)
                {
                    var token = CriaToken.GenerateToken(user);

                    //return Ok(new { token });

                    user.Senha = "";
                    return new
                    {
                        token = token
                    };
                }
                else
                {
                    return NotFound(new { message = "Usuário ou senha inválidos" });
                }
            }
        }

        [HttpGet]
        [Route("authenticated")]
        [Authorize]
        public async Task<IActionResult> verificaToken()
        {
            return Ok();
        }
    }
}
