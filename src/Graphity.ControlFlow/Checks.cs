using System.Globalization;
using System.Runtime.CompilerServices;

namespace Graphity.ControlFlow;

/// <summary>
/// Index checks, the counterpart of Java's <c>Objects.checkIndex</c>. The module's copy of the core's
/// internal helper, which a module cannot see; same exception and message.
/// </summary>
internal static class Checks
{
    /// <summary>Throws <see cref="ArgumentOutOfRangeException"/> unless <paramref name="index"/> is in <c>0..length-1</c>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Index(int index, int length, [CallerArgumentExpression(nameof(index))] string? name = null)
    {
        if ((uint)index >= (uint)length)
        {
            ThrowIndex(index, length, name);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowIndex(int index, int length, string? name) =>
        throw new ArgumentOutOfRangeException(name, index, string.Create(CultureInfo.InvariantCulture,
            $"Index {index} out of bounds for length {length}"));
}
