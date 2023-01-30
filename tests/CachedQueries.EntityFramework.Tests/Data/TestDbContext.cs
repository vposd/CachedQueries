using System.Collections.Generic;
using CachedQueries.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CachedQueries.EntityFramework.Tests.Data;

public class Blog
{
    public long Id { get; set; }
    public string? Name { get; set; }
    public long AuthorId { get; set; }
    public Author Author { get; set; }
    public ICollection<Post> Posts { get; set; }
}

public class Post
{
    public long Id { get; set; }
    public string? Text { get; set; }
    public ICollection<Comment> Comments { get; set; }
}

public class Author
{
    public long Id { get; set; }
    public string? Name { get; set; }
}

public class Comment
{
    public long Id { get; set; }
    public string? Text { get; set; }
}

public class TestDbContext : DbContext, ICachedContext
{
    public TestDbContext(DbContextOptions<TestDbContext> options) : base(options)
    {
    }

    public TestDbContext(DbContextOptions<TestDbContext> options, ICacheManager cacheManager) : base(options)
    {
        CacheManager = cacheManager;
    }

    public virtual DbSet<Post> Posts { get; set; }
    public virtual DbSet<Comment> Comments { get; set; }
    public virtual DbSet<Author> Authors { get; set; }
    public virtual DbSet<Blog> Blogs { get; set; }
    public ICacheManager CacheManager { get; }
}
