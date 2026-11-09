using BranchBooking.Domain.Exceptions;
using BranchBooking.Domain.ValueObjects;

namespace BranchBooking.Domain.Entities;

public sealed class Branch
{
    public const int CodeMaxLength = 20;
    public const int NameMaxLength = 100;
    public const int AddressMaxLength = 200;
    public const int CityMaxLength = 100;
    public const int ProvinceMaxLength = 50;
    public const int TimeZoneIdMaxLength = 64;
    public const int MinSlotLengthMinutes = 5;
    public const int MaxSlotLengthMinutes = 240;
    public const int MaxSlotCapacity = 50;

    private readonly List<BranchOpeningHours> _openingHours = [];

    private Branch()
    {
        // Required by EF Core.
    }

    public Guid Id { get; private set; }

    /// <summary>Short stable code, for example <c>DEMO-JHB</c>.</summary>
    public string Code { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string AddressLine { get; private set; } = string.Empty;

    public string City { get; private set; } = string.Empty;

    public string Province { get; private set; } = string.Empty;

    /// <summary>IANA time zone the opening hours are expressed in.</summary>
    public string TimeZoneId { get; private set; } = string.Empty;

    public int SlotLengthMinutes { get; private set; }

    /// <summary>How many appointments can run at the same time (consultants available).</summary>
    public int SlotCapacity { get; private set; }

    public bool IsActive { get; private set; } = true;

    public IReadOnlyList<BranchOpeningHours> OpeningHours => _openingHours;

    public static Branch Create(
        Guid id,
        string code,
        string name,
        string addressLine,
        string city,
        string province,
        string timeZoneId,
        int slotLengthMinutes,
        int slotCapacity)
    {
        if (id == Guid.Empty)
        {
            throw new DomainException("A branch needs an id.");
        }

        if (!IsValidCode(code))
        {
            throw new DomainException(
                $"Code must be 3 to {CodeMaxLength} characters: capital letters, digits and hyphens, starting with a letter.");
        }

        var trimmedName = Required(name, NameMaxLength, "Name");
        var trimmedAddress = Required(addressLine, AddressMaxLength, "Address");
        var trimmedCity = Required(city, CityMaxLength, "City");
        var trimmedProvince = Required(province, ProvinceMaxLength, "Province");

        if (slotLengthMinutes is < MinSlotLengthMinutes or > MaxSlotLengthMinutes)
        {
            throw new DomainException(
                $"Slot length must be between {MinSlotLengthMinutes} and {MaxSlotLengthMinutes} minutes.");
        }

        if (slotCapacity is < 1 or > MaxSlotCapacity)
        {
            throw new DomainException($"Slot capacity must be between 1 and {MaxSlotCapacity}.");
        }

        EnsureTimeZoneExists(timeZoneId);

        return new Branch
        {
            Id = id,
            Code = code,
            Name = trimmedName,
            AddressLine = trimmedAddress,
            City = trimmedCity,
            Province = trimmedProvince,
            TimeZoneId = timeZoneId,
            SlotLengthMinutes = slotLengthMinutes,
            SlotCapacity = slotCapacity,
            IsActive = true,
        };
    }

    /// <summary>Hides the branch from customers. Existing bookings are kept.</summary>
    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;

    /// <summary>Adds the opening window for one weekday.</summary>
    public void AddOpeningHours(DayOfWeek dayOfWeek, TimeOnly opensAt, TimeOnly closesAt)
    {
        if (_openingHours.Any(h => h.DayOfWeek == dayOfWeek))
        {
            throw new DomainException($"Opening hours for {dayOfWeek} are already set.");
        }

        if (closesAt <= opensAt)
        {
            throw new DomainException("A branch must close after it opens.");
        }

        if (closesAt.ToTimeSpan() - opensAt.ToTimeSpan() < TimeSpan.FromMinutes(SlotLengthMinutes))
        {
            throw new DomainException("The opening window is shorter than one slot.");
        }

        _openingHours.Add(BranchOpeningHours.Create(Id, dayOfWeek, opensAt, closesAt));
    }

    /// <summary>
    /// The slot grid for a local calendar date, in UTC. Empty when the branch is closed that day.
    /// Slots that would end after closing time are not offered.
    /// </summary>
    public IReadOnlyList<TimeSlot> GetSlotsFor(DateOnly localDate)
    {
        var hours = _openingHours.FirstOrDefault(h => h.DayOfWeek == localDate.DayOfWeek);
        if (hours is null)
        {
            return [];
        }

        var zone = TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);
        var length = TimeSpan.FromMinutes(SlotLengthMinutes);
        var close = hours.ClosesAt.ToTimeSpan();
        var slots = new List<TimeSlot>();

        for (var cursor = hours.OpensAt.ToTimeSpan(); cursor + length <= close; cursor += length)
        {
            var local = localDate.ToDateTime(TimeOnly.FromTimeSpan(cursor), DateTimeKind.Unspecified);
            if (zone.IsInvalidTime(local))
            {
                continue; // Skipped by a daylight-saving jump; never happens in South Africa.
            }

            var startsAtUtc = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, zone), TimeSpan.Zero);
            slots.Add(new TimeSlot(startsAtUtc, startsAtUtc + length));
        }

        return slots;
    }

    private static string Required(string? value, int maxLength, string field)
    {
        var trimmed = (value ?? string.Empty).Trim();
        if (trimmed.Length is 0 || trimmed.Length > maxLength)
        {
            throw new DomainException($"{field} is required and at most {maxLength} characters.");
        }

        return trimmed;
    }

    private static bool IsValidCode(string? code) =>
        code is { Length: >= 3 and <= CodeMaxLength }
        && char.IsAsciiLetterUpper(code[0])
        && code.All(c => char.IsAsciiLetterUpper(c) || char.IsAsciiDigit(c) || c == '-');

    private static void EnsureTimeZoneExists(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId) || timeZoneId.Length > TimeZoneIdMaxLength)
        {
            throw new DomainException("A valid time zone id is required.");
        }

        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            throw new DomainException($"Unknown time zone '{timeZoneId}'.");
        }
    }
}
