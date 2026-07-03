using TingGo.SharedKernel.Errors;
using Xunit;

namespace TingGo.UnitTests;

public class SanityTests
{
    [Fact]
    public void ApiError_GiuDungFormatPrd()
    {
        var error = new ApiError(ErrorCodes.OrderInvalidStatus, "Test", "trace-1");

        Assert.Equal("ORDER_INVALID_STATUS", error.Code);
        Assert.Equal("trace-1", error.TraceId);
        Assert.Null(error.Details);
    }
}
