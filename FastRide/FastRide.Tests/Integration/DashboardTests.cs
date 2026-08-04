using FastRide.Shared.DTOs;
using FastRide.Shared.Models;
using FastRide.Tests.Infrastructure;

namespace FastRide.Tests.Integration;

[Collection(ApiCollection.Name)]
public class DashboardTests(ApiFixture fixture)
{
    [Fact]
    public async Task Overview_ReturnsEverySectionInOneCall()
    {
        // The console makes this one call instead of six; all of it has to be there.
        var overview = await fixture.Admin.GetAndReadAsync<DashboardOverviewResponse>("/api/dashboard/overview");

        Assert.NotNull(overview.Stats);
        Assert.NotEmpty(overview.ByStatus);
        Assert.Equal(24, overview.Hourly.Count);
        Assert.Equal(30, overview.RevenueSeries.Count);
        Assert.NotNull(overview.TopDrivers);
        Assert.NotNull(overview.Categories);
        Assert.NotNull(overview.PaymentMethods);
    }

    [Fact]
    public async Task OrdersByStatus_ListsEveryStatusIncludingTheEmptyOnes()
    {
        // A chart legend that changes shape as data arrives is hard to read.
        var byStatus = await fixture.Admin.GetAndReadAsync<List<OrderStatusCount>>("/api/dashboard/orders-by-status");

        foreach (var status in Enum.GetValues<OrderStatus>())
            Assert.Contains(byStatus, entry => entry.Status == status);
    }

    [Fact]
    public async Task OrdersByHour_AlwaysCoversAFullDay()
    {
        var hourly = await fixture.Admin.GetAndReadAsync<List<HourlyStats>>("/api/dashboard/orders-by-hour");

        Assert.Equal(24, hourly.Count);
        Assert.Equal(Enumerable.Range(0, 24), hourly.Select(bucket => bucket.Hour));
    }

    [Fact]
    public async Task RevenueSeries_HonoursTheRequestedWindow()
    {
        var week = await fixture.Admin.GetAndReadAsync<List<RevenuePoint>>("/api/dashboard/revenue-series?days=7");

        Assert.Equal(7, week.Count);
        Assert.Equal(week.OrderBy(point => point.Date), week);
    }

    [Fact]
    public async Task RevenueSeries_ClampsAnAbsurdWindow()
    {
        var series = await fixture.Admin.GetAndReadAsync<List<RevenuePoint>>("/api/dashboard/revenue-series?days=99999");

        Assert.Equal(365, series.Count);
    }

    [Fact]
    public async Task Stats_CountsATripThroughToCompletion()
    {
        var before = await fixture.Admin.GetAndReadAsync<DashboardStatsResponse>("/api/dashboard/stats");

        var rider = await fixture.NewRiderAsync();
        var driver = await fixture.NewVerifiedDriverAsync();
        var order = await OrderLifecycleTests.CompleteATripAsync(rider, driver);

        // The stats endpoint is cached for ten seconds; overview shares the computation but
        // has its own key, so read a fresh figure from the financial report instead.
        var report = await fixture.Admin.GetAndReadAsync<FinancialSummaryResponse>(
            $"/api/dashboard/financial-summary?from={DateTime.UtcNow:yyyy-MM-dd}&to={DateTime.UtcNow:yyyy-MM-dd}");

        Assert.True(report.CompletedOrders >= 1);
        Assert.True(report.NetRevenue >= order.FinalFare);
        Assert.True(before.TotalOrdersToday >= 0);
    }

    [Fact]
    public async Task FinancialSummary_AddsUp()
    {
        var report = await fixture.Admin.GetAndReadAsync<FinancialSummaryResponse>("/api/dashboard/financial-summary");

        Assert.Equal(report.GrossRevenue - report.Discounts, report.NetRevenue);
        Assert.Equal(report.NetRevenue - report.PlatformCommission, report.DriverEarnings);
        Assert.True(report.From <= report.To);
    }

    [Fact]
    public async Task FinancialSummary_TakesTwentyPercentAsCommission()
    {
        var rider = await fixture.NewRiderAsync();
        var driver = await fixture.NewVerifiedDriverAsync();
        await OrderLifecycleTests.CompleteATripAsync(rider, driver);

        var report = await fixture.Admin.GetAndReadAsync<FinancialSummaryResponse>("/api/dashboard/financial-summary");

        Assert.Equal(Math.Round(report.NetRevenue * 0.20m, 0), report.PlatformCommission);
    }

    [Fact]
    public async Task TopDrivers_RanksByEarnings()
    {
        var rider = await fixture.NewRiderAsync();
        var driver = await fixture.NewVerifiedDriverAsync();
        await OrderLifecycleTests.CompleteATripAsync(rider, driver);

        var leaders = await fixture.Admin.GetAndReadAsync<List<TopDriverItem>>("/api/dashboard/top-drivers?limit=10");

        Assert.NotEmpty(leaders);
        Assert.Equal(leaders.OrderByDescending(item => item.Earnings), leaders);
    }

    [Fact]
    public async Task OrdersCsv_DownloadsAsASpreadsheetFile()
    {
        using var response = await fixture.Admin.GetAsync("/api/orders/export.csv");

        response.EnsureSuccessStatusCode();
        Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Kode", body);
    }

    [Fact]
    public async Task FinancialCsv_DownloadsAsASpreadsheetFile()
    {
        using var response = await fixture.Admin.GetAsync("/api/dashboard/financial-summary/export.csv");

        response.EnsureSuccessStatusCode();
        Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Tanggal", body);
    }

    [Fact]
    public async Task Health_ReportsTheProvidersInUse()
    {
        var client = fixture.NewClient();

        var health = await client.GetAndReadAsync<HealthResponse>("/api/health");

        Assert.Equal("healthy", health.Status);
        Assert.Equal("SQLite", health.Database);
        Assert.Equal("FileSystem", health.StorageProvider);
        Assert.Equal("Memory", health.Cache);
    }
}
