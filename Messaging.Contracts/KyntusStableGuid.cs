using System.Security.Cryptography;
using System.Text;

namespace Kyntus.Messaging.Contracts;

public static class KyntusStableGuid
{
    public static Guid FromSeed(string namespaceSeed, string value)
    {
        var hash = MD5.HashData(Encoding.UTF8.GetBytes($"{namespaceSeed}:{value}"));
        return new Guid(hash);
    }

    public static Guid FromPrimeOrgId(string primeId) => FromSeed("kyntus-org", primeId);
}
