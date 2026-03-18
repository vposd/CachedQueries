using FluentAssertions;
using Xunit;

namespace CachedQueries.Tests;

public class CacheOptionsBuilderTests
{
    [Fact]
    public void Build_WithDefaults_ShouldReturn30MinAbsoluteExpiration()
    {
        var builder = new CacheOptionsBuilder();
        var options = builder.Build();

        options.Expiration.Should().Be(TimeSpan.FromMinutes(30));
        options.UseSlidingExpiration.Should().BeFalse();
        options.CacheKey.Should().BeNull();
        options.Tags.Should().BeEmpty();
        options.SkipCache.Should().BeFalse();
        options.Target.Should().Be(CacheTarget.Auto);
    }

    [Fact]
    public void Expire_ShouldSetAbsoluteExpiration()
    {
        var options = new CacheOptionsBuilder()
            .Expire(TimeSpan.FromHours(2))
            .Build();

        options.Expiration.Should().Be(TimeSpan.FromHours(2));
        options.UseSlidingExpiration.Should().BeFalse();
    }

    [Fact]
    public void SlidingExpiration_ShouldSetSlidingMode()
    {
        var options = new CacheOptionsBuilder()
            .SlidingExpiration(TimeSpan.FromMinutes(10))
            .Build();

        options.Expiration.Should().Be(TimeSpan.FromMinutes(10));
        options.UseSlidingExpiration.Should().BeTrue();
    }

    [Fact]
    public void SlidingExpiration_AfterExpire_ShouldOverride()
    {
        var options = new CacheOptionsBuilder()
            .Expire(TimeSpan.FromHours(1))
            .SlidingExpiration(TimeSpan.FromMinutes(5))
            .Build();

        options.Expiration.Should().Be(TimeSpan.FromMinutes(5));
        options.UseSlidingExpiration.Should().BeTrue();
    }

    [Fact]
    public void Expire_AfterSlidingExpiration_ShouldOverride()
    {
        var options = new CacheOptionsBuilder()
            .SlidingExpiration(TimeSpan.FromMinutes(5))
            .Expire(TimeSpan.FromHours(1))
            .Build();

        options.Expiration.Should().Be(TimeSpan.FromHours(1));
        options.UseSlidingExpiration.Should().BeFalse();
    }

    [Fact]
    public void WithKey_ShouldSetCustomCacheKey()
    {
        var options = new CacheOptionsBuilder()
            .WithKey("my-key")
            .Build();

        options.CacheKey.Should().Be("my-key");
    }

    [Fact]
    public void WithTags_ShouldAddTags()
    {
        var options = new CacheOptionsBuilder()
            .WithTags("orders", "reports")
            .Build();

        options.Tags.Should().BeEquivalentTo(["orders", "reports"]);
    }

    [Fact]
    public void WithTags_CalledMultipleTimes_ShouldAccumulateTags()
    {
        var options = new CacheOptionsBuilder()
            .WithTags("orders")
            .WithTags("reports")
            .Build();

        options.Tags.Should().BeEquivalentTo(["orders", "reports"]);
    }

    [Fact]
    public void SkipIf_True_ShouldSetSkipCache()
    {
        var options = new CacheOptionsBuilder()
            .SkipIf(true)
            .Build();

        options.SkipCache.Should().BeTrue();
    }

    [Fact]
    public void SkipIf_False_ShouldNotSetSkipCache()
    {
        var options = new CacheOptionsBuilder()
            .SkipIf(false)
            .Build();

        options.SkipCache.Should().BeFalse();
    }

    [Fact]
    public void UseTarget_ShouldSetTarget()
    {
        var options = new CacheOptionsBuilder()
            .UseTarget(CacheTarget.Scalar)
            .Build();

        options.Target.Should().Be(CacheTarget.Scalar);
    }

    [Fact]
    public void IgnoreContext_ShouldSetFlag()
    {
        var options = new CacheOptionsBuilder()
            .IgnoreContext()
            .Build();

        options.IgnoreContext.Should().BeTrue();
    }

    [Fact]
    public void Build_WithDefaults_ShouldHaveIgnoreContextFalse()
    {
        var options = new CacheOptionsBuilder().Build();

        options.IgnoreContext.Should().BeFalse();
    }

    [Fact]
    public void FluentChaining_ShouldWorkWithAllOptions()
    {
        var options = new CacheOptionsBuilder()
            .Expire(TimeSpan.FromHours(1))
            .WithKey("my-key")
            .WithTags("tag1", "tag2")
            .UseTarget(CacheTarget.Collection)
            .IgnoreContext()
            .Build();

        options.Expiration.Should().Be(TimeSpan.FromHours(1));
        options.UseSlidingExpiration.Should().BeFalse();
        options.CacheKey.Should().Be("my-key");
        options.Tags.Should().HaveCount(2);
        options.Target.Should().Be(CacheTarget.Collection);
        options.SkipCache.Should().BeFalse();
        options.IgnoreContext.Should().BeTrue();
    }
}
