using AuthService.DTO;
using AuthService.Interfaces;
using AuthService.Models;

namespace AuthService.Services
{
    /// <summary>
    /// Service d'authentification
    /// </summary>
    public class AuthenticationService : IAuthService
    {
        private readonly IUserRepository _userRepository;
        private readonly IRoleRepository _roleRepository;
        private readonly IJwtService _jwtService;
        private readonly IPasswordHasher _passwordHasher;

        public AuthenticationService(
            IUserRepository userRepository,
            IRoleRepository roleRepository,
            IJwtService jwtService,
            IPasswordHasher passwordHasher)
        {
            _userRepository = userRepository;
            _roleRepository = roleRepository;
            _jwtService = jwtService;
            _passwordHasher = passwordHasher;
        }

        public async Task<AuthResponseDto> RegisterAsync(RegisterDto registerDto)
        {
            // Vérifier si l'email existe déjà
            if (await _userRepository.ExistsAsync(registerDto.Email))
            {
                throw new InvalidOperationException("Cet email est déjà utilisé");
            }

            // Vérifier si le username existe déjà
            if (await _userRepository.UsernameExistsAsync(registerDto.Username))
            {
                throw new InvalidOperationException("Ce nom d'utilisateur est déjà pris");
            }

            // Récupérer le rôle par défaut (User)
            var role = await _roleRepository.GetByNameAsync("Employee");
            if (role == null)
            {
                throw new InvalidOperationException("Le rôle par défaut 'User' n'existe pas");
            }

            // Créer le nouvel utilisateur
            var user = new User
            {
                Username = registerDto.Username,
                Email = registerDto.Email,
                PasswordHash = _passwordHasher.HashPassword(registerDto.Password),
                RoleId = role.Id,
                RefreshToken = _jwtService.GenerateRefreshToken(),
                RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7)
            };

            // Sauvegarder l'utilisateur
            await _userRepository.CreateAsync(user);

            // Charger le rôle pour la génération du token
            user.Role = role;

            // Générer les tokens
            var accessToken = _jwtService.GenerateAccessToken(user);

            return new AuthResponseDto
            {
                AccessToken = accessToken,
                RefreshToken = user.RefreshToken,
                ExpiresIn = 900, // 15 minutes en secondes
                TokenType = "Bearer",
                User = new UserDto
                {
                    Id = user.Id,
                    Username = user.Username,
                    Email = user.Email,
                    Role = user.Role.Name
                }
            };
        }

        public async Task<AuthResponseDto> LoginAsync(LoginDto loginDto)
        {
            // Récupérer l'utilisateur par email
            var user = await _userRepository.GetByEmailAsync(loginDto.Email);
            if (user == null)
            {
                throw new UnauthorizedAccessException("Email ou mot de passe incorrect");
            }

            // Vérifier le mot de passe
            if (!_passwordHasher.VerifyPassword(user.PasswordHash, loginDto.Password))
            {
                throw new UnauthorizedAccessException("Email ou mot de passe incorrect");
            }

            // Générer un nouveau refresh token
            user.RefreshToken = _jwtService.GenerateRefreshToken();
            user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
            await _userRepository.UpdateAsync(user);

            // Générer l'access token
            var accessToken = _jwtService.GenerateAccessToken(user);

            return new AuthResponseDto
            {
                AccessToken = accessToken,
                RefreshToken = user.RefreshToken,
                ExpiresIn = 900, // 15 minutes
                TokenType = "Bearer",
                User = new UserDto
                {
                    Id = user.Id,
                    Username = user.Username,
                    Email = user.Email,
                    Role = user.Role?.Name ?? "Employee"
                }
            };
        }

        public async Task<AuthResponseDto> RefreshTokenAsync(string refreshToken)
        {
            // Trouver l'utilisateur avec ce refresh token
            var users = await _userRepository.GetAllAsync();
            var user = users.FirstOrDefault(u => u.RefreshToken == refreshToken);

            if (user == null || user.RefreshTokenExpiryTime <= DateTime.UtcNow)
            {
                throw new UnauthorizedAccessException("Refresh token invalide ou expiré");
            }

            // Générer un nouveau refresh token
            user.RefreshToken = _jwtService.GenerateRefreshToken();
            user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
            await _userRepository.UpdateAsync(user);

            // Générer un nouveau access token
            var accessToken = _jwtService.GenerateAccessToken(user);

            return new AuthResponseDto
            {
                AccessToken = accessToken,
                RefreshToken = user.RefreshToken,
                ExpiresIn = 900,
                TokenType = "Bearer",
                User = new UserDto
                {
                    Id = user.Id,
                    Username = user.Username,
                    Email = user.Email,
                    Role = user.Role?.Name ?? "Employee"
                }
            };
        }

        public async Task<bool> LogoutAsync(string refreshToken)
        {
            var users = await _userRepository.GetAllAsync();
            var user = users.FirstOrDefault(u => u.RefreshToken == refreshToken);

            if (user == null)
                return false;

            // Révoquer le refresh token
            user.RefreshToken = null;
            user.RefreshTokenExpiryTime = null;
            await _userRepository.UpdateAsync(user);

            return true;
        }

        public async Task<bool> EmailExistsAsync(string email)
        {
            return await _userRepository.ExistsAsync(email);
        }

        public async Task<bool> UsernameExistsAsync(string username)
        {
            return await _userRepository.UsernameExistsAsync(username);
        }
    }
}