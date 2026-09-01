using System.Net;

namespace WeavoGo.Master.Api.Common;

/// <summary>The complete error catalogue from SDS §10.8 and §12.2.</summary>
public static class ErrorCodes
{
    public const string ItemCodeDuplicate = "ITEM_CODE_DUPLICATE";
    public const string AttributeRequiredMissing = "ATTRIBUTE_REQUIRED_MISSING";
    public const string AttributeDataTypeMismatch = "ATTRIBUTE_DATATYPE_MISMATCH";
    public const string ItemNotAuthorizedForEntity = "ITEM_NOT_AUTHORIZED_FOR_ENTITY";
    public const string ItemCodeLocked = "ITEM_CODE_LOCKED";
    public const string InvalidStatusTransition = "INVALID_STATUS_TRANSITION";
    public const string QcProfileNotSatisfied = "QC_PROFILE_NOT_SATISFIED";
    public const string LotNotIssuable = "LOT_NOT_ISSUABLE";
    public const string SerialDuplicate = "SERIAL_DUPLICATE";
    public const string BarcodeDuplicate = "BARCODE_DUPLICATE";
    public const string RfidDuplicate = "RFID_DUPLICATE";
    public const string BomCircularReference = "BOM_CIRCULAR_REFERENCE";
    public const string ApprovalRoleMismatch = "APPROVAL_ROLE_MISMATCH";
    public const string ObsolescenceStockOnHand = "OBSOLESCENCE_STOCK_ON_HAND";
    public const string MappingTargetConflict = "MAPPING_TARGET_CONFLICT";

    // Codes outside the catalogue, for conditions the catalogue does not name.
    public const string NotFound = "RESOURCE_NOT_FOUND";
    public const string ValidationFailed = "VALIDATION_FAILED";
    public const string Unauthorized = "UNAUTHORIZED";
    public const string Forbidden = "FORBIDDEN";
    public const string ConcurrencyConflict = "CONCURRENCY_CONFLICT";
    public const string InternalError = "INTERNAL_ERROR";
}

/// <summary>The §10.8 error envelope. Every non-2xx response uses this shape.</summary>
public sealed class ErrorEnvelope
{
    public ErrorBody Error { get; set; } = new();

    public sealed class ErrorBody
    {
        public string Code { get; set; } = null!;
        public string Message { get; set; } = null!;
        public string? Field { get; set; }
        public string TraceId { get; set; } = null!;
    }
}

/// <summary>Throw to produce a §10.8 error response with a specific code and status.</summary>
public class ApiException : Exception
{
    public string Code { get; }
    public HttpStatusCode Status { get; }
    public string? Field { get; }

    public ApiException(string code, HttpStatusCode status, string message, string? field = null)
        : base(message)
    {
        Code = code;
        Status = status;
        Field = field;
    }

    public static ApiException Conflict(string code, string message, string? field = null)
        => new(code, HttpStatusCode.Conflict, message, field);

    public static ApiException Unprocessable(string code, string message, string? field = null)
        => new(code, HttpStatusCode.UnprocessableEntity, message, field);

    public static ApiException Forbidden(string code, string message)
        => new(code, HttpStatusCode.Forbidden, message);

    public static ApiException NotFound(string message)
        => new(ErrorCodes.NotFound, HttpStatusCode.NotFound, message);

    public static ApiException Validation(string message, string? field = null)
        => new(ErrorCodes.ValidationFailed, HttpStatusCode.BadRequest, message, field);
}
