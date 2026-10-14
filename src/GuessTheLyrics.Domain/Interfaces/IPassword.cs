namespace GuessTheLyrics.Domain.Interfaces;

public interface IPassword
{
    string Hash(string password);
    bool Validate(string password, string hashedPassword);
}
