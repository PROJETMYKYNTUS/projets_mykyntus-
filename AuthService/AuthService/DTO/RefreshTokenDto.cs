using System.ComponentModel.DataAnnotations;

namespace AuthService.DTO
{
    public class RefreshTokenDto
    {
        [Required(ErrorMessage = "Le refresh token est requis")]
        public required string RefreshToken { get; set; }
    }
}