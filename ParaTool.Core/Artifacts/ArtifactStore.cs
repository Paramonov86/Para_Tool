using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ParaTool.Core.Services;

namespace ParaTool.Core.Artifacts;

/// <summary>
/// Manages the user's artifact collection.
/// Artifacts are stored as individual .art files (AES-256 encrypted JSON) in a dedicated folder.
/// Location: %LocalAppData%/ParaTool/Artifacts/
/// </summary>
public static class ArtifactStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    // AES-256 key derived from a passphrase (obfuscated in binary)
    private static readonly byte[] AesKey = DeriveKey("P4r4T00l_Anc13nt_Arm0ury_2024!@#");
    private static readonly byte[] Magic = "PART"u8.ToArray(); // file header magic

    /// <summary>
    /// Gets the artifacts storage directory.
    /// </summary>
    public static string GetArtifactsDir()
    {
        var dir = Path.Combine(ProfileService.GetStorageDir(), "Artifacts");
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>
    /// Save an artifact definition to disk (encrypted).
    /// </summary>
    public static void Save(ArtifactDefinition artifact)
    {
        artifact.ModifiedAt = DateTime.UtcNow;

        if (string.IsNullOrEmpty(artifact.DisplayNameHandle))
            artifact.DisplayNameHandle = Localization.HandleGenerator.New();
        if (string.IsNullOrEmpty(artifact.DescriptionHandle))
            artifact.DescriptionHandle = Localization.HandleGenerator.New();

        foreach (var p in artifact.Passives)
        {
            if (string.IsNullOrEmpty(p.DisplayNameHandle)) p.DisplayNameHandle = Localization.HandleGenerator.New();
            if (string.IsNullOrEmpty(p.DescriptionHandle)) p.DescriptionHandle = Localization.HandleGenerator.New();
        }
        foreach (var s in artifact.Statuses)
        {
            if (string.IsNullOrEmpty(s.DisplayNameHandle)) s.DisplayNameHandle = Localization.HandleGenerator.New();
            if (string.IsNullOrEmpty(s.DescriptionHandle)) s.DescriptionHandle = Localization.HandleGenerator.New();
        }
        foreach (var sp in artifact.Spells)
        {
            if (string.IsNullOrEmpty(sp.DisplayNameHandle)) sp.DisplayNameHandle = Localization.HandleGenerator.New();
            if (string.IsNullOrEmpty(sp.DescriptionHandle)) sp.DescriptionHandle = Localization.HandleGenerator.New();
        }

        var path = GetArtifactPath(artifact.ArtifactId);
        var tmpPath = path + ".tmp";
        var json = JsonSerializer.Serialize(artifact, JsonOptions);
        var encrypted = Encrypt(Encoding.UTF8.GetBytes(json));
        File.WriteAllBytes(tmpPath, encrypted);
        File.Move(tmpPath, path, overwrite: true);
    }

    /// <summary>
    /// Load a single artifact by ID.
    /// </summary>
    public static ArtifactDefinition? Load(string artifactId)
    {
        var path = GetArtifactPath(artifactId);
        if (!File.Exists(path)) return null;

        var json = ReadArtFile(path);
        if (json == null) return null;
        return JsonSerializer.Deserialize<ArtifactDefinition>(json, JsonOptions);
    }

    /// <summary>
    /// Load all artifacts from the store.
    /// </summary>
    public static List<ArtifactDefinition> LoadAll()
    {
        var dir = GetArtifactsDir();
        var result = new List<ArtifactDefinition>();

        foreach (var file in Directory.GetFiles(dir, "*.art"))
        {
            try
            {
                var json = ReadArtFile(file);
                if (json == null) continue;
                var artifact = JsonSerializer.Deserialize<ArtifactDefinition>(json, JsonOptions);
                if (artifact != null)
                    result.Add(artifact);
            }
            catch (Exception ex) { Services.AppLogger.Warn($"Skipping corrupt .art file {file}: {ex.Message}"); }
        }

        return result.OrderBy(a => a.StatId).ToList();
    }

    /// <summary>
    /// Delete an artifact by ID.
    /// </summary>
    public static void Delete(string artifactId)
    {
        var path = GetArtifactPath(artifactId);
        if (File.Exists(path))
            File.Delete(path);
    }

    private static string GetArtifactPath(string artifactId)
    {
        return Path.Combine(GetArtifactsDir(), $"{artifactId}.art");
    }

    // ── Crypto ─────────────────────────────────────────────────

    /// <summary>
    /// Read .art file — supports both encrypted (PART header) and legacy plain JSON.
    /// </summary>
    private static string? ReadArtFile(string path)
    {
        var data = File.ReadAllBytes(path);
        if (data.Length == 0) return null;

        // Check for encrypted format (PART magic + IV + ciphertext)
        if (data.Length > 4 + 16 && data[0] == Magic[0] && data[1] == Magic[1]
            && data[2] == Magic[2] && data[3] == Magic[3])
        {
            var decrypted = Decrypt(data);
            return decrypted != null ? Encoding.UTF8.GetString(decrypted) : null;
        }

        // Legacy: plain JSON (starts with '{' or BOM)
        return Encoding.UTF8.GetString(data);
    }

    private static byte[] Encrypt(byte[] plaintext)
    {
        using var aes = Aes.Create();
        aes.Key = AesKey;
        aes.GenerateIV();

        using var ms = new MemoryStream();
        ms.Write(Magic); // 4 bytes header
        ms.Write(aes.IV); // 16 bytes IV

        using (var cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
        {
            cs.Write(plaintext);
            cs.FlushFinalBlock();
        }

        return ms.ToArray();
    }

    private static byte[]? Decrypt(byte[] data)
    {
        try
        {
            using var aes = Aes.Create();
            aes.Key = AesKey;

            // Skip magic (4) + read IV (16)
            var iv = new byte[16];
            Buffer.BlockCopy(data, 4, iv, 0, 16);
            aes.IV = iv;

            using var ms = new MemoryStream(data, 4 + 16, data.Length - 4 - 16);
            using var cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Read);
            using var result = new MemoryStream();
            cs.CopyTo(result);
            return result.ToArray();
        }
        catch
        {
            return null;
        }
    }

    private static byte[] DeriveKey(string passphrase)
    {
        // PBKDF2 with fixed salt for deterministic key
        var salt = "ParaTool_ArtifactSalt_v1"u8.ToArray();
        using var pbkdf2 = new Rfc2898DeriveBytes(passphrase, salt, 10000, HashAlgorithmName.SHA256);
        return pbkdf2.GetBytes(32); // AES-256
    }
}
