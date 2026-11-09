namespace BranchBooking.Application.Notifications;

/// <summary>Reminder settings (<c>Notifications:Reminders</c> section). Defaults are planner choices.</summary>
public sealed class ReminderOptions
{
    public const string SectionName = "Notifications:Reminders";

    public bool Enabled { get; set; } = true;

    /// <summary>A booking is reminded once its appointment is within this many hours.</summary>
    public int LeadHours { get; set; } = 24;

    /// <summary>No reminder when the appointment is this close or closer.</summary>
    public int MinimumLeadMinutes { get; set; } = 60;

    /// <summary>A booking must have been left alone this long, so a reminder never trails a fresh confirmation.</summary>
    public int MinimumGapHours { get; set; } = 4;

    public int ScanIntervalSeconds { get; set; } = 60;

    public int BatchSize { get; set; } = 100;

    public TimeSpan Lead => TimeSpan.FromHours(LeadHours);

    public TimeSpan MinimumLead => TimeSpan.FromMinutes(MinimumLeadMinutes);

    public TimeSpan MinimumGap => TimeSpan.FromHours(MinimumGapHours);

    public bool IsValid() =>
        LeadHours is >= 2 and <= 168
        && MinimumLeadMinutes >= 0
        && MinimumLeadMinutes < LeadHours * 60
        && MinimumGapHours is >= 0 and <= 72
        && ScanIntervalSeconds is >= 10 and <= 3600
        && BatchSize is >= 1 and <= 1000;
}
