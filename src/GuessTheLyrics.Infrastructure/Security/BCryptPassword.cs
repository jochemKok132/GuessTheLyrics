using GuessTheLyrics.Domain.Interfaces;

namespace GuessTheLyrics.Infrastructure.Security;

public sealed class BCryptPassword : IPassword
{
    public string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password);

    public bool Validate(string password, string hashedPassword) => BCrypt.Net.BCrypt.Verify(password, hashedPassword);
}
