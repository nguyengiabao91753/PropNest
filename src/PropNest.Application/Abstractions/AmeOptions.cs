using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PropNest.Application.Abstractions
{
    public sealed class AmeOptions
    {
        public const string SectionName = "Ame";
        [SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "Bound directly from configuration appsettings.json.")]
        public string BaseUrl { get; init; } = "http://localhost:8001";
        public int TimeoutSeconds { get; init; } = 15;
        public bool Enabled { get; init; } = true;
    }
}
