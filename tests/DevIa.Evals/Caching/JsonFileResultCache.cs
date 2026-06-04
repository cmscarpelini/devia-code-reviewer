using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace DevIa.Evals.Caching;

/// <summary>
/// A tiny content-addressed JSON cache on disk: one file per key (SHA-256 of the key). Used to
/// avoid re-spending LLM calls when re-running the harness with the same prompt version + model
/// (ADR-0005 cost control). Keys are rich strings (e.g. "v1|model|&lt;diff&gt;") hashed for the filename.
/// </summary>
public sealed class JsonFileResultCache(string directory)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public int Hits { get; private set; }
    public int Misses { get; private set; }

    public async Task<T> GetOrAddAsync<T>(string key, Func<Task<T>> factory, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, Hash(key) + ".json");

        if (File.Exists(path))
        {
            var cached = JsonSerializer.Deserialize<T>(await File.ReadAllTextAsync(path, cancellationToken), JsonOptions);
            if (cached is not null)
            {
                Hits++;
                return cached;
            }
        }

        Misses++;
        var value = await factory();
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(value, JsonOptions), cancellationToken);
        return value;
    }

    private static string Hash(string key) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key))).ToLowerInvariant();
}
