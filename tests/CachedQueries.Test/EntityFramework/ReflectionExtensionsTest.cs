using AutoFixture;
using CachedQueries.EntityFramework.Extensions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace CachedQueries.Test.EntityFramework;

public sealed class ReflectExtensionsTest
{
    private readonly Mock<Func<TestDbContext>> _contextFactoryMock;
    private readonly Fixture _fixture;

    public ReflectExtensionsTest()
    {
        _fixture = new Fixture();
        _contextFactoryMock = new Mock<Func<TestDbContext>>();
        _contextFactoryMock.Setup(x => x()).Returns(() =>
        {
            var options = new DbContextOptionsBuilder<TestDbContext>()
                .UseInMemoryDatabase(_fixture.Create<string>())
                .Options;
            var context = new TestDbContext(options);
            return context;
        });

        var services = new ServiceCollection();

        services.AddMemoryCache();
        services.AddSingleton<ILoggerFactory, NullLoggerFactory>();
    }

    [Fact]
    public async Task GetIncludeTypes_Should_Return_Include_Types()
    {
        // Given
        await using var context = _contextFactoryMock.Object();
        var entities = _fixture.CreateMany<Blog>(20).ToList();
        context.Blogs.AddRange(entities);
        await context.SaveChangesAsync();

        // When
        var queryLinq = context.Blogs
            .Include(x => x.Posts)
            .Include(x => x.Author)
            .Where(x => x.Id > 0);

        var querySyntax = from blog in context.Blogs
            join author in context.Authors on blog.AuthorId equals author.Id
            join post in context.Posts on blog.Id equals post.Id
            where blog.Id > 0
            select new
            {
                blog, author
            };

        var typesFromLinq = queryLinq.GetIncludeTypes().ToList();
        var typesFromQuerySyntax = querySyntax.GetIncludeTypes().ToList();

        // Then
        typesFromLinq.Should().HaveCount(3);
        typesFromLinq.Should().Contain(typeof(Blog));
        typesFromLinq.Should().Contain(typeof(Author));
        typesFromLinq.Should().Contain(typeof(Post));

        typesFromQuerySyntax.Should().HaveCount(3);
        typesFromQuerySyntax.Should().Contain(typeof(Blog));
        typesFromQuerySyntax.Should().Contain(typeof(Author));
        typesFromQuerySyntax.Should().Contain(typeof(Post));
    }

    [Fact]
    public async Task GetIncludeTypes_Should_Return_Include_And_ThenInclude_Types()
    {
        // Given
        await using var context = _contextFactoryMock.Object();
        var entities = _fixture.CreateMany<Blog>(20).ToList();
        context.Blogs.AddRange(entities);
        await context.SaveChangesAsync();

        // When
        var queryLinq = context.Blogs
            .Include(x => x.Posts)
            .ThenInclude(x => x.Comments)
            .Include(x => x.Author)
            .IgnoreQueryFilters()
            .Where(x => x.Id > 0);

        var querySyntax = from blog in context.Blogs
            join author in context.Authors on blog.AuthorId equals author.Id
            join post in context.Posts on blog.Id equals post.Id
            join comment in context.Comments on post.Id equals comment.Id
            where blog.Id > 0
            select new
            {
                blog, author
            };

        var typesFromLinq = queryLinq.GetIncludeTypes().ToList();
        var typesFromQuerySyntax = querySyntax.GetIncludeTypes().ToList();

        // Then
        typesFromLinq.Should().HaveCount(4);
        typesFromLinq.Should().Contain(typeof(Blog));
        typesFromLinq.Should().Contain(typeof(Author));
        typesFromLinq.Should().Contain(typeof(Comment));
        typesFromLinq.Should().Contain(typeof(Post));

        typesFromQuerySyntax.Should().HaveCount(4);
        typesFromQuerySyntax.Should().Contain(typeof(Blog));
        typesFromQuerySyntax.Should().Contain(typeof(Author));
        typesFromQuerySyntax.Should().Contain(typeof(Comment));
        typesFromQuerySyntax.Should().Contain(typeof(Post));
    }

    [Fact]
    public async Task GetIncludeTypes_Should_Return_Include_And_ThenInclude_Types_With_Filters()
    {
        // Given
        await using var context = _contextFactoryMock.Object();
        var entities = _fixture.CreateMany<Blog>(20).ToList();
        context.Blogs.AddRange(entities);
        await context.SaveChangesAsync();

        // When
        var query = context.Blogs
            .Include(x => x.Posts.Where(p => !string.IsNullOrEmpty(p.Text)))
            .ThenInclude(x => x.Comments.Where(p => !string.IsNullOrEmpty(p.Text)))
            .Include(x => x.Author)
            .IgnoreQueryFilters()
            .Where(x => x.Id > 0)
            .Select(x => x.Name);

        var types = query.GetIncludeTypes().ToList();

        // Then
        types.Should().HaveCount(4);
        types.Should().Contain(typeof(Blog));
        types.Should().Contain(typeof(Author));
        types.Should().Contain(typeof(Comment));
        types.Should().Contain(typeof(Post));
    }

    [Fact]
    public async Task GetIncludeTypes_Should_Return_Root_Type_If_Include_Not_In_Use()
    {
        // Given
        await using var context = _contextFactoryMock.Object();
        var entities = _fixture.CreateMany<Blog>(20).ToList();
        context.Blogs.AddRange(entities);
        await context.SaveChangesAsync();

        // When
        var types = context.Blogs.GetIncludeTypes().ToList();

        // Then
        types.Should().HaveCount(1);
        types.Should().Contain(typeof(Blog));
    }

    [Fact]
    public async Task
        GetIncludeTypes_Should_Return_Root_Type_If_Include_Not_In_Use_And_There_Are_Other_Expression_Defined()
    {
        // Given
        await using var context = _contextFactoryMock.Object();
        var entities = _fixture.CreateMany<Blog>(20).ToList();
        context.Blogs.AddRange(entities);
        await context.SaveChangesAsync();

        // When
        var types = context.Blogs.Where(x => x.Id > 0).Select(x => x.Name).GetIncludeTypes().ToList();

        // Then
        types.Should().HaveCount(1);
        types.Should().Contain(typeof(Blog));
    }
}
