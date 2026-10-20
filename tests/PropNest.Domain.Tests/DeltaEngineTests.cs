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
        //Thay đổi giá 
        [Fact]
        public void PriceChangeDetected()
        {
            var deltas = CreateDeltas(Price: 5_000_000_000m);

            var delta = Assert.Single(deltas);
            Assert.Equal("Price", delta.FieldName);
            Assert.Equal(4_500_000_000m, delta.OldValue);
            Assert.Equal(5_000_000_000m, delta.NewValue);
        }

        //Thay đổi tiêu đề
        [Fact]
        public void TitleChangeDetected()
        {
            var deltas = CreateDeltas(Title: "Villa for rent");
            var delta = Assert.Single(deltas);
            Assert.Equal("Title", delta.FieldName);
            Assert.Equal("Apartment for sale", delta.OldValue);
            Assert.Equal("Villa for rent", delta.NewValue);
        }

        //Không có thay đổi
        [Fact]
        public void NoChangeDetected()
        {
            var deltas = CreateDeltas();
            Assert.Empty(deltas);
        }

        //Đổi description null -> "Tan Phu"
        [Fact]
        public void NullToValueDetected()
        {
            var deltas = CreateDeltas(Description: "A new Description");

            var delta = Assert.Single(deltas);
            Assert.Equal("Description", delta.FieldName);
            Assert.Null(delta.OldValue);
            Assert.Equal("A new Description", delta.NewValue);
        }

        //Đổi ward "Tan Phu" -> null
        [Fact]
        public void ValueToNullDetected()
        {
            var deltas = CreateDeltas(Ward: null);
            var delta = Assert.Single(deltas);
            Assert.Equal("Ward", delta.FieldName);
            Assert.Equal("Tan Phu", delta.OldValue);
            Assert.Null(delta.NewValue);
        }



        private static List<ListingDelta> CreateDeltas(decimal? Price = null, string? Title = null, string? Ward="Tan Phu", string? Description = null)
        {
            var before = CreateListingDto();
            var after = before with
            {
                Price = Price ?? before.Price,
                Title = Title ?? before.Title,
                Description = Description ?? before.Description,
                Ward = Ward
            };

            return DeltaEngine.CreateDeltas(before, after);
        }

        private static ListingDto CreateListingDto() => new(
        ListingId: 1,
        OwnerUserId: 1,
        Title: "Apartment for sale",
        Description: null,
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
