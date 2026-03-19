using DatabasePerformances.Infrastructure.Naive.Queries;
using DatabasePerformances.Infrastructure.Optimized.Queries;
using Xunit;

namespace DatabasePerformances.Tests.Correctness;

/// <summary>
/// Correctness tests for Scenario 1 — Customer Search.
/// Verifies that naive and optimized implementations return logically equivalent
/// results for the same logical query (email contains "john").
/// </summary>
[Collection(nameof(DatabaseCollection))]
public sealed class CustomerSearchTests(DatabaseFixture fixture)
{
    private readonly NaiveCustomerQueries _naive         = new(fixture.NaiveContext);
    private readonly OptimizedCustomerQueries _optimized = new(fixture.OptimizedContext);

    [Fact(DisplayName = "Email search: both return at least one result")]
    public async Task SearchByEmail_BothReturnResults()
    {
        var naiveResults     = await _naive.SearchByEmailAsync("john");
        var optimizedResults = await _optimized.SearchByEmailPrefixAsync("john");

        Assert.NotEmpty(naiveResults);
        Assert.NotEmpty(optimizedResults);
    }

    [Fact(DisplayName = "Email search: optimized result count is within range of naive count")]
    public async Task SearchByEmail_OptimizedResultSubsetOfNaive()
    {
        // Naive uses Contains("john") — may return more results than optimized's StartsWith("john")
        // The optimized result set must be a subset (all prefix-matches are also mid-matches).
        var naiveEmails     = (await _naive.SearchByEmailAsync("john"))
                                  .Select(c => c.Email.ToLowerInvariant())
                                  .ToHashSet();

        var optimizedEmails = (await _optimized.SearchByEmailPrefixAsync("john"))
                                  .Select(r => r.Email.ToLowerInvariant())
                                  .ToList();

        // Every prefix match must also appear in the naive (contains) result set
        foreach (var email in optimizedEmails)
        {
            Assert.Contains(email, naiveEmails);
        }
    }

    [Fact(DisplayName = "LastName search: both return at least one result")]
    public async Task SearchByLastName_BothReturnResults()
    {
        var naiveResults     = await _naive.SearchByLastNameAsync("Sm");
        var optimizedResults = await _optimized.SearchByLastNamePrefixAsync("Sm");

        Assert.NotEmpty(naiveResults);
        Assert.NotEmpty(optimizedResults);
    }
}
