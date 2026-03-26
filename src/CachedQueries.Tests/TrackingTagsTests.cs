using CachedQueries.Internal;
using FluentAssertions;
using Xunit;

namespace CachedQueries.Tests;

public class TrackingTagsTests
{
    [Fact]
    public void EntityTag_WithoutContext_ShouldIncludeTagMarker()
    {
        var tag = TrackingTags.EntityTag(typeof(Order), null);
        tag.Should().Be($"tag:{typeof(Order).FullName}");
    }

    [Fact]
    public void EntityTag_WithContext_ShouldPutContextBeforeTagMarker()
    {
        var tag = TrackingTags.EntityTag(typeof(Order), "tenant-1");
        tag.Should().Be($"tenant-1:tag:{typeof(Order).FullName}");
    }

    [Fact]
    public void UserTag_WithoutContext_ShouldIncludeTagMarker()
    {
        var tag = TrackingTags.UserTag("orders", null);
        tag.Should().Be("tag:orders");
    }

    [Fact]
    public void UserTag_WithContext_ShouldPutContextBeforeTagMarker()
    {
        var tag = TrackingTags.UserTag("orders", "tenant-1");
        tag.Should().Be("tenant-1:tag:orders");
    }

    [Fact]
    public void BuildTrackingTags_WithExplicitTags_ShouldUseOnlyExplicitTags()
    {
        var tags = TrackingTags.BuildTrackingTags(
            [typeof(Order)],
            ["my-tag"],
            null);

        tags.Should().HaveCount(1);
        tags.Should().Contain("tag:my-tag");
        tags.Should().NotContain($"tag:{typeof(Order).FullName}");
    }

    [Fact]
    public void BuildTrackingTags_WithoutExplicitTags_ShouldUseEntityTypeTags()
    {
        var tags = TrackingTags.BuildTrackingTags(
            [typeof(Order)],
            [],
            null);

        tags.Should().HaveCount(1);
        tags.Should().Contain($"tag:{typeof(Order).FullName}");
    }

    [Fact]
    public void BuildTrackingTags_WithContext_AndExplicitTags_ShouldUseOnlyExplicitTags()
    {
        var tags = TrackingTags.BuildTrackingTags(
            [typeof(Order)],
            ["my-tag"],
            "tenant-1");

        tags.Should().HaveCount(1);
        tags.Should().Contain("tenant-1:tag:my-tag");
        tags.Should().NotContain($"tenant-1:tag:{typeof(Order).FullName}");
    }

    [Fact]
    public void BuildTrackingTags_WithContext_AndNoExplicitTags_ShouldScopeToContext()
    {
        var tags = TrackingTags.BuildTrackingTags(
            [typeof(Order)],
            [],
            "tenant-1");

        tags.Should().HaveCount(1);
        tags.Should().Contain($"tenant-1:tag:{typeof(Order).FullName}");
        tags.Should().NotContain($"tag:{typeof(Order).FullName}");
    }

    [Fact]
    public void BuildTrackingTags_WithNoEntityTypesOrTags_AndContext_ShouldReturnEmpty()
    {
        var tags = TrackingTags.BuildTrackingTags(
            [],
            [],
            "tenant-1");

        tags.Should().BeEmpty();
    }

    [Fact]
    public void BuildTrackingTags_WithNoEntityTypesOrTags_AndNoContext_ShouldReturnEmpty()
    {
        var tags = TrackingTags.BuildTrackingTags([], [], null);
        tags.Should().BeEmpty();
    }

    [Fact]
    public void InvalidationTagsForEntityTypes_WithoutContext_ShouldReturnOnlyGlobalTags()
    {
        var tags = TrackingTags.InvalidationTagsForEntityTypes(
            [typeof(Order), typeof(Customer)],
            null);

        tags.Should().HaveCount(2);
        tags.Should().Contain($"tag:{typeof(Order).FullName}");
        tags.Should().Contain($"tag:{typeof(Customer).FullName}");
    }

    [Fact]
    public void InvalidationTagsForEntityTypes_WithContext_ShouldReturnGlobalAndContextTags()
    {
        var tags = TrackingTags.InvalidationTagsForEntityTypes(
            [typeof(Order)],
            "tenant-1");

        tags.Should().HaveCount(2);
        tags.Should().Contain($"tag:{typeof(Order).FullName}");
        tags.Should().Contain($"tenant-1:tag:{typeof(Order).FullName}");
    }

    [Fact]
    public void InvalidationTagsForUserTags_WithoutContext_ShouldReturnTagsWithMarker()
    {
        var tags = TrackingTags.InvalidationTagsForUserTags(
            ["orders", "items"],
            null);

        tags.Should().HaveCount(2);
        tags.Should().Contain("tag:orders");
        tags.Should().Contain("tag:items");
    }

    [Fact]
    public void InvalidationTagsForUserTags_WithContext_ShouldReturnGlobalAndContextTags()
    {
        var tags = TrackingTags.InvalidationTagsForUserTags(
            ["orders"],
            "tenant-1");

        tags.Should().HaveCount(2);
        tags.Should().Contain("tag:orders");
        tags.Should().Contain("tenant-1:tag:orders");
    }
}
