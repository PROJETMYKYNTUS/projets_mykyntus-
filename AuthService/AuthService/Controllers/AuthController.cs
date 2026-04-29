using AuthService.DTO;

using AuthService.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuthService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private const string AccessTokenCookieName = "kyntus_access_token";
        private const string RefreshTokenCookieName = "kyntus_refresh_token";
        private readonly IAuthService _authService;
        private readonly ILogger<AuthController> _logger;

        public AuthController(IAuthService authService, ILogger<AuthController> logger)
        {
            _authService = authService;
            _logger = logger;
        }

        /// <summary>
        /// Inscription d'un nouvel utilisateur
        /// </summary>
        [HttpPost("register")]
        [AllowAnonymous]
        public async Task<IActionResult> Register([FromBody] RegisterDto registerDto)
        {
            try
            {
                var response = await _authService.RegisterAsync(registerDto);
                SetAuthCookies(response);
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

        /// <summary>
        /// Connexion d'un utilisateur
        /// </summary>
        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody] LoginDto loginDto)
        {
            try
            {
                var response = await _authService.LoginAsync(loginDto);
                SetAuthCookies(response);
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

        /// <summary>
        /// Rafraîchir le token d'accès
        /// </summary>
        [HttpPost("refresh")]
        [AllowAnonymous]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenDto refreshTokenDto)
        {
            try
            {
                var response = await _authService.RefreshTokenAsync(refreshTokenDto.RefreshToken);
                SetAuthCookies(response);
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

        /// <summary>
        /// Déconnexion (révocation du refresh token)
        /// </summary>
        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> Logout([FromBody] RefreshTokenDto refreshTokenDto)
        {
            try
            {
                var result = await _authService.LogoutAsync(refreshTokenDto.RefreshToken);
                ClearAuthCookies();
                if (result)
                {
                    return Ok(new { message = "Déconnexion réussie" });
                }
                return BadRequest(new { message = "Échec de la déconnexion" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur serveur lors de la déconnexion");
                return StatusCode(500, new { message = "Une erreur est survenue lors de la déconnexion" });
            }
        }

        private void SetAuthCookies(AuthResponseDto response)
        {
            var accessTokenCookie = new CookieOptions
            {
                HttpOnly = true,
                Secure = Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                Expires = DateTimeOffset.UtcNow.AddSeconds(response.ExpiresIn),
                Path = "/"
            };
            Response.Cookies.Append(AccessTokenCookieName, response.AccessToken, accessTokenCookie);

            var refreshTokenCookie = new CookieOptions
            {
                HttpOnly = true,
                Secure = Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                Expires = DateTimeOffset.UtcNow.AddDays(7),
                Path = "/"
            };
            Response.Cookies.Append(RefreshTokenCookieName, response.RefreshToken, refreshTokenCookie);
        }

        private void ClearAuthCookies()
        {
            Response.Cookies.Delete(AccessTokenCookieName, new CookieOptions { Path = "/" });
            Response.Cookies.Delete(RefreshTokenCookieName, new CookieOptions { Path = "/" });
        }

        /// <summary>
        /// Vérifier la disponibilité d'un email
        /// </summary>
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

        /// <summary>
        /// Vérifier la disponibilité d'un nom d'utilisateur
        /// </summary>
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
    }
}