using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace HomeDaemon.DataAccess.Persistence;

/// <summary>
/// Used only by the EF Core command line tools at design time, so migrations can be created
/// without booting the web host.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<HomeDaemonDbContext>
{
    public HomeDaemonDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<HomeDaemonDbContext>()
            .UseSqlite("Data Source=home-daemon.design.db")
            .Options;

        return new HomeDaemonDbContext(options);
    }
}
