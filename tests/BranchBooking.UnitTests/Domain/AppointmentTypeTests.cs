using BranchBooking.Domain.Entities;
using BranchBooking.Domain.Exceptions;
using Xunit;

namespace BranchBooking.UnitTests.Domain;

public class AppointmentTypeTests
{
    [Fact]
    public void Create_sets_all_fields_and_is_active()
    {
        var id = Guid.CreateVersion7();

        var type = AppointmentType.Create(id, "OPEN_ACCOUNT", " Open an account ", " Desc ", " Bring ID ", 1);

        Assert.Equal(id, type.Id);
        Assert.Equal("OPEN_ACCOUNT", type.Code);
        Assert.Equal("Open an account", type.Name);
        Assert.Equal("Desc", type.Description);
        Assert.Equal("Bring ID", type.WhatToBring);
        Assert.Equal(1, type.SortOrder);
        Assert.True(type.IsActive);
    }

    [Theory]
    [InlineData("")]
    [InlineData("ab")]
    [InlineData("open_account")]
    [InlineData("1OPEN")]
    [InlineData("OPEN-ACCOUNT")]
    [InlineData("OPEN ACCOUNT")]
    public void Create_rejects_an_invalid_code(string code)
    {
        Assert.Throws<DomainException>(() => AppointmentType.Create(Guid.CreateVersion7(), code, "Name", "d", "w", 1));
    }

    [Fact]
    public void Create_rejects_an_empty_id()
    {
        Assert.Throws<DomainException>(() => AppointmentType.Create(Guid.Empty, "OPEN_ACCOUNT", "Name", "d", "w", 1));
    }

    [Fact]
    public void Create_rejects_a_blank_name()
    {
        Assert.Throws<DomainException>(() => AppointmentType.Create(Guid.CreateVersion7(), "OPEN_ACCOUNT", "  ", "d", "w", 1));
    }

    [Fact]
    public void Create_rejects_text_over_the_limit()
    {
        var tooLong = new string('x', AppointmentType.TextMaxLength + 1);

        Assert.Throws<DomainException>(() => AppointmentType.Create(Guid.CreateVersion7(), "OPEN_ACCOUNT", "Name", tooLong, "w", 1));
        Assert.Throws<DomainException>(() => AppointmentType.Create(Guid.CreateVersion7(), "OPEN_ACCOUNT", "Name", "d", tooLong, 1));
    }

    [Fact]
    public void Deactivate_and_Activate_toggle_availability()
    {
        var type = AppointmentType.Create(Guid.CreateVersion7(), "OPEN_ACCOUNT", "Name", "d", "w", 1);

        type.Deactivate();
        Assert.False(type.IsActive);

        type.Activate();
        Assert.True(type.IsActive);
    }
}
