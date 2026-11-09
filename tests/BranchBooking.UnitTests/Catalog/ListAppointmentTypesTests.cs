using BranchBooking.Domain.Entities;
using BranchBooking.UnitTests.Support;
using Xunit;

namespace BranchBooking.UnitTests.Catalog;

public class ListAppointmentTypesTests
{
    [Fact]
    public async Task Lists_active_types_in_sort_order_and_maps_every_field()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new TestWorld();

        var result = await world.ListAppointmentTypes().ExecuteAsync(ct);

        var dto = Assert.Single(result);
        Assert.Equal(world.Type.Id, dto.Id);
        Assert.Equal("TEST_TYPE", dto.Code);
        Assert.Equal("Test type", dto.Name);
        Assert.Equal("Description", dto.Description);
        Assert.Equal("Bring ID", dto.WhatToBring);
    }

    [Fact]
    public async Task Leaves_out_inactive_types()
    {
        var ct = TestContext.Current.CancellationToken;
        var hidden = AppointmentType.Create(Guid.CreateVersion7(), "HIDDEN_TYPE", "Hidden", "d", "w", 0);
        hidden.Deactivate();
        var type = AppointmentType.Create(Guid.CreateVersion7(), "SHOWN_TYPE", "Shown", "d", "w", 1);
        var useCase = new BranchBooking.Application.Catalog.ListAppointmentTypes(
            new InMemoryAppointmentTypeRepository(hidden, type));

        var result = await useCase.ExecuteAsync(ct);

        Assert.Equal(["Shown"], result.Select(t => t.Name));
    }
}
