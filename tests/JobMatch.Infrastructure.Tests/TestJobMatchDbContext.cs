using JobMatch.Domain;
using JobMatch.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JobMatch.Infrastructure.Tests;

internal sealed class TestJobMatchDbContext(
    DbContextOptions<JobMatchDbContext> options)
    : JobMatchDbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<JobApplication>().Ignore(application => application.ResumeSnapshot);
    }
}
