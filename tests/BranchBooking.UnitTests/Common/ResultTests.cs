using BranchBooking.Application.Common;
using Xunit;

namespace BranchBooking.UnitTests.Common;

public class ResultTests
{
    [Fact]
    public void A_success_carries_its_value_and_no_error()
    {
        var result = Result<int>.Success(42);

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
        Assert.Null(result.Error);
    }

    [Fact]
    public void A_failure_carries_its_error_and_refuses_to_give_a_value()
    {
        var result = Result<int>.Failure(Errors.BookingNotFound);

        Assert.False(result.IsSuccess);
        Assert.Same(Errors.BookingNotFound, result.Error);
        Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void A_failure_needs_an_error()
    {
        Assert.Throws<ArgumentNullException>(() => Result<int>.Failure(null!));
    }
}
