using System.Net;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using WeavoGo.Master.Api.Common;

namespace WeavoGo.Master.Api.Middleware;

/// <summary>
/// Renders every non-2xx response in the SDS §10.8 envelope, and translates the
/// database's own guard rails (unique indexes, the governance triggers in
/// 10_triggers.sql) into the §12.2 error codes.
/// </summary>
public sealed class ErrorHandlingMiddleware
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorHandlingMiddleware> _logger;

    public ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (ApiException ex)
        {
            await WriteAsync(context, ex.Status, ex.Code, ex.Message, ex.Field);
        }
        catch (DbUpdateConcurrencyException)
        {
            await WriteAsync(context, HttpStatusCode.Conflict, ErrorCodes.ConcurrencyConflict,
                "The record was modified by another user. Reload and try again.", null);
        }
        catch (DbUpdateException ex) when (TryTranslate(ex, out var code, out var status, out var message))
        {
            await WriteAsync(context, status, code, message, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception on {Method} {Path}", context.Request.Method, context.Request.Path);
            await WriteAsync(context, HttpStatusCode.InternalServerError, ErrorCodes.InternalError,
                "An unexpected error occurred.", null);
        }
    }

    /// <summary>Maps SQL Server errors raised by constraints and triggers onto §12.2 codes.</summary>
    private static bool TryTranslate(DbUpdateException ex, out string code, out HttpStatusCode status, out string message)
    {
        code = ErrorCodes.InternalError;
        status = HttpStatusCode.InternalServerError;
        message = "A database constraint rejected the change.";

        if (ex.InnerException is not SqlException sql) return false;

        switch (sql.Number)
        {
            // Trigger THROW codes from 10_triggers.sql.
            case 51001:
                code = ErrorCodes.ValidationFailed; status = HttpStatusCode.UnprocessableEntity;
                message = "Denormalized classification ancestry does not match the item family (SDS 4.2).";
                return true;
            case 51002:
                code = ErrorCodes.InvalidStatusTransition; status = HttpStatusCode.Conflict;
                message = "Requested item status change is not a permitted transition (SDS 8.1).";
                return true;
            case 51003:
                code = ErrorCodes.ObsolescenceStockOnHand; status = HttpStatusCode.Conflict;
                message = "Marking an item Obsolete requires an ItemObsolescence record (SDS 8.5).";
                return true;
            case 51004:
            case 51005:
                code = ErrorCodes.AttributeDataTypeMismatch; status = HttpStatusCode.UnprocessableEntity;
                message = "Attribute value does not match its declared DataType (SDS 4.11).";
                return true;
            case 51006:
                code = ErrorCodes.ValidationFailed; status = HttpStatusCode.Conflict;
                message = "An attribute definition's DataType cannot change once it is in use (SDS 4.7).";
                return true;
            case 51007:
            case 51008:
                code = ErrorCodes.BomCircularReference; status = HttpStatusCode.UnprocessableEntity;
                message = "The proposed BOM structure would create a cycle (SDS 6.5.2).";
                return true;

            // Unique constraint / index violations.
            case 2601:
            case 2627:
                status = HttpStatusCode.Conflict;
                var text = sql.Message;
                if (text.Contains("UQ_ItemMaster_Code", StringComparison.OrdinalIgnoreCase))
                { code = ErrorCodes.ItemCodeDuplicate; message = "Item code already exists."; }
                else if (text.Contains("UQ_ItemSerial", StringComparison.OrdinalIgnoreCase))
                { code = ErrorCodes.SerialDuplicate; message = "Serial number already exists for this item."; }
                else if (text.Contains("UQ_ItemBarcode", StringComparison.OrdinalIgnoreCase))
                { code = ErrorCodes.BarcodeDuplicate; message = "Barcode value is already assigned to another item."; }
                else if (text.Contains("UQ_ItemRFID", StringComparison.OrdinalIgnoreCase))
                { code = ErrorCodes.RfidDuplicate; message = "EPC code is already assigned to another tag."; }
                else
                { code = ErrorCodes.ValidationFailed; message = "A uniqueness constraint was violated."; }
                return true;

            // CHECK constraint violation.
            case 547:
                code = ErrorCodes.ValidationFailed;
                status = HttpStatusCode.UnprocessableEntity;
                message = "A database constraint rejected the change: " + sql.Message;
                return true;
        }

        return false;
    }

    private static async Task WriteAsync(HttpContext context, HttpStatusCode status, string code,
                                         string message, string? field)
    {
        if (context.Response.HasStarted) return;

        context.Response.Clear();
        context.Response.StatusCode = (int)status;
        context.Response.ContentType = "application/json";

        var envelope = new ErrorEnvelope
        {
            Error = new ErrorEnvelope.ErrorBody
            {
                Code = code,
                Message = message,
                Field = field,
                TraceId = context.TraceIdentifier
            }
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(envelope, Json));
    }
}
