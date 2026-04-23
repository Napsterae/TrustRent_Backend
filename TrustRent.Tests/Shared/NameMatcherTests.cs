using TrustRent.Shared.Services;

namespace TrustRent.Tests.Shared;

public class NameMatcherTests
{
    [Theory]
    [InlineData("João Silva", "João Silva")]
    [InlineData("João Silva", "joao silva")]
    [InlineData("João Pedro Silva", "Joao Silva")]
    [InlineData("João Pedro Almeida Silva", "JOAO SILVA")]
    [InlineData("Maria João dos Santos", "Maria dos Santos")]
    public void IsLikelySame_MatchingNames_ReturnsTrue(string a, string b)
    {
        Assert.True(NameMatcher.IsLikelySame(a, b));
    }

    [Theory]
    [InlineData("João Silva", "Pedro Costa")]
    [InlineData("Ana Pereira", "Maria Santos")]
    [InlineData("João Silva", "")]
    [InlineData(null, "João Silva")]
    public void IsLikelySame_DifferentNames_ReturnsFalse(string? a, string? b)
    {
        Assert.False(NameMatcher.IsLikelySame(a, b));
    }

    [Fact]
    public void Normalize_RemovesAccentsAndLowercases()
    {
        Assert.Equal("joao silva", NameMatcher.Normalize("João Silva"));
        Assert.Equal("francois muller", NameMatcher.Normalize("François Müller"));
    }
}
