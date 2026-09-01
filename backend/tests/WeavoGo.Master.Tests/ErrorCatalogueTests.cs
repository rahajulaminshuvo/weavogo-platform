using System.Net;
using WeavoGo.Master.Api.Common;
using Xunit;

namespace WeavoGo.Master.Tests;

/// <summary>SDS §10.8 and §12.2 — the error codes and the status codes they map to.</summary>
public class ErrorCatalogueTests
{
    public static IEnumerable<object[]> Catalogue => new List<object[]>
    {
        new object[] { ErrorCodes.ItemCodeDuplicate,          HttpStatusCode.Conflict },
        new object[] { ErrorCodes.AttributeRequiredMissing,   HttpStatusCode.UnprocessableEntity },
        new object[] { ErrorCodes.AttributeDataTypeMismatch,  HttpStatusCode.UnprocessableEntity },
        new object[] { ErrorCodes.ItemNotAuthorizedForEntity, HttpStatusCode.Forbidden },
        new object[] { ErrorCodes.ItemCodeLocked,             HttpStatusCode.Conflict },
        new object[] { ErrorCodes.InvalidStatusTransition,    HttpStatusCode.Conflict },
        new object[] { ErrorCodes.QcProfileNotSatisfied,      HttpStatusCode.UnprocessableEntity },
        new object[] { ErrorCodes.BomCircularReference,       HttpStatusCode.UnprocessableEntity },
        new object[] { ErrorCodes.ApprovalRoleMismatch,       HttpStatusCode.Forbidden },
        new object[] { ErrorCodes.ObsolescenceStockOnHand,    HttpStatusCode.Conflict },
    };

    [Theory]
    [MemberData(nameof(Catalogue))]
    public void ApiException_CarriesCodeAndStatus(string code, HttpStatusCode status)
    {
        var ex = new ApiException(code, status, "message", "field");

        Assert.Equal(code, ex.Code);
        Assert.Equal(status, ex.Status);
        Assert.Equal("field", ex.Field);
    }

    [Fact]
    public void Conflict_Helper_Uses409()
        => Assert.Equal(HttpStatusCode.Conflict, ApiException.Conflict(ErrorCodes.ItemCodeDuplicate, "x").Status);

    [Fact]
    public void Unprocessable_Helper_Uses422()
        => Assert.Equal(HttpStatusCode.UnprocessableEntity,
            ApiException.Unprocessable(ErrorCodes.AttributeRequiredMissing, "x").Status);

    [Fact]
    public void Forbidden_Helper_Uses403()
        => Assert.Equal(HttpStatusCode.Forbidden,
            ApiException.Forbidden(ErrorCodes.ApprovalRoleMismatch, "x").Status);

    [Fact]
    public void Envelope_HasTheFourSpecifiedFields()
    {
        var envelope = new ErrorEnvelope
        {
            Error = new ErrorEnvelope.ErrorBody
            {
                Code = ErrorCodes.ItemNotAuthorizedForEntity,
                Message = "Item 6 is not authorized for business unit 13.",
                Field = null,
                TraceId = "8f14e45f-ea08-4c34-9a1e-2c3b1f0a9d77"
            }
        };

        var json = System.Text.Json.JsonSerializer.Serialize(envelope,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));

        Assert.Contains("\"code\"", json);
        Assert.Contains("\"message\"", json);
        Assert.Contains("\"field\"", json);
        Assert.Contains("\"traceId\"", json);
    }
}
