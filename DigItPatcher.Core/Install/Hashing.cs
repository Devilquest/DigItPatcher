using System.Security.Cryptography;

namespace DigItPatcher.Core.Install;

/// <summary>SHA-256 of a file path, shared by the install and the backup so both fingerprint files the same way.</summary>
internal static class Hashing
{
    /// <summary>Hashes the file at <paramref name="path"/>, or reports that it could not be read.</summary>
    internal static bool TryHash(string path, out string sha256)
    {
        sha256 = string.Empty;

        try
        {
            using var stream = File.OpenRead(path);
            sha256 = Convert.ToHexStringLower(SHA256.HashData(stream));
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
