using FastRide.Api.Services;
using FastRide.Shared.Models;

namespace FastRide.Tests.Unit;

/// <summary>
/// The transition table is the contract every caller shares — mobile apps, admin console and
/// simulator all go through it. Anything not listed must be refused.
/// </summary>
public class OrderTransitionTests
{
    [Theory]
    [InlineData(OrderStatus.Requested, OrderStatus.Accepted)]
    [InlineData(OrderStatus.Requested, OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Requested, OrderStatus.Expired)]
    [InlineData(OrderStatus.Accepted, OrderStatus.DriverArrived)]
    [InlineData(OrderStatus.Accepted, OrderStatus.Started)]
    [InlineData(OrderStatus.Accepted, OrderStatus.Cancelled)]
    [InlineData(OrderStatus.DriverArrived, OrderStatus.Started)]
    [InlineData(OrderStatus.DriverArrived, OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Started, OrderStatus.Completed)]
    [InlineData(OrderStatus.Started, OrderStatus.Cancelled)]
    public void CanTransition_AllowsTheHappyPathAndCancellation(OrderStatus from, OrderStatus to) =>
        Assert.True(OrderService.CanTransition(from, to));

    [Theory]
    [InlineData(OrderStatus.Requested, OrderStatus.Completed)]   // paid without ever starting
    [InlineData(OrderStatus.Requested, OrderStatus.Started)]     // started without a driver
    [InlineData(OrderStatus.Accepted, OrderStatus.Completed)]    // completed before pickup
    [InlineData(OrderStatus.DriverArrived, OrderStatus.Completed)]
    [InlineData(OrderStatus.Started, OrderStatus.Accepted)]      // backwards
    [InlineData(OrderStatus.Completed, OrderStatus.Started)]
    [InlineData(OrderStatus.Cancelled, OrderStatus.Accepted)]
    public void CanTransition_RefusesEverythingElse(OrderStatus from, OrderStatus to) =>
        Assert.False(OrderService.CanTransition(from, to));

    [Theory]
    [InlineData(OrderStatus.Completed)]
    [InlineData(OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Expired)]
    public void TerminalStates_HaveNoWayOut(OrderStatus terminal)
    {
        foreach (var target in Enum.GetValues<OrderStatus>())
            Assert.False(OrderService.CanTransition(terminal, target));
    }

    [Fact]
    public void NoStatus_CanTransitionToItself()
    {
        foreach (var status in Enum.GetValues<OrderStatus>())
            Assert.False(OrderService.CanTransition(status, status));
    }

    [Fact]
    public void EveryLiveStatus_CanBeCancelled()
    {
        OrderStatus[] live =
            [OrderStatus.Requested, OrderStatus.Accepted, OrderStatus.DriverArrived, OrderStatus.Started];

        foreach (var status in live)
            Assert.True(OrderService.CanTransition(status, OrderStatus.Cancelled));
    }
}
