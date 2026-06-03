using System.Security.Claims;
using AuthService.DTO;
using AuthService.Helpers;
using AuthService.Interfaces;
using AuthService.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly ILogger<AuthController> _logger;
        private readonly IUserRepository _userRepository;   // 🆕
        private readonly IPasswordHasher _passwordHasher;   // 🆕
        private readonly IRoleRepository _roleRepository;   // 🆕

        public AuthController(
            IAuthService authService,
            ILogger<AuthController> logger,
            IUserRepository userRepository,     // 🆕
            IPasswordHasher passwordHasher,     // 🆕
            IRoleRepository roleRepository)     // 🆕
        {
            _authService = authService;
            _logger = logger;
            _userRepository = userRepository;   // 🆕
            _passwordHasher = passwordHasher;   // 🆕
            _roleRepository = roleRepository;   // 🆕
        }

        // ✅ TOUS LES ENDPOINTS EXISTANTS INCHANGÉS
        [HttpPost("register")]
        [AllowAnonymous]
        public async Task<IActionResult> Register([FromBody] RegisterDto registerDto)
        {
            try
            {
                var response = await _authService.RegisterAsync(registerDto);
                return Ok(response);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Erreur lors de l'inscription");
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur serveur lors de l'inscription");
                return StatusCode(500, new { message = "Une erreur est survenue lors de l'inscription" });
            }
        }
 

        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody] LoginDto loginDto)
        {
            try
            {
                var response = await _authService.LoginAsync(loginDto);
                return Ok(response);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Tentative de connexion échouée pour {Email}", loginDto.Email);
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur serveur lors de la connexion");
                return StatusCode(500, new { message = "Une erreur est survenue lors de la connexion" });
            }
        }

        [HttpPost("refresh")]
        [AllowAnonymous]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenDto refreshTokenDto)
        {
            try
            {
                var response = await _authService.RefreshTokenAsync(refreshTokenDto.RefreshToken);
                return Ok(response);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Tentative de rafraîchissement avec un token invalide");
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur serveur lors du rafraîchissement du token");
                return StatusCode(500, new { message = "Une erreur est survenue lors du rafraîchissement" });
            }
        }

        [HttpGet("me")]
        [Authorize]
        public IActionResult Me()
        {
            var sub = User.FindFirstValue("sub");
            var email = User.FindFirstValue(ClaimTypes.Email) ?? "";
            var role = User.FindFirstValue(ClaimTypes.Role) ?? "Employee";
            var authUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(sub, out var subjectId) || subjectId == Guid.Empty)
                return Unauthorized(new { message = "Claim sub manquant." });

            return Ok(new AuthMeDto
            {
                SubjectId = subjectId,
                AuthUserId = int.TryParse(authUserId, out var id) ? id : 0,
                Email = email,
                Role = role,
                TenantId = "atlas-tech-demo",
            });
        }

        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> Logout([FromBody] RefreshTokenDto refreshTokenDto)
        {
            try
            {
                var result = await _authService.LogoutAsync(refreshTokenDto.RefreshToken);
                if (result)
                    return Ok(new { message = "Déconnexion réussie" });
                return BadRequest(new { message = "Échec de la déconnexion" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur serveur lors de la déconnexion");
                return StatusCode(500, new { message = "Une erreur est survenue lors de la déconnexion" });
            }
        }

        [HttpGet("check-email")]
        [AllowAnonymous]
        public async Task<IActionResult> CheckEmail([FromQuery] string email)
        {
            try
            {
                var exists = await _authService.EmailExistsAsync(email);
                return Ok(new { exists });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la vérification de l'email");
                return StatusCode(500, new { message = "Erreur lors de la vérification" });
            }
        }

        [HttpGet("check-username")]
        [AllowAnonymous]
        public async Task<IActionResult> CheckUsername([FromQuery] string username)
        {
            try
            {
                var exists = await _authService.UsernameExistsAsync(username);
                return Ok(new { exists });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la vérification du username");
                return StatusCode(500, new { message = "Erreur lors de la vérification" });
            }
        }

        // ✅ 🆕 NOUVEL ENDPOINT
        [HttpPost("register-from-planning")]
        [AllowAnonymous]
        public async Task<IActionResult> RegisterFromPlanning(
            [FromBody] RegisterFromPlanningDto dto)
        {
            try
            {
                // Idempotent : déjà existant → retourner sans erreur
                var existing = await _userRepository.GetByEmailAsync(dto.Email);
                if (existing != null)
                {
                    _logger.LogWarning("⚠️ {Email} existe déjà dans Auth", dto.Email);
                    return Ok(new RegisterFromPlanningResponseDto
                    {
                        Id = existing.Id,
                        Email = existing.Email
                    });
                }

                // Vérifier le rôle
                var role = await _roleRepository.GetByIdAsync(dto.RoleId);
                if (role == null)
                {
                    _logger.LogWarning("⚠️ RoleId {RoleId} introuvable", dto.RoleId);
                    return BadRequest(new { message = $"Role {dto.RoleId} introuvable" });
                }

                // Créer le user avec le bon rôle
                var user = new User
                {
                    Username = dto.Email,
                    Email = dto.Email,
                    SubjectId = KyntusSubjectIdCatalog.ResolveForEmail(dto.Email),
                    PasswordHash = _passwordHasher.HashPassword(dto.DefaultPassword),
                    RoleId = role.Id,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    RefreshToken = null,
                    RefreshTokenExpiryTime = null
                };

                await _userRepository.CreateAsync(user);

                _logger.LogInformation("✅ User {Email} | Role {Role} créé depuis Planning",
                    dto.Email, role.Name);

                return Ok(new RegisterFromPlanningResponseDto
                {
                    Id = user.Id,
                    Email = user.Email
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Erreur création user depuis Planning");
                return StatusCode(500, new { message = "Erreur serveur" });
            }
        }
    }
}