using Beep.KocAiCommunity.Application.Jobs;
using FluentAssertions;
using Xunit;

namespace Beep.KocAiCommunity.UnitTests;

public class RetryPolicyTests
{
    [Fact]
    public void First_attempt_uses_the_base_delay()
    {
        RetryPolicy.BackoffFor(1).Should().Be(RetryPolicy.BaseDelay);
        RetryPolicy.BackoffFor(0).Should().Be(RetryPolicy.BaseDelay);
    }

    [Fact]
    public void Backoff_doubles_each_attempt_until_capped()
    {
        RetryPolicy.BackoffFor(2).Should().Be(RetryPolicy.BaseDelay * 2);
        RetryPolicy.BackoffFor(3).Should().Be(RetryPolicy.BaseDelay * 4);
        RetryPolicy.BackoffFor(4).Should().Be(RetryPolicy.BaseDelay * 8);
    }

    [Fact]
    public void Backoff_never_exceeds_the_cap_or_overflows()
    {
        RetryPolicy.BackoffFor(100).Should().Be(RetryPolicy.MaxDelay);
        RetryPolicy.BackoffFor(int.MaxValue).Should().Be(RetryPolicy.MaxDelay);
    }

    [Fact]
    public void Backoff_is_monotonic_non_decreasing()
    {
        var last = TimeSpan.Zero;
        for (var attempt = 1; attempt <= 30; attempt++)
        {
            var delay = RetryPolicy.BackoffFor(attempt);
            delay.Should().BeGreaterThanOrEqualTo(last);
            last = delay;
        }
    }
}
