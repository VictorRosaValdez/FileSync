namespace FileSync.Shared.Protocol;

public sealed record ParsedRequest(RequestLine Line, Headers Headers);

public sealed record ParsedResponse(ResponseLine Line, Headers Headers);
