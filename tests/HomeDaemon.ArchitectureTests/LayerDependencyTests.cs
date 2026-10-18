using HomeDaemon.Application.Abstractions;
using HomeDaemon.DataAccess.Persistence;
using HomeDaemon.Domain.Chores;
using HomeDaemon.Domain.Households;
using HomeDaemon.Domain.Scheduling;
using HomeDaemon.Domain.Sync;
using Microsoft.EntityFrameworkCore;
using NetArchTest.Rules;

namespace HomeDaemon.ArchitectureTests;

public sealed class LayerDependencyTests
{
    [Fact]
    public void Domain_DoesNotDependOnOtherLayersOrInfrastructure()
    {
        var result = Types.InAssembly(typeof(ChoreDefinition).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny("HomeDaemon.Application", "HomeDaemon.DataAccess", "HomeDaemon.Api", "Microsoft.EntityFrameworkCore")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void Application_DoesNotDependOnDataAccessOrApi()
    {
        var result = Types.InAssembly(typeof(IApplicationDbContext).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny("HomeDaemon.DataAccess", "HomeDaemon.Api")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void DataAccess_DoesNotDependOnApi()
    {
        var result = Types.InAssembly(typeof(HomeDaemonDbContext).Assembly)
            .ShouldNot()
            .HaveDependencyOn("HomeDaemon.Api")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void Application_DoesNotReferenceTheSqliteProviderDirectly()
    {
        var result = Types.InAssembly(typeof(IApplicationDbContext).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny("Microsoft.EntityFrameworkCore.Sqlite", "Microsoft.Data.Sqlite")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void PersistenceEntities_LiveInTheDomainAssembly()
    {
        var domainAssembly = typeof(ChoreDefinition).Assembly;
        Assert.Same(domainAssembly, typeof(CompletionHistory).Assembly);
        Assert.Same(domainAssembly, typeof(Household).Assembly);
        Assert.Same(domainAssembly, typeof(ScheduleRun).Assembly);
        Assert.Same(domainAssembly, typeof(TodoSyncOutbox).Assembly);
        Assert.Same(domainAssembly, typeof(Room).Assembly);
    }
}
