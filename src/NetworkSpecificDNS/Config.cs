using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace NetworkSpecificDNS;

/// <summary>
/// The configuration DTO.
/// </summary>
[SuppressMessage("Usage", "CA2227", Justification = "Required for serialization.")]
public class Config
{
    /// <summary>
    /// Gets or sets the interval between polling.
    /// </summary>
    public int Interval { get; set; } = 30000;

    /// <summary>
    /// Gets or sets the settings.
    /// </summary>
    public IDictionary<string, IDictionary<string, IList<string>>> Settings { get; set; } = new Dictionary<string, IDictionary<string, IList<string>>>();
}
