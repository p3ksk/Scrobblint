namespace Scrobblint.Application.Common;

/// <summary>Shared tuning knobs and validation bounds used across services.</summary>
public static class AppConstants
{
    public const int DefaultPageSize = 50;
    public const int MaxPageSize = 200;

    /// <summary>Maximum number of scrobbles accepted in a single batch submission.</summary>
    public const int MaxBatchSize = 1000;

    /// <summary>Default size of "top N" statistics lists.</summary>
    public const int TopListSize = 10;

    /// <summary>Trailing window (days) for the daily listening chart.</summary>
    public const int DailyChartDays = 30;

    /// <summary>Hours shown on the hourly listening distribution chart.</summary>
    public const int HourlyChartHours = 24;

    /// <summary>Days shown on the day-of-week listening distribution chart.</summary>
    public const int DayOfWeekChartDays = 7;

    public const int UsernameMinLength = 3;
    public const int UsernameMaxLength = 32;
    public const int PasswordMinLength = 8;
    public const int PasswordMaxLength = 128;
    // 255 keeps (UserId, Artist, Track) / (UserId, Artist, Album) composite indexes within InnoDB's
    // 3072-byte key limit under utf8mb4, while staying ample for artist/track/album names.
    public const int FieldMaxLength = 255;

    /// <summary>Clamps a requested page size into the allowed range.</summary>
    public static int ClampPageSize(int pageSize) =>
        pageSize <= 0 ? DefaultPageSize : Math.Min(pageSize, MaxPageSize);

    public static int ClampPage(int page) => page < 1 ? 1 : page;
}
