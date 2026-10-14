using GuessTheLyrics.Shared;

namespace GuessTheLyrics.UnitTests;

public class WordNormalizerTests
{
    [Theory]
    [InlineData("Hello", "hello")]
    [InlineData("HEY!", "hey")]
    [InlineData("Don't", "dont")]
    [InlineData("don’t", "dont")]
    [InlineData("Café", "cafe")]
    [InlineData("naïve", "naive")]
    [InlineData("rock'n'roll", "rocknroll")]
    [InlineData("2Pac", "2pac")]
    [InlineData("  spaced  ", "spaced")]
    public void Normalize_Word_ReturnsLowercaseLettersAndDigitsOnly(string input, string expected)
    {
        var result = WordNormalizer.Normalize(input);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("?!...")]
    public void Normalize_NoLettersOrDigits_ReturnsEmpty(string? input)
    {
        var result = WordNormalizer.Normalize(input);

        Assert.Equal(string.Empty, result);
    }
}
