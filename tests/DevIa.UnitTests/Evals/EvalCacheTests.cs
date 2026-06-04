using DevIa.Evals.Caching;

namespace DevIa.UnitTests.Evals;

/// <summary>
/// Deterministic tests for the on-disk result cache: a hit must avoid the factory, a miss must
/// run it once and persist, and distinct keys must not collide.
/// </summary>
public class EvalCacheTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "devia-eval-cache-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public async Task Miss_runs_the_factory_once_and_persists()
    {
        var cache = new JsonFileResultCache(_dir);
        var calls = 0;

        var value = await cache.GetOrAddAsync("k1", () => { calls++; return Task.FromResult(42); });

        Assert.Equal(42, value);
        Assert.Equal(1, calls);
        Assert.Equal(1, cache.Misses);
        Assert.Equal(0, cache.Hits);
    }

    [Fact]
    public async Task Hit_returns_cached_value_without_calling_the_factory()
    {
        var cache = new JsonFileResultCache(_dir);
        await cache.GetOrAddAsync("k1", () => Task.FromResult("first"));

        // A fresh instance over the same directory still hits (cache is on disk).
        var second = new JsonFileResultCache(_dir);
        var calls = 0;
        var value = await second.GetOrAddAsync("k1", () => { calls++; return Task.FromResult("second"); });

        Assert.Equal("first", value);
        Assert.Equal(0, calls);
        Assert.Equal(1, second.Hits);
    }

    [Fact]
    public async Task Distinct_keys_do_not_collide()
    {
        var cache = new JsonFileResultCache(_dir);

        var a = await cache.GetOrAddAsync("ka", () => Task.FromResult(1));
        var b = await cache.GetOrAddAsync("kb", () => Task.FromResult(2));

        Assert.Equal(1, a);
        Assert.Equal(2, b);
        Assert.Equal(2, cache.Misses);
    }

    [Fact]
    public async Task Caches_complex_records_round_trip()
    {
        var cache = new JsonFileResultCache(_dir);
        var original = new Sample("hello", [1, 2, 3]);

        await cache.GetOrAddAsync("rec", () => Task.FromResult(original));
        var restored = await cache.GetOrAddAsync("rec", () => Task.FromResult(new Sample("DIFFERENT", [])));

        Assert.Equal(original.Name, restored.Name);
        Assert.Equal(original.Values, restored.Values); // sequence equality
    }

    private sealed record Sample(string Name, int[] Values);
}
