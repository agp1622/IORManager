namespace IORManager.Dtos;

public record LoginResponseDto(string Token, string Name, string Email, string Role);
