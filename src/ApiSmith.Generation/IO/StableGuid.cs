using System.Security.Cryptography;
using System.Text;

namespace ApiSmith.Generation.IO;

public static class StableGuid
{
    /// <summary>SHA-256-derived deterministic GUID; keeps <c>.sln</c> project GUIDs byte-stable across replays.</summary>
    public static System.Guid From(string seed)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(seed));
        var bytes = new byte[16];
        System.Array.Copy(hash, bytes, 16);

        // Stamp as version-5 layout for tooling that inspects it.
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);

        return new System.Guid(bytes);
    }
}
