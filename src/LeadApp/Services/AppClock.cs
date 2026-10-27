namespace LeadApp.Services;

/// <summary>Time helpers in the business time zone (Europe/Sarajevo).</summary>
public sealed class AppClock(TimeProvider timeProvider)
{
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Sarajevo");

    public DateTime UtcNow => timeProvider.GetUtcNow().UtcDateTime;

    public DateTime ToLocal(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zone);

    public DateOnly Today => DateOnly.FromDateTime(ToLocal(UtcNow));

    /// <summary>Local wall-clock time on the given day with its Europe/Sarajevo offset (e.g. +01:00 / +02:00).</summary>
    public static DateTimeOffset AtLocal(DateOnly day, TimeOnly time)
    {
        var local = day.ToDateTime(time, DateTimeKind.Unspecified);
        return new DateTimeOffset(local, Zone.GetUtcOffset(local));
    }

    /// <summary>UTC instant at which the given local calendar day starts.</summary>
    public DateTime StartOfDayUtc(DateOnly day) =>
        TimeZoneInfo.ConvertTimeToUtc(day.ToDateTime(TimeOnly.MinValue), Zone);
}
