using BS2.Application.Analytics;

namespace BS2.Api.Endpoints;

public static class AnalyticsEndpoints
{
    public static RouteGroupBuilder MapAnalytics(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/analytics");

        group.MapGet("/expenses", async (HttpRequest request, AnalyticsService analytics, CancellationToken ct) =>
            Results.Ok(await analytics.ExpensesAsync(QueryBinding.BindFilter(request.Query), ct)));

        group.MapGet("/income", async (HttpRequest request, AnalyticsService analytics, CancellationToken ct) =>
            Results.Ok(await analytics.IncomeAsync(QueryBinding.BindFilter(request.Query), ct)));

        group.MapGet("/savings", async (HttpRequest request, AnalyticsService analytics, CancellationToken ct) =>
            Results.Ok(await analytics.SavingsAsync(QueryBinding.BindFilter(request.Query), ct)));

        group.MapGet("/summary", async (HttpRequest request, AnalyticsService analytics, CancellationToken ct) =>
            Results.Ok(await analytics.SummaryAsync(QueryBinding.BindFilter(request.Query), ct)));

        return api;
    }
}
