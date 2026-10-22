using JobMatch.Domain;
using JobMatch.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace JobMatch.Infrastructure.Tests;

internal sealed class TestJobMatchDbContext(
    DbContextOptions<JobMatchDbContext> options)
    : JobMatchDbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<JobApplication>()
            .Property(application => application.ResumeSnapshot)
            .HasConversion(
                document => document.RootElement.GetRawText(),
                json => JsonDocument.Parse(json, new JsonDocumentOptions()));
    }
}
