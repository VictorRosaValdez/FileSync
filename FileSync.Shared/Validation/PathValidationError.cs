namespace FileSync.Shared.Validation;

public enum PathValidationError
{
    Empty,
    LeadingSlash,
    TraversalSegment,
    ForbiddenChar,
    ControlChar,
    TrailingSpaceOrDot,
    ReservedName,
    SegmentTooLong,
    PathTooLong,
}
