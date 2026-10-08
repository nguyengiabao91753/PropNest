using PropNest.Application.Listings;
using PropNest.Domain.Listings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PropNest.Domain.Tests
{
    public sealed class DeltaEngineTests
    {
        [Fact]
        public void PriceChangeDetected()
        {
            var before = CreateListingDto();
            var after = before with { Price = 5_000_000_000m };

            var deltas = DeltaEngine.CreateDeltas(before, after);

            var delta = Assert.Single(deltas);
            Assert.Equal("Price", delta.FieldName);
            Assert.Equal(4_500_000_000m, delta.OldValue);
            Assert.Equal(5_000_000_000m, delta.NewValue);
        }



        private static ListingDto CreateListingDto() => new(
        ListingId: 1,
        OwnerUserId: 1,
        Title: "Apartment for sale",
        Description: "A valid property listing.",
        PropertyType: "Apartment",
        ListingType: "Sale",
        Price: 4_500_000_000m,
        Area: 75m,
        City: "Ho Chi Minh City",
        District: "District 7",
        Ward: "Tan Phu",
        Address: "1 Nguyen Van Linh",
        Status: ListingStatus.Draft,
        ModerationDecision: ModerationDecision.Approved,
        PackageCode: "Standard",
        StartDate: null,
        EndDate: null,
        RowVersion: Array.Empty<byte>());
    }
}
