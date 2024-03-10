using Microsoft.EntityFrameworkCore;
using PocketDockUI.Data.Models;

namespace PocketDockUI.Data;

public class PocketDockContext : DbContext
{
    public PocketDockContext (DbContextOptions<PocketDockContext> options) : base(options)
    { }

    public DbSet<Server> Server { get; set; }
    public DbSet<ServerAssignment> ServerAssignment { get; set; }
    public DbSet<Proxy> Proxy { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<Server>()
            .Navigation(e => e.ServerAssignment)
            .AutoInclude();
        
        modelBuilder.Entity<ServerAssignment>()
            .Navigation(e => e.Proxy)
            .AutoInclude();

        modelBuilder.Entity<Proxy>()
            .HasIndex(x => x.Region)
            .IsUnique();
    }

    public void DeallocateServer(Server server)
    {
        if (server.ServerAssignmentId != null)
        {
            Remove(server.ServerAssignment);
            if (server.IsTemporaryServer)
            {
                server.ServerId = null;
                server.PrivateIpAddress = null;
            }
        }
    }
}