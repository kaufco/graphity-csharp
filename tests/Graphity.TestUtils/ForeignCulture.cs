using System.Globalization;

namespace Graphity.TestUtils;

/// <summary>
/// Makes the current culture one that writes negative numbers with U+2212 MINUS SIGN, as Swedish
/// does, until disposed. The test projects run with invariant globalization, so the culture is a
/// modified copy of the invariant one.
/// </summary>
public sealed class ForeignCulture : IDisposable
{
    private readonly CultureInfo _saved = CultureInfo.CurrentCulture;

    public ForeignCulture()
    {
        var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        culture.NumberFormat.NegativeSign = "\u2212";
        CultureInfo.CurrentCulture = culture;
    }

    public void Dispose() => CultureInfo.CurrentCulture = _saved;
}
