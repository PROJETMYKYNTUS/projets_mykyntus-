namespace Planning.Application.Exceptions;

/// <summary>
/// Génération d’une semaine future bloquée : le superviseur n’a pas encore
/// enregistré les modes et la deadline (veille de l’auto-gen RH) n’est pas passée.
/// </summary>
public class SupervisorModesPendingException : InvalidOperationException
{
    public SupervisorModesPendingException(string message) : base(message)
    {
    }
}
