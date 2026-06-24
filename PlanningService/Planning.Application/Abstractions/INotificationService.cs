// src/Interfaces/INotificationService.cs
namespace Planning.Application.Abstractions
{
    public interface IReclamationNotificationService
    {
        // Notifier l'auteur
        Task NotifyAuteurAsync(string auteurId, string titre, string message, string type);
        // Notifier tous les gestionnaires
        Task NotifyManagersAsync(string titre, string message, string type);
    }
}