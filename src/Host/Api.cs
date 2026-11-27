// SPDX-License-Identifier: LGPL-3.0-or-later
using Osme.Core.Documents;

namespace Osme.Host;

/// <summary>The one response shape of the API, for success and for failure.</summary>
public sealed record ApiResponse(bool Ok, object? Data, ApiError? Error);

public sealed record ApiError(string Code, IReadOnlyList<FieldError>? Fields);

public static class Api
{
    public static IResult Ok(object data, int status = StatusCodes.Status200OK) =>
        Results.Json(new ApiResponse(true, data, null), statusCode: status);

    public static IResult Fail(int status, string code, IReadOnlyList<FieldError>? fields = null) =>
        Results.Json(new ApiResponse(false, null, new ApiError(code, fields)), statusCode: status);

    // Same answer for a document nobody declares and for a record that does not exist.
    public static IResult NotFound() => Fail(StatusCodes.Status404NotFound, "not_found");
}
