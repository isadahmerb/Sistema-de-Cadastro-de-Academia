using System.Net;
using System.Net.Http.Json;

namespace backend.Tests;

public class ClientesTests : IClassFixture<ApiFactory>
{
    private readonly HttpClient _http;

    public ClientesTests(ApiFactory factory) => _http = factory.CreateClient();

    private static object NovoCliente(string cpf, string nome = "Ana") =>
        new { name = nome, cpf, telefone = "48999999999", address = "Rua A, 1" };

    [Fact]
    public async Task Cadastrar_cliente_retorna_201()
    {
        var resp = await _http.PostAsJsonAsync("/clientes", NovoCliente("10000000001"));

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
    }

    [Fact]
    public async Task Cadastrar_cpf_repetido_retorna_409()
    {
        await _http.PostAsJsonAsync("/clientes", NovoCliente("10000000002"));

        var resp = await _http.PostAsJsonAsync("/clientes", NovoCliente("10000000002", "Outro"));

        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
    }

    [Fact]
    public async Task Consultar_cliente_inexistente_retorna_404()
    {
        var resp = await _http.GetAsync("/clientes/999999");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    // Issue #5: a listagem descartava o primeiro registro
    [Fact]
    public async Task Listagem_inclui_o_primeiro_cliente_cadastrado()
    {
        await _http.PostAsJsonAsync("/clientes", NovoCliente("10000000003", "Primeiro"));

        var lista = await _http.GetFromJsonAsync<List<Dictionary<string, object>>>("/clientes");

        Assert.Contains(lista!, c => c["nome"].ToString() == "Primeiro");
    }
}
