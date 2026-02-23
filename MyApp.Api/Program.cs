using System.Collections.Concurrent;
using System.Threading;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// In-memory store
builder.Services.AddSingleton<InMemoryHoldingsStore>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// --- Health endpoint (unchanged contract) ---
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

// --- Holdings CRUD endpoints ---

app.MapPost("/holdings", (UpsertHoldingRequest request, InMemoryHoldingsStore store) =>
{
    var errors = Validate(request);
    if (errors.Count > 0)
        return Results.BadRequest(new { errors });

    var created = store.Create(
        Normalize(request.Symbol),
        request.Shares,
        request.AverageCost,
        Normalize(request.Currency)
    );

    return Results.Created($"/holdings/{created.Id}", created);
});

app.MapGet("/holdings", (InMemoryHoldingsStore store) =>
{
    return Results.Ok(store.List());
});

app.MapGet("/holdings/{id:int}", (int id, InMemoryHoldingsStore store) =>
{
    var holding = store.Get(id);
    return holding is null ? Results.NotFound() : Results.Ok(holding);
});

app.MapPut("/holdings/{id:int}", (int id, UpsertHoldingRequest request, InMemoryHoldingsStore store) =>
{
    var errors = Validate(request);
    if (errors.Count > 0)
        return Results.BadRequest(new { errors });

    var updated = store.Update(
        id,
        Normalize(request.Symbol),
        request.Shares,
        request.AverageCost,
        Normalize(request.Currency)
    );

    return updated is null ? Results.NotFound() : Results.Ok(updated);
});

app.MapDelete("/holdings/{id:int}", (int id, InMemoryHoldingsStore store) =>
{
    return store.Delete(id)
        ? Results.NoContent()
        : Results.NotFound();
});

app.Run();

// -------------------- Helpers & Models --------------------

static string Normalize(string value) =>
    value.Trim().ToUpperInvariant();

static Dictionary<string, string> Validate(UpsertHoldingRequest r)
{
    var errors = new Dictionary<string, string>();

    if (string.IsNullOrWhiteSpace(r.Symbol))
        errors["symbol"] = "Symbol is required.";

    if (string.IsNullOrWhiteSpace(r.Currency))
        errors["currency"] = "Currency is required.";

    if (r.Shares <= 0)
        errors["shares"] = "Shares must be greater than 0.";

    if (r.AverageCost < 0)
        errors["averageCost"] = "AverageCost must be >= 0.";

    return errors;
}

public sealed record UpsertHoldingRequest(
    string Symbol,
    decimal Shares,
    decimal AverageCost,
    string Currency
);

public sealed record HoldingResponse(
    int Id,
    string Symbol,
    decimal Shares,
    decimal AverageCost,
    string Currency
);

public sealed class InMemoryHoldingsStore
{
    private int _nextId = 0;

    private readonly ConcurrentDictionary<int, HoldingResponse> _byId = new();
    private readonly ConcurrentQueue<int> _insertionOrder = new();

    public HoldingResponse Create(string symbol, decimal shares, decimal averageCost, string currency)
    {
        var id = Interlocked.Increment(ref _nextId);

        var holding = new HoldingResponse(id, symbol, shares, averageCost, currency);

        _byId[id] = holding;
        _insertionOrder.Enqueue(id);

        return holding;
    }

    public HoldingResponse? Get(int id)
        => _byId.TryGetValue(id, out var h) ? h : null;

    public IReadOnlyList<HoldingResponse> List()
    {
        var result = new List<HoldingResponse>();

        foreach (var id in _insertionOrder)
        {
            if (_byId.TryGetValue(id, out var h))
                result.Add(h);
        }

        return result;
    }

    public HoldingResponse? Update(int id, string symbol, decimal shares, decimal averageCost, string currency)
    {
        if (!_byId.TryGetValue(id, out var existing))
            return null;

        var updated = existing with
        {
            Symbol = symbol,
            Shares = shares,
            AverageCost = averageCost,
            Currency = currency
        };

        _byId[id] = updated;

        return updated;
    }

    public bool Delete(int id)
        => _byId.TryRemove(id, out _);
}

// Required for integration testing
public partial class Program { }
