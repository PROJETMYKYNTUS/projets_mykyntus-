using Microsoft.AspNetCore.Identity;

// Même algorithme que AuthService.Helpers.PasswordHasher (Identity PasswordHasher<object>).
var pwd = args.Length > 0 ? args[0] : "DocAlign!2026";
var hash = new PasswordHasher<object>().HashPassword(null!, pwd);
Console.WriteLine(hash);
