using DungeonServer.Infrastructure.EntityFramework.Entities;
using Microsoft.EntityFrameworkCore;

namespace DungeonServer.Infrastructure.EntityFramework;

public class DungeonDbContext(DbContextOptions options) : DbContext(options)
{
    public DbSet<PlayerEntity> Players => Set<PlayerEntity>();
    public DbSet<RoomEntity> Rooms => Set<RoomEntity>();
    public DbSet<RoomExitEntity> RoomExits => Set<RoomExitEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Composite Key: Exits are unique to a Room + Direction.
        modelBuilder.Entity<RoomExitEntity>().HasKey(e => new { e.FromRoomId, e.ExitDirection });

        // Relationship: 1 RoomId -> many RoomExits - linked by FromRoomId (FK).
        modelBuilder.Entity<RoomEntity>().HasMany(r => r.Exits).WithOne().HasForeignKey(e => e.FromRoomId);

        modelBuilder.Entity<RoomEntity>().Property(r => r.Width).IsRequired();
        modelBuilder.Entity<RoomEntity>().Property(r => r.Height).IsRequired();
    }
}