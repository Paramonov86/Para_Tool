using System.Text.Json;
using System.Text.Json.Serialization;
using ParaTool.Core.Services;

namespace ParaTool.Core.Artifacts;

/// <summary>
/// Manages the user's artifact collection.
/// Artifacts are stored as individual .art files (JSON) in a dedicated folder.
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
    /// Save an artifact definition to disk.
    /// Filename is based on ArtifactId for uniqueness.
    /// </summary>
    public static void Save(ArtifactDefinition artifact)
    {
        artifact.ModifiedAt = DateTime.UtcNow;

        if (string.IsNullOrEmpty(artifact.DisplayNameHandle))
            artifact.DisplayNameHandle = Localization.HandleGenerator.New();
        if (string.IsNullOrEmpty(artifact.DescriptionHandle))
            artifact.DescriptionHandle = Localization.HandleGenerator.New();

        // Ensure all passives/statuses/spells have handles
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
        var json = JsonSerializer.Serialize(artifact, JsonOptions);
        File.WriteAllText(path, json);
    }

    /// <summary>
    /// Load a single artifact by ID.
    /// </summary>
    public static ArtifactDefinition? Load(string artifactId)
    {
        var path = GetArtifactPath(artifactId);
        if (!File.Exists(path)) return null;

        var json = File.ReadAllText(path);
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
                var json = File.ReadAllText(file);
                var artifact = JsonSerializer.Deserialize<ArtifactDefinition>(json, JsonOptions);
                if (artifact != null)
                    result.Add(artifact);
            }
            catch { /* skip corrupt files */ }
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
}
