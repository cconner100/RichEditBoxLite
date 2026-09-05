namespace CConner100.RichEditBoxLite;

/// <summary>
/// Safety bounds shared by every codec so imported content cannot exhaust
/// memory or stall the UI regardless of the source format.
/// </summary>
internal static class CodecLimits
{
    internal const int MaximumInputLength = 16 * 1024 * 1024;
    internal const int MaximumNestingDepth = 256;
}
