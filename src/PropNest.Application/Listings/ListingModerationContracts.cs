using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PropNest.Application.Listings;

public sealed record ListingSubmitted(
    Guid CorrelationId,
    long ListingId,
    long OwnerUserId,
    string Title,
    string? Description,
    string PropertyType,
    string ListingType,
    decimal Price,
    decimal Area,
    string City,
    string District,
    string? Ward,
    string Address,
    DateTimeOffset SubmittedAt);
