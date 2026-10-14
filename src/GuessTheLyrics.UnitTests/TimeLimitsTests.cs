using GuessTheLyrics.Shared.Game;

namespace GuessTheLyrics.UnitTests;

public class TimeLimitsTests
{
    [Theory]
    [InlineData(37, 120)]   // 111 s → rounded up to 2:00
    [InlineData(40, 120)]   // exactly 120 s
    [InlineData(41, 135)]   // 123 s → 2:15
    [InlineData(100, 300)]
    public void ForUniqueWords_ThreeSecondsPerWord_RoundedUpTo15Seconds(int uniqueWords, int expectedSeconds)
    {
        Assert.Equal(expectedSeconds, TimeLimits.ForUniqueWords(uniqueWords));
    }

    [Fact]
    public void ForUniqueWords_FewWords_GetsAtLeastOneMinute()
    {
        Assert.Equal(TimeLimits.MinimumSeconds, TimeLimits.ForUniqueWords(5));
        Assert.Equal(TimeLimits.MinimumSeconds, TimeLimits.ForUniqueWords(0));
    }

    [Fact]
    public void ForUniqueWords_HugeSong_CappedAtTwentyMinutes()
    {
        Assert.Equal(TimeLimits.MaximumSeconds, TimeLimits.ForUniqueWords(5000));
    }
}
