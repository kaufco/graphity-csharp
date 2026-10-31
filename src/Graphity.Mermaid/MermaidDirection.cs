namespace Graphity.Mermaid;

/// <summary>
/// The direction of a flowchart, for <see cref="MermaidWriter.Direction"/>.
/// </summary>
/// <remarks>
/// The Java library nests it in its writer as <c>MermaidWriter.Direction</c>; C# allows no nested
/// type beside a method of the same name, so it lives beside the writer.
/// </remarks>
public enum MermaidDirection
{
    /// <summary>Top to bottom, <c>TD</c>.</summary>
    TopDown,

    /// <summary>Bottom to top, <c>BT</c>.</summary>
    BottomUp,

    /// <summary>Left to right, <c>LR</c>.</summary>
    LeftRight,

    /// <summary>Right to left, <c>RL</c>.</summary>
    RightLeft,
}
