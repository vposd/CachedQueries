using Microsoft.EntityFrameworkCore;

namespace CachedQueries.Test;

public class Root
{
    public long Id { get; set; }
}

public class Blog : Root
{
    public string? Name { get; set; }
    public long AuthorId { get; set; }
    public Author Author { get; set; }
    public ICollection<Post> Posts { get; set; }
}

public class Post : Root
{
    public string? Text { get; set; }
    public ICollection<Comment> Comments { get; set; }
}

public class Author : Root
{
    public string? Name { get; set; }
}

public class Comment
{
    public long Id { get; set; }
    public string? Text { get; set; }
}

public class TestDbContext : DbContext
{
    public TestDbContext(DbContextOptions<TestDbContext> options) : base(options)
    {
    }

    public virtual DbSet<Post> Posts { get; set; }
    public virtual DbSet<Comment> Comments { get; set; }
    public virtual DbSet<Author> Authors { get; set; }
    public virtual DbSet<Blog> Blogs { get; set; }
}
