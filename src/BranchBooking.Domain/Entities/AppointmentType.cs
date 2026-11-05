using BranchBooking.Domain.Exceptions;

namespace BranchBooking.Domain.Entities;

/// <summary>A reason to visit a branch. New types are added as data, not code.</summary>
public sealed class AppointmentType
{
    public const int CodeMaxLength = 40;
    public const int NameMaxLength = 100;
    public const int TextMaxLength = 500;

    private AppointmentType()
    {
        // Required by EF Core.
    }

    public Guid Id { get; private set; }

    /// <summary>Stable machine code, for example <c>OPEN_ACCOUNT</c>.</summary>
    public string Code { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    /// <summary>What the customer should bring, shown on the confirmation.</summary>
    public string WhatToBring { get; private set; } = string.Empty;

    public int SortOrder { get; private set; }

    public bool IsActive { get; private set; } = true;

    public static AppointmentType Create(
        Guid id, string code, string name, string description, string whatToBring, int sortOrder)
    {
        if (id == Guid.Empty)
        {
            throw new DomainException("An appointment type needs an id.");
        }

        if (!IsValidCode(code))
        {
            throw new DomainException(
                $"Code must be 3 to {CodeMaxLength} characters: capital letters, digits and underscores, starting with a letter.");
        }

        var trimmedName = (name ?? string.Empty).Trim();
        if (trimmedName.Length is 0 or > NameMaxLength)
        {
            throw new DomainException($"Name is required and at most {NameMaxLength} characters.");
        }

        var trimmedDescription = (description ?? string.Empty).Trim();
        var trimmedWhatToBring = (whatToBring ?? string.Empty).Trim();
        if (trimmedDescription.Length > TextMaxLength || trimmedWhatToBring.Length > TextMaxLength)
        {
            throw new DomainException($"Description and what-to-bring are at most {TextMaxLength} characters.");
        }

        return new AppointmentType
        {
            Id = id,
            Code = code,
            Name = trimmedName,
            Description = trimmedDescription,
            WhatToBring = trimmedWhatToBring,
            SortOrder = sortOrder,
            IsActive = true,
        };
    }

    /// <summary>Hides the type from customers. Existing bookings are kept.</summary>
    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;

    private static bool IsValidCode(string? code) =>
        code is { Length: >= 3 and <= CodeMaxLength }
        && char.IsAsciiLetterUpper(code[0])
        && code.All(c => char.IsAsciiLetterUpper(c) || char.IsAsciiDigit(c) || c == '_');
}
