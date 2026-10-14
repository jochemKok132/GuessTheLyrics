using GuessTheLyrics.Domain.Entities;

namespace GuessTheLyrics.Application.Interfaces;

public interface ITokenService
{
    (string Token, DateTime ExpiresAt) CreateToken(User user);
}
