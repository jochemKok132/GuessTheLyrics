using GuessTheLyrics.Application.Lyrics;
using GuessTheLyrics.Shared.Game;
using GuessTheLyrics.Shared.Songs;

namespace GuessTheLyrics.UnitTests;

public class GuessGameTests
{
    private static GuessGame Game(string lyrics)
    {
        var lines = LyricsTokenizer.Tokenize(lyrics);
        return new GuessGame(new LyricsPuzzleResponse
        {
            Id = 1,
            Lines = lines,
            TotalWords = LyricsTokenizer.CountWords(lines),
            UniqueWords = LyricsTokenizer.CountUniqueWords(lines)
        });
    }

    [Fact]
    public void Guess_CorrectWord_IsAcceptedAndInputCleared()
    {
        var game = Game("hello world\nhello");

        var result = game.Guess("Hello");

        Assert.True(result.Accepted);
        Assert.Equal(string.Empty, result.Input);
        Assert.Equal(2, game.FoundWordCount); // both occurrences
        Assert.Equal("hello", game.LastFound);
    }

    [Fact]
    public void Guess_ShortWordWhileLongerHiddenWordStartsTheSame_Waits()
    {
        var game = Game("a day again");

        var result = game.Guess("a");

        Assert.False(result.Accepted);
        Assert.Equal("a", result.Input);
        Assert.True(result.Typed);
    }

    [Fact]
    public void Guess_ShortWordFollowedBySpace_IsAccepted()
    {
        var game = Game("a day again");

        var result = game.Guess("a ");

        Assert.True(result.Accepted);
    }

    [Fact]
    public void Guess_LongerWordTypedThroughShortPrefix_IsAccepted()
    {
        var game = Game("a day again");

        game.Guess("a");
        game.Guess("ag");
        var result = game.Guess("again");

        Assert.True(result.Accepted);
        Assert.Contains("again", game.Found);
        Assert.DoesNotContain("a", game.Found);
    }

    [Fact]
    public void Guess_WrongWordSubmitted_ClearsInputAndGivesFeedback()
    {
        var game = Game("hello world");

        var result = game.Guess("zzz ");

        Assert.False(result.Accepted);
        Assert.Equal(string.Empty, result.Input);
        Assert.Equal("\"zzz\" is not in the song", game.Feedback);
    }

    [Fact]
    public void Guess_AlreadyFoundWordSubmitted_SaysSo()
    {
        var game = Game("hello world");
        game.Guess("hello");

        game.Guess("hello ");

        Assert.Equal("\"hello\" is already found", game.Feedback);
    }

    [Fact]
    public void AllFound_EveryWordGuessed_IsTrue()
    {
        var game = Game("hi there");

        game.Guess("hi");
        game.Guess("there");

        Assert.True(game.AllFound);
        Assert.Equal(100, game.Percentage);
    }

    [Fact]
    public void Restore_SavedWords_CountsOnlyRealWords()
    {
        var game = Game("na na hey");

        game.Restore(["na", "bogus"]);

        Assert.Equal(2, game.FoundWordCount);
        Assert.Single(game.Found);
    }

    [Fact]
    public void Verses_HeaderAndBlankLines_GroupLinesWithLabels()
    {
        var game = Game("[Verse]\none two\n\n[Chorus]\nla la");

        Assert.Equal(2, game.Verses.Count);
        Assert.Equal("Verse", game.Verses[0].Label);
        Assert.Equal("Chorus", game.Verses[1].Label);
        Assert.Equal(new HashSet<string> { "la" }, game.Verses[1].Keys);
    }
}
