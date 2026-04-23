using System.ComponentModel.DataAnnotations;

namespace AuthService.DTO
{
    public class LoginDto
    {
        [Required(ErrorMessage = "L'email est requis")]
        [EmailAddress(ErrorMessage = "Format d'email invalide")]
        public required string Email { get; set; }

        [Required(ErrorMessage = "Le mot de passe est requis")]
        public required string Password { get; set; }
    }
}