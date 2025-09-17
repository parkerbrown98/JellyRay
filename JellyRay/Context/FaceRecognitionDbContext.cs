using System.Runtime.CompilerServices;
using JellyRay.Entities;
using Microsoft.EntityFrameworkCore;

public class FaceRecognitionDbContext : DbContext
{
    private readonly string _dbPath;

    public DbSet<FaceRecognitionResult> Results { get; set; }

    public FaceRecognitionDbContext(string dbPath)
    {
        _dbPath = dbPath;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) => optionsBuilder.UseSqlite($"Data Source={_dbPath}");
}

