using Diploma.Application.Interfaces;
using Diploma.Domain.Interfaces;
using Diploma.Domain.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace Diploma.Infrastructure.Persistence;

public class ApplicationDbContext : IdentityDbContext
{
    private readonly ICurrentUserService _currentUserService;

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, ICurrentUserService currentUserService)
        : base(options)
    {
        _currentUserService = currentUserService;
    }

    public DbSet<Document> Documents { get; set; } = null!;
    public DbSet<DocumentChunk> DocumentChunks { get; set; } = null!;
    public DbSet<ChatSession> ChatSessions { get; set; } = null!;
    public DbSet<ChatMessage> ChatMessages { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // --- Document Configuration ---
        builder.Entity<Document>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired();
            entity.HasIndex(e => e.UserId);
            
            entity.HasMany(d => d.Chunks)
                  .WithOne(c => c.Document)
                  .HasForeignKey(c => c.DocumentId)
                  .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasQueryFilter(e => e.UserId == _currentUserService.UserId);
        });

        // --- DocumentChunk Configuration ---
        builder.Entity<DocumentChunk>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired();
            entity.HasIndex(e => e.UserId);

            entity.HasQueryFilter(e => e.UserId == _currentUserService.UserId);
        });

        // --- ChatSession Configuration ---
        builder.Entity<ChatSession>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired();
            entity.HasIndex(e => e.UserId);

            entity.HasMany(s => s.Messages)
                  .WithOne(m => m.Session)
                  .HasForeignKey(m => m.ChatSessionId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasQueryFilter(e => e.UserId == _currentUserService.UserId);
        });

        // --- ChatMessage Configuration ---
        builder.Entity<ChatMessage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired();
            entity.HasIndex(e => e.UserId);
            entity.Property(e => e.Role).IsRequired();

            entity.HasQueryFilter(e => e.UserId == _currentUserService.UserId);
            
        });
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<IMultiTenant>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.UserId = _currentUserService.UserId
                    ?? throw new InvalidOperationException("Current user ID is required for multi-tenant entities.");
            }
        }
        return base.SaveChangesAsync(cancellationToken);
    }
}
