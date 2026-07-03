using TingGo.SharedKernel.Errors;

namespace TingGo.Modules.Ordering.Domain;

/// <summary>
/// State machine tập trung (PRD 7.9) — MỌI chuyển trạng thái order phải đi qua đây,
/// không set Status trực tiếp.
/// </summary>
public static class OrderStateMachine
{
    private static readonly Dictionary<string, string[]> Allowed = new()
    {
        [OrderStatus.Submitted] = [OrderStatus.Confirmed, OrderStatus.Rejected],
        [OrderStatus.Confirmed] = [OrderStatus.Preparing, OrderStatus.Cancelled],
        [OrderStatus.Preparing] = [OrderStatus.Ready, OrderStatus.Cancelled],
        [OrderStatus.Ready] = [OrderStatus.Completed],
        [OrderStatus.Completed] = [],
        [OrderStatus.Rejected] = [],
        [OrderStatus.Cancelled] = [],
    };

    public static bool CanTransition(string from, string to)
        => Allowed.TryGetValue(from, out var targets) && targets.Contains(to);

    /// <summary>Chuyển trạng thái + ghi history. Throw ORDER_INVALID_STATUS nếu không hợp lệ.</summary>
    public static OrderStatusHistory Transition(Order order, string to, Guid? actorMembershipId, string? reason)
    {
        if (!CanTransition(order.Status, to))
        {
            throw new ApiException(ErrorCodes.OrderInvalidStatus,
                $"Không thể chuyển order từ '{order.Status}' sang '{to}'.", 409);
        }
        if (to == OrderStatus.Rejected && string.IsNullOrWhiteSpace(reason))
        {
            throw new ApiException(ErrorCodes.ValidationFailed, "Từ chối order cần lý do.", 400);
        }

        var history = new OrderStatusHistory
        {
            OrderId = order.Id,
            FromStatus = order.Status,
            ToStatus = to,
            ActorMembershipId = actorMembershipId,
            Reason = reason,
        };
        order.Status = to;
        order.RowVersion++;
        order.UpdatedAt = DateTimeOffset.UtcNow;
        if (to == OrderStatus.Completed)
        {
            order.CompletedAt = DateTimeOffset.UtcNow;
        }
        if (to == OrderStatus.Rejected)
        {
            order.RejectionReason = reason;
        }
        return history;
    }
}
