namespace HomeDaemon.IntegrationTests.Support;

/// <summary>A controllable clock for deterministic date assertions.</summary>
public sealed class TestTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    public DateTimeOffset UtcNow { get; set; } = utcNow;

    public static TestTimeProvider At(DateOnly date) => new(new DateTimeOffset(date.ToDateTime(new TimeOnly(12, 0)), TimeSpan.Zero));

    public override DateTimeOffset GetUtcNow() => UtcNow;
}
