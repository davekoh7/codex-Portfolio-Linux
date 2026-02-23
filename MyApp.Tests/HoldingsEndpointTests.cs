using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

public class HoldingsEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public HoldingsEndpointTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Crud_Holdings_Works_With_InMemory_Store()
    {
        var createPayload = new UpsertHoldingRequest(" msft ", 10m, 415.25m, "usd");

        var createResponse = await _client.PostAsJsonAsync("/holdings", createPayload);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var created = await createResponse.Content.ReadFromJsonAsync<HoldingResponse>();
        Assert.NotNull(created);
        Assert.True(created!.Id > 0);
        Assert.Equal("MSFT", created.Symbol);
        Assert.Equal(10m, created.Shares);
        Assert.Equal(415.25m, created.AverageCost);
        Assert.Equal("USD", created.Currency);

        var getResponse = await _client.GetAsync($"/holdings/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var fetched = await getResponse.Content.ReadFromJsonAsync<HoldingResponse>();
        Assert.NotNull(fetched);
        Assert.Equal(created.Id, fetched!.Id);

        var updatePayload = new UpsertHoldingRequest("msft", 12m, 420m, "USD");
        var updateResponse = await _client.PutAsJsonAsync($"/holdings/{created.Id}", updatePayload);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var updated = await updateResponse.Content.ReadFromJsonAsync<HoldingResponse>();
        Assert.NotNull(updated);
        Assert.Equal(12m, updated!.Shares);
        Assert.Equal(420m, updated.AverageCost);

        var list = await _client.GetFromJsonAsync<List<HoldingResponse>>("/holdings");
        Assert.NotNull(list);
        Assert.Contains(list!, h => h.Id == created.Id && h.Shares == 12m);

        var deleteResponse = await _client.DeleteAsync($"/holdings/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var getAfterDelete = await _client.GetAsync($"/holdings/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getAfterDelete.StatusCode);
    }

    [Fact]
    public async Task Get_Unknown_Holding_Returns_NotFound()
    {
        var response = await _client.GetAsync($"/holdings/{int.MaxValue}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Post_Invalid_Holding_Returns_BadRequest()
    {
        var invalid = new UpsertHoldingRequest("", 0m, -1m, "");

        var response = await _client.PostAsJsonAsync("/holdings", invalid);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private sealed class HoldingResponse
    {
        public int Id { get; set; }
        public string Symbol { get; set; } = string.Empty;
        public decimal Shares { get; set; }
        public decimal AverageCost { get; set; }
        public string Currency { get; set; } = string.Empty;
    }

    private sealed record UpsertHoldingRequest(string Symbol, decimal Shares, decimal AverageCost, string Currency);
}
