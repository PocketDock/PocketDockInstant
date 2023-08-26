using Microsoft.EntityFrameworkCore;
using PocketDockUI.Data.Models;

namespace PocketDockUI.Data;

public class PocketDockContext : DbContext
{
    public PocketDockContext (DbContextOptions<PocketDockContext> options) : base(options)
    { }

    public DbSet<Server> Server { get; set; }
    public DbSet<ServerAssignment> ServerAssignment { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<Server>()
            .Navigation(e => e.ServerAssignment)
            .AutoInclude();
    }

    public void DeallocateServer(Server server)
    {
        if (server.ServerAssignmentId != null)
        {
            Remove(server.ServerAssignment);
        }
    }
}