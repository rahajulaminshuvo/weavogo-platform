using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WeavoGo.Master.Api.Common;
using WeavoGo.Master.Api.Contracts;
using WeavoGo.Master.Api.Domain;
using WeavoGo.Master.Api.Infrastructure;

namespace WeavoGo.Master.Api.Services;

/// <summary>
/// Resolves an item's effective attribute template (SDS §4.10: the item's own
/// AttributeTemplateId, else its family's DefaultAttributeTemplateId) and converts
/// inbound JSON values to the typed ItemAttribute columns (§4.11).
/// </summary>
public interface IAttributeResolver
{
    Task<int> ResolveTemplateIdAsync(ItemMaster item, CancellationToken ct);

    Task<IReadOnlyList<TemplateAttribute>> GetTemplateAttributesAsync(int templateId, CancellationToken ct);

    /// <summary>
    /// Writes the supplied values onto the item's ItemAttribute rows, validating each
    /// against its AttributeDefinition.DataType and EnumOptions.
    /// </summary>
    Task ApplyAttributesAsync(ItemMaster item, IEnumerable<AttributeValueDto> values, int actingUserId, CancellationToken ct);

    /// <summary>
    /// SDS §12.1 rule 7 — every IsRequired TemplateAttribute must have a value before
    /// the item may become Active. Throws ATTRIBUTE_REQUIRED_MISSING when one does not.
    /// </summary>
    Task AssertRequiredAttributesPresentAsync(ItemMaster item, CancellationToken ct);
}

public sealed class AttributeResolver : IAttributeResolver
{
    private readonly ItemMasterDbContext _db;

    public AttributeResolver(ItemMasterDbContext db) => _db = db;

    public async Task<int> ResolveTemplateIdAsync(ItemMaster item, CancellationToken ct)
    {
        if (item.AttributeTemplateId is > 0) return item.AttributeTemplateId.Value;

        var familyDefault = await _db.ItemFamilies
            .Where(f => f.ItemFamilyId == item.ItemFamilyId)
            .Select(f => f.DefaultAttributeTemplateId)
            .FirstOrDefaultAsync(ct);

        if (familyDefault is > 0) return familyDefault.Value;

        // SDS §4.6 — creation cannot proceed with no template at all.
        throw ApiException.Unprocessable(
            ErrorCodes.ValidationFailed,
            $"Item family {item.ItemFamilyId} has no default attribute template; attributeTemplateId must be supplied.",
            "attributeTemplateId");
    }

    public async Task<IReadOnlyList<TemplateAttribute>> GetTemplateAttributesAsync(int templateId, CancellationToken ct) =>
        await _db.TemplateAttributes
            .Include(ta => ta.Definition)
            .Where(ta => ta.AttributeTemplateId == templateId && ta.IsActive)
            .OrderBy(ta => ta.DisplayOrder)
            .ToListAsync(ct);

    public async Task ApplyAttributesAsync(ItemMaster item, IEnumerable<AttributeValueDto> values,
                                           int actingUserId, CancellationToken ct)
    {
        var supplied = values.ToList();
        if (supplied.Count == 0) return;

        var templateId = await ResolveTemplateIdAsync(item, ct);
        var allowed = await GetTemplateAttributesAsync(templateId, ct);
        var byCode = allowed.ToDictionary(ta => ta.Definition.AttributeCode, StringComparer.OrdinalIgnoreCase);

        foreach (var value in supplied)
        {
            if (!byCode.TryGetValue(value.AttributeCode, out var templateAttribute))
                throw ApiException.Unprocessable(
                    ErrorCodes.AttributeDataTypeMismatch,
                    $"Attribute '{value.AttributeCode}' is not part of attribute template {templateId}.",
                    value.AttributeCode);

            var definition = templateAttribute.Definition;

            var row = item.Attributes.FirstOrDefault(a => a.AttributeDefinitionId == definition.AttributeDefinitionId);
            if (row is null)
            {
                row = new ItemAttribute
                {
                    ItemId = item.ItemId,
                    AttributeDefinitionId = definition.AttributeDefinitionId,
                    CreatedBy = actingUserId
                };
                item.Attributes.Add(row);
                _db.ItemAttributes.Add(row);
            }
            else
            {
                row.ModifiedBy = actingUserId;
                row.ModifiedDate = DateTime.UtcNow;
            }

            row.ClearValues();
            AssignTypedValue(row, definition, value);
        }
    }

