namespace HomeDaemon.IntegrationTests.Support;

/// <summary>
/// Shares a single API host across the HTTP test classes. The factory points the host at a
/// throwaway SQLite file through process-wide environment variables, so two live instances
/// would overwrite each other's connection string; a collection also serialises these tests
/// against the one database they share.
/// </summary>
[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<HomeDaemonApiFactory>
{
    public const string Name = "api-host";
}
