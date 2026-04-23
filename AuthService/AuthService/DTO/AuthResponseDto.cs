namespace AuthService.DTO
{
    public class AuthResponseDto
    {
        public required string AccessToken { get; set; }
        public required string RefreshToken { get; set; }
        public int ExpiresIn { get; set; }
        public required string TokenType { get; set; }
        public required UserDto User { get; set; }
    }
}