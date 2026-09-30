using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PropNest.Domain.Packages
{
    public sealed class PackageDefinitions
    {
        public long PackageId { get; private set; }
        public string PackageCode { get; private set; } = string.Empty;
        public string Name { get; private set; } = string.Empty;
        public decimal Price { get; private set; }
        public int DurationDays { get; private set; }
        public string Kind { get; private set; } = string.Empty;
        public bool IsActive { get; private set; } = true;


        private PackageDefinitions()
        {
        }
        public PackageDefinitions(
            long packageId,
            string packageCode,
            string name,
            decimal price,
            int durationDays,
            string kind,
            bool isActive = true)
        {
            if (string.IsNullOrWhiteSpace(packageCode))
                throw new ArgumentException("Package code cannot be empty.", nameof(packageCode));
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Package name cannot be empty.", nameof(name));
            if (price < 0)
                throw new ArgumentOutOfRangeException(nameof(price), "Price must be non-negative.");
            if (durationDays <= 0)
                throw new ArgumentOutOfRangeException(nameof(durationDays), "Duration days must be greater than zero.");
            PackageId = packageId;
            PackageCode = packageCode.Trim();
            Name = name.Trim();
            Price = price;
            DurationDays = durationDays;
            Kind = kind.Trim();
            IsActive = isActive;
        }
       

    }
}