    public async Task AssertRequiredAttributesPresentAsync(ItemMaster item, CancellationToken ct)
    {
        var templateId = await ResolveTemplateIdAsync(item, ct);
        var required = (await GetTemplateAttributesAsync(templateId, ct)).Where(ta => ta.IsRequired).ToList();
        if (required.Count == 0) return;

        var present = await _db.ItemAttributes
            .Where(a => a.ItemId == item.ItemId)
            .Select(a => a.AttributeDefinitionId)
            .ToListAsync(ct);

        var missing = required.Where(ta => !present.Contains(ta.AttributeDefinitionId)).ToList();
        if (missing.Count > 0)
            throw ApiException.Unprocessable(
                ErrorCodes.AttributeRequiredMissing,
                "Required attribute(s) have no value: " +
                string.Join(", ", missing.Select(m => m.Definition.AttributeCode)),
                missing[0].Definition.AttributeCode);
    }

    private static void AssignTypedValue(ItemAttribute row, AttributeDefinition definition, AttributeValueDto supplied)
    {
        var json = supplied.Value;
        var code = definition.AttributeCode;

        switch (definition.DataType)
        {
            case "Text":
                row.ValueText = AsString(json, code);
                break;

            case "Enum":
                var text = AsString(json, code);
                AssertEnumMember(definition, text);
                row.ValueText = text;
                break;

            case "Number":
                row.ValueNumber = json.ValueKind switch
                {
                    JsonValueKind.Number => json.GetDecimal(),
                    JsonValueKind.String when decimal.TryParse(json.GetString(), out var parsed) => parsed,
                    _ => throw Mismatch(code, "Number")
                };
                break;

            case "Boolean":
                row.ValueBoolean = json.ValueKind switch
                {
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    JsonValueKind.String when bool.TryParse(json.GetString(), out var parsed) => parsed,
                    _ => throw Mismatch(code, "Boolean")
                };
                break;

            case "Date":
                var raw = json.ValueKind == JsonValueKind.String ? json.GetString() : null;
                if (raw is null || !DateOnly.TryParse(raw, out var date)) throw Mismatch(code, "Date");
                row.ValueDate = date;
                break;

            default:
                throw Mismatch(code, definition.DataType);
        }
    }

    private static string AsString(JsonElement json, string code) => json.ValueKind switch
    {
        JsonValueKind.String => json.GetString()!,
        JsonValueKind.Number => json.GetRawText(),
        JsonValueKind.True or JsonValueKind.False => json.GetRawText(),
        _ => throw Mismatch(code, "Text")
    };

    private static void AssertEnumMember(AttributeDefinition definition, string value)
    {
        if (string.IsNullOrWhiteSpace(definition.EnumOptions)) return;
        try
        {
            using var doc = JsonDocument.Parse(definition.EnumOptions);
            var options = doc.RootElement.EnumerateArray().Select(e => e.GetString()).ToList();
            if (!options.Contains(value, StringComparer.Ordinal))
                throw ApiException.Unprocessable(
                    ErrorCodes.AttributeDataTypeMismatch,
                    $"'{value}' is not an allowed value for {definition.AttributeCode}. Allowed: {string.Join(", ", options)}.",
                    definition.AttributeCode);
        }
        catch (JsonException)
        {
            // A malformed EnumOptions payload is a data problem, not a caller problem.
        }
    }

    private static ApiException Mismatch(string code, string expected) =>
        ApiException.Unprocessable(
            ErrorCodes.AttributeDataTypeMismatch,
            $"Value supplied for '{code}' does not match its declared DataType '{expected}'.",
            code);
}
