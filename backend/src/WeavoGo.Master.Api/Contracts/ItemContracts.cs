using System.Text.Json.Serialization;

namespace WeavoGo.Master.Api.Contracts;

/// <summary>SDS §10.3 request body.</summary>
public sealed class CreateItemRequest
{
    public string ItemCode { get; set; } = null!;
    public string ItemName { get; set; } = null!;
    public string? Description { get; set; }
    public int ItemFamilyId { get; set; }
    public int? AttributeTemplateId { get; set; }
    public int BaseUOMId { get; set; }
    public bool CanPurchase { get; set; } = true;
    public bool CanSell { get; set; }
    public bool CanManufacture { get; set; }
    public bool CanStock { get; set; } = true;
    public bool CanTransfer { get; set; } = true;
    public bool IsSerialControlled { get; set; }
    public bool IsLotControlled { get; set; }
    public List<AttributeValueDto> Attributes { get; set; } = new();
    public List<int> BusinessUnitIds { get; set; } = new();
}

/// <summary>A dynamic attribute supplied by AttributeCode (SDS §10.3).</summary>
public sealed class AttributeValueDto
{
    public string AttributeCode { get; set; } = null!;

    /// <summary>Raw JSON value — string, number or boolean, resolved against the definition's DataType.</summary>
    public System.Text.Json.JsonElement Value { get; set; }
}

/// <summary>SDS §10.3 response body.</summary>
public sealed class CreateItemResponse
{
    public long ItemId { get; set; }
    public string ItemCode { get; set; } = null!;
    public string ItemStatus { get; set; } = null!;
    public int VersionNumber { get; set; }
    public int CreatedBy { get; set; }
    public DateTime CreatedDate { get; set; }

    [JsonPropertyName("_links")]
    public Dictionary<string, string> Links { get; set; } = new();
}

/// <summary>SDS §10.4 response body.</summary>
public sealed class ItemDetailResponse
{
    public long ItemId { get; set; }
    public string ItemCode { get; set; } = null!;
    public string ItemName { get; set; } = null!;
    public string? Description { get; set; }
    public string ItemStatus { get; set; } = null!;
    public int VersionNumber { get; set; }
    public ClassificationDto Classification { get; set; } = new();
    public FlagsDto Flags { get; set; } = new();
    public string BaseUom { get; set; } = null!;
    public List<ResolvedAttributeDto> Attributes { get; set; } = new();
    public List<BusinessUnitDto> BusinessUnits { get; set; } = new();
    public List<PackagingDto> Packaging { get; set; } = new();
    public QcProfileDto? QcProfile { get; set; }

    public sealed class ClassificationDto
    {
        public int CategoryId { get; set; }
        public string CategoryName { get; set; } = null!;
        public int GroupId { get; set; }
        public string GroupName { get; set; } = null!;
        public int SubGroupId { get; set; }
        public string SubGroupName { get; set; } = null!;
        public int FamilyId { get; set; }
        public string FamilyName { get; set; } = null!;
    }

    public sealed class FlagsDto
    {
        public bool CanPurchase { get; set; }
        public bool CanSell { get; set; }
        public bool CanManufacture { get; set; }
        public bool CanStock { get; set; }
        public bool CanTransfer { get; set; }
        public bool IsSerialControlled { get; set; }
        public bool IsLotControlled { get; set; }
    }

    public sealed class BusinessUnitDto
    {
        public int BusinessUnitId { get; set; }
        public string UnitName { get; set; } = null!;
        public bool IsAuthorized { get; set; }
    }

    public sealed class PackagingDto
    {
        public byte Level { get; set; }
        public string LevelName { get; set; } = null!;
        public string Uom { get; set; } = null!;
        public decimal QtyPerParentLevel { get; set; }
        public bool IsPurchaseUom { get; set; }
        public bool IsSalesUom { get; set; }
    }

    public sealed class QcProfileDto
    {
        public int QcTemplateId { get; set; }
        public string TemplateName { get; set; } = null!;
        public bool IsMandatory { get; set; }
        public string InspectionFrequency { get; set; } = null!;
        public string Source { get; set; } = null!;   // "Item" or "Family"
    }
}

