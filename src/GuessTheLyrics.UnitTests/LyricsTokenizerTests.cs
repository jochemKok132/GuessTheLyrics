using GuessTheLyrics.Application.Lyrics;

namespace GuessTheLyrics.UnitTests;

public class LyricsTokenizerTests
{
    [Fact]
    public void Tokenize_NullOrEmpty_ReturnsNoLines()
    {
        Assert.Empty(LyricsTokenizer.Tokenize(null));
        Assert.Empty(LyricsTokenizer.Tokenize("   \n  "));
    }

    [Fact]
    public void Tokenize_Line_SplitsWordsAndSeparators()
    {
        var lines = LyricsTokenizer.Tokenize("Hey, you're here!");

        var tokens = Assert.Single(lines).Tokens;
        Assert.Equal(new[] { "Hey", ", ", "you're", " ", "here", "!" }, tokens.Select(t => t.Text));
        Assert.Equal(new string?[] { "hey", null, "youre", null, "here", null }, tokens.Select(t => t.Key));
    }

    [Fact]
    public void Tokenize_TokensJoinedTogether_RebuildTheOriginalLine()
    {
        const string line = "(Oh-oh) we're gonna... rock 'n' roll, baby";

        var lines = LyricsTokenizer.Tokenize(line);

        Assert.Equal(line, string.Concat(Assert.Single(lines).Tokens.Select(t => t.Text)));
    }

    [Fact]
    public void Tokenize_HyphenatedWord_IsSplitIntoTwoWords()
    {
        var lines = LyricsTokenizer.Tokenize("ever-lasting");

        var keys = lines[0].Tokens.Where(t => t.Key is not null).Select(t => t.Key);
        Assert.Equal(new string?[] { "ever", "lasting" }, keys);
    }

    [Fact]
    public void Tokenize_SectionHeader_IsMarkedAsHeaderWithoutWords()
    {
        var lines = LyricsTokenizer.Tokenize("[Chorus]\nla la");

        Assert.True(lines[0].IsHeader);
        Assert.All(lines[0].Tokens, t => Assert.Null(t.Key));
        Assert.False(lines[1].IsHeader);
    }

    [Fact]
    public void Tokenize_MultipleBlankLines_CollapseIntoOneVerseBreak()
    {
        var lines = LyricsTokenizer.Tokenize("\n\nfirst\n\n\n\nsecond\n\n");

        Assert.Equal(3, lines.Count);
        Assert.NotEmpty(lines[0].Tokens);
        Assert.Empty(lines[1].Tokens);
        Assert.NotEmpty(lines[2].Tokens);
    }

    [Fact]
    public void Tokenize_WindowsLineEndings_AreHandled()
    {
        var lines = LyricsTokenizer.Tokenize("one\r\ntwo");

        Assert.Equal(2, lines.Count);
    }

    [Fact]
    public void CountWords_RepeatedWords_CountsEveryOccurrence()
    {
        var lines = LyricsTokenizer.Tokenize("na na na\nhey Na");

        Assert.Equal(5, LyricsTokenizer.CountWords(lines));
        Assert.Equal(2, LyricsTokenizer.CountUniqueWords(lines));
    }
}
