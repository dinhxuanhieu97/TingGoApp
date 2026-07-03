using TingGo.Modules.Ordering.Domain;
using TingGo.SharedKernel.Errors;
using Xunit;

namespace TingGo.UnitTests;

/// <summary>Test plan U1–U8 — 100% nhánh state machine.</summary>
public class OrderStateMachineTests
{
    [Theory]
    [InlineData(OrderStatus.Submitted, OrderStatus.Confirmed)]   // U1
    [InlineData(OrderStatus.Confirmed, OrderStatus.Preparing)]   // U3
    [InlineData(OrderStatus.Preparing, OrderStatus.Ready)]       // U4
    [InlineData(OrderStatus.Ready, OrderStatus.Completed)]       // U5
    [InlineData(OrderStatus.Confirmed, OrderStatus.Cancelled)]   // U6
    [InlineData(OrderStatus.Preparing, OrderStatus.Cancelled)]   // U6
    public void Transition_HopLe_DoiTrangThaiVaGhiHistory(string from, string to)
    {
        var order = new Order { Status = from, RowVersion = 3 };

        var history = OrderStateMachine.Transition(order, to, actorMembershipId: Guid.NewGuid(), reason: null);

        Assert.Equal(to, order.Status);
        Assert.Equal(4, order.RowVersion);
        Assert.Equal(from, history.FromStatus);
        Assert.Equal(to, history.ToStatus);
    }

    [Fact] // U2
    public void Reject_CanReason()
    {
        var order = new Order { Status = OrderStatus.Submitted };

        Assert.Throws<ApiException>(() =>
            OrderStateMachine.Transition(order, OrderStatus.Rejected, null, reason: null));

        var history = OrderStateMachine.Transition(order, OrderStatus.Rejected, null, reason: "Hết nguyên liệu");
        Assert.Equal(OrderStatus.Rejected, order.Status);
        Assert.Equal("Hết nguyên liệu", order.RejectionReason);
        Assert.Equal("Hết nguyên liệu", history.Reason);
    }

    [Theory] // U7 — trạng thái cuối không đi đâu được
    [InlineData(OrderStatus.Completed)]
    [InlineData(OrderStatus.Rejected)]
    [InlineData(OrderStatus.Cancelled)]
    public void TrangThaiCuoi_KhongTheChuyen(string finalStatus)
    {
        foreach (var target in new[]
                 {
                     OrderStatus.Submitted, OrderStatus.Confirmed, OrderStatus.Preparing,
                     OrderStatus.Ready, OrderStatus.Completed, OrderStatus.Cancelled,
                 })
        {
            var order = new Order { Status = finalStatus };
            var ex = Assert.Throws<ApiException>(() =>
                OrderStateMachine.Transition(order, target, null, "x"));
            Assert.Equal(ErrorCodes.OrderInvalidStatus, ex.Code);
        }
    }

    [Theory] // U8 — nhảy cóc bị chặn
    [InlineData(OrderStatus.Submitted, OrderStatus.Ready)]
    [InlineData(OrderStatus.Submitted, OrderStatus.Completed)]
    [InlineData(OrderStatus.Submitted, OrderStatus.Preparing)]
    [InlineData(OrderStatus.Confirmed, OrderStatus.Completed)]
    [InlineData(OrderStatus.Ready, OrderStatus.Cancelled)]
    public void NhayCoc_BiChan(string from, string to)
    {
        var order = new Order { Status = from };
        var ex = Assert.Throws<ApiException>(() => OrderStateMachine.Transition(order, to, null, "x"));
        Assert.Equal(ErrorCodes.OrderInvalidStatus, ex.Code);
    }

    [Fact] // U5 chi tiết: completed_at được set
    public void Complete_SetCompletedAt()
    {
        var order = new Order { Status = OrderStatus.Ready };
        OrderStateMachine.Transition(order, OrderStatus.Completed, null, null);
        Assert.NotNull(order.CompletedAt);
    }
}