public sealed class ResolvedAttributeDto
{
    public string AttributeCode { get; set; } = null!;
    public string AttributeName { get; set; } = null!;
    public string DataType { get; set; } = null!;
    public object? Value { get; set; }
    public string? UnitOfMeasure { get; set; }
    public bool IsRequired { get; set; }
    public int DisplayOrder { get; set; }
}

/// <summary>SDS §10.5 response body.</summary>
public sealed class ItemSearchResponse
{
    public List<ItemSummaryDto> Items { get; set; } = new();
    public PaginationDto Pagination { get; set; } = new();

    public sealed class ItemSummaryDto
    {
        public long ItemId { get; set; }
        public string ItemCode { get; set; } = null!;
        public string ItemName { get; set; } = null!;
        public string ItemStatus { get; set; } = null!;
        public string CategoryCode { get; set; } = null!;
        public string BaseUom { get; set; } = null!;
    }

    public sealed class PaginationDto
    {
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalItems { get; set; }
        public int TotalPages { get; set; }
    }
}

/// <summary>SDS §10.6 request body — every field optional.</summary>
public sealed class PatchItemRequest
{
    public string? ItemName { get; set; }
    public string? Description { get; set; }
    public string? ItemCode { get; set; }
    public bool? CanPurchase { get; set; }
    public bool? CanSell { get; set; }
    public bool? CanManufacture { get; set; }
    public bool? CanStock { get; set; }
    public bool? CanTransfer { get; set; }
    public bool? IsSerialControlled { get; set; }
    public bool? IsLotControlled { get; set; }
    public List<AttributeValueDto>? Attributes { get; set; }
    public string? ChangeReason { get; set; }
}

/// <summary>SDS §10.6 response body.</summary>
public sealed class PatchItemResponse
{
    public long ItemId { get; set; }
    public string ItemStatus { get; set; } = null!;
    public int? PendingVersionNumber { get; set; }
    public long? ApprovalRequestId { get; set; }

    [JsonPropertyName("_links")]
    public Dictionary<string, string> Links { get; set; } = new();
}

/// <summary>SDS §10.7 submit request.</summary>
public sealed class SubmitItemRequest
{
    /// <summary>Optional. Falls back to the category's default workflow, then STANDARD-ITEM.</summary>
    public string? WorkflowTemplateCode { get; set; }
}

/// <summary>SDS §10.7 submit/approve response.</summary>
public sealed class ApprovalStateResponse
{
    public long RequestId { get; set; }
    public int CurrentStepOrder { get; set; }
    public string? CurrentStepRole { get; set; }
    public string OverallStatus { get; set; } = null!;
    public string ItemStatus { get; set; } = null!;
}

/// <summary>SDS §10.7 approval-action request.</summary>
public sealed class ApprovalActionRequest
{
    public int StepOrder { get; set; }
    public string Decision { get; set; } = null!;   // Approved | Rejected
    public string? Comments { get; set; }
}

public sealed class AuthorizeBusinessUnitsRequest
{
    public List<int> BusinessUnitIds { get; set; } = new();
    public bool IsAuthorized { get; set; } = true;
}

public sealed class ItemVersionDto
{
    public long ItemVersionId { get; set; }
    public int VersionNumber { get; set; }
    public string? ChangeReason { get; set; }
    public long? ApprovalRequestId { get; set; }
    public DateTime CreatedDate { get; set; }
    public int CreatedBy { get; set; }
    public System.Text.Json.JsonElement Snapshot { get; set; }
}

public sealed class LoginRequest
{
    public string UserName { get; set; } = null!;
    public string Password { get; set; } = null!;
}

public sealed class LoginResponse
{
    public string AccessToken { get; set; } = null!;
    public string TokenType { get; set; } = "Bearer";
    public int ExpiresInSeconds { get; set; }
    public int UserId { get; set; }
    public string FullName { get; set; } = null!;
    public List<string> Roles { get; set; } = new();
    public List<int> BusinessUnitIds { get; set; } = new();
}
