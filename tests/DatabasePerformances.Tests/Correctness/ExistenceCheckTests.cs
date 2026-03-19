using DatabasePerformances.Infrastructure.Naive.Queries;
using DatabasePerformances.Infrastructure.Optimized.Queries;
using Xunit;

namespace DatabasePerformances.Tests.Correctness;

/// <summary>
/// Correctness tests for Scenario 9 — Count() vs Any() existence checks.
/// Verifies that both naive (<c>Count() > 0</c>) and optimized (<c>Any()</c>)
/// return the same boolean answer for the same logical question.
/// </summary>
[Collection(nameof(DatabaseCollection))]
public sealed class ExistenceCheckTests(DatabaseFixture fixture)
{
    private readonly NaiveExistenceCheckQueries _naive       = new(fixture.NaiveContext);
    private readonly OptimizedExistenceCheckQueries _optimized = new(fixture.OptimizedContext);

    [Fact(DisplayName = "Email existence: both agree customer 1 exists")]
    public async Task EmailExists_BothAgree()
    {
        // Fetch an email that is guaranteed to exist in both databases
        var customer = await fixture.OptimizedContext.Customers.FindAsync(1);
        Assert.NotNull(customer);

        var naiveResult     = await _naive.CustomerExistsByEmailAsync(customer!.Email);
        var optimizedResult = await _optimized.CustomerExistsByEmailAsync(customer.Email);

        Assert.True(naiveResult);
        Assert.True(optimizedResult);
    }

    [Fact(DisplayName = "Email existence: both agree non-existent email is false")]
    public async Task EmailNotExists_BothAgreeFalse()
    {
        const string fakeEmail = "definitely-does-not-exist-xyz@nope.invalid";

        var naiveResult     = await _naive.CustomerExistsByEmailAsync(fakeEmail);
        var optimizedResult = await _optimized.CustomerExistsByEmailAsync(fakeEmail);

        Assert.False(naiveResult);
        Assert.False(optimizedResult);
    }

    [Fact(DisplayName = "Has-orders: both agree customer 1 has orders")]
    public async Task HasOrders_BothAgree()
    {
        var naiveResult     = await _naive.CustomerHasOrdersAsync(1);
        var optimizedResult = await _optimized.CustomerHasOrdersAsync(1);

        Assert.True(naiveResult);
        Assert.True(optimizedResult);
    }

    [Fact(DisplayName = "Has-orders: both agree non-existent customer has no orders")]
    public async Task HasOrders_NonExistentCustomer_BothFalse()
    {
        const int nonExistentId = int.MaxValue;

        var naiveResult     = await _naive.CustomerHasOrdersAsync(nonExistentId);
        var optimizedResult = await _optimized.CustomerHasOrdersAsync(nonExistentId);

        Assert.False(naiveResult);
        Assert.False(optimizedResult);
    }
}
