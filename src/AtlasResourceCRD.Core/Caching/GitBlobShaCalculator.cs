using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace AtlasResourceCRD.Core.Caching;

public static class GitBlobShaCalculator
{
    /// <summary>
    /// Computes the exact Git blob SHA-1 hash for raw file bytes matching Git's internal format:
    /// SHA1("blob " + length + "\0" + content)
    /// </summary>
    public static string ComputeBlobSha(byte[] content)
    {
        var header = Encoding.ASCII.GetBytes($"blob {content.Length}\0");
        var combined = new byte[header.Length + content.Length];

        Buffer.BlockCopy(header, 0, combined, 0, header.Length);
        Buffer.BlockCopy(content, 0, combined, header.Length, content.Length);

        var hashBytes = SHA1.HashData(combined);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    /// <summary>
    /// Computes the Git blob SHA-1 hash for a file on disk.
    /// </summary>
    public static string ComputeBlobShaForFile(string filePath)
    {
        var bytes = File.ReadAllBytes(filePath);
        return ComputeBlobSha(bytes);
    }

    /// <summary>
    /// Computes the Git blob SHA-1 hash for a text string (UTF-8).
    /// </summary>
    public static string ComputeBlobShaForText(string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        return ComputeBlobSha(bytes);
    }
}
