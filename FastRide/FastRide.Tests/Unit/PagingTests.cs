using FastRide.Shared.Common;

namespace FastRide.Tests.Unit;

public class PagingTests
{
    [Fact]
    public void From_UsesTheDefaultsWhenNothingIsSupplied()
    {
        var page = PageRequest.From(null, null);

        Assert.Equal(1, page.Page);
        Assert.Equal(25, page.Limit);
        Assert.Equal(0, page.Skip);
    }

    [Fact]
    public void From_HonoursAnExplicitDefaultLimit() =>
        Assert.Equal(20, PageRequest.From(null, null, defaultLimit: 20).Limit);

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(3, 3)]
    public void From_NeverReturnsAPageBelowOne(int requested, int expected) =>
        Assert.Equal(expected, PageRequest.From(requested, null).Page);

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-10, 1)]
    [InlineData(50, 50)]
    [InlineData(1_000_000, PageRequest.MaxLimit)]
    public void From_ClampsTheLimit(int requested, int expected) =>
        Assert.Equal(expected, PageRequest.From(null, requested).Limit);

    [Fact]
    public void Skip_MovesByWholePages()
    {
        var page = PageRequest.From(4, 25);

        Assert.Equal(75, page.Skip);
    }

    [Fact]
    public void TotalPages_RoundsUp()
    {
        var result = new PagedResult<string> { Total = 41, Page = 1, Limit = 20 };

        Assert.Equal(3, result.TotalPages);
    }

    [Fact]
    public void TotalPages_IsZero_WhenTheLimitIsMeaningless() =>
        Assert.Equal(0, new PagedResult<string> { Total = 10, Limit = 0 }.TotalPages);

    [Fact]
    public void HasNext_And_HasPrevious_DescribeThePositionInTheSet()
    {
        var first = new PagedResult<string> { Total = 100, Page = 1, Limit = 25 };
        var middle = new PagedResult<string> { Total = 100, Page = 2, Limit = 25 };
        var last = new PagedResult<string> { Total = 100, Page = 4, Limit = 25 };

        Assert.False(first.HasPrevious);
        Assert.True(first.HasNext);

        Assert.True(middle.HasPrevious);
        Assert.True(middle.HasNext);

        Assert.True(last.HasPrevious);
        Assert.False(last.HasNext);
    }

    [Fact]
    public void Empty_CarriesThePagingBackToTheClient()
    {
        var empty = PagedResult<string>.Empty(page: 3, limit: 10);

        Assert.Equal(0, empty.Total);
        Assert.Equal(3, empty.Page);
        Assert.Equal(10, empty.Limit);
        Assert.Empty(empty.Data);
    }
}
