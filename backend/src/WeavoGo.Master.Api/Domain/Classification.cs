namespace WeavoGo.Master.Api.Domain;

/// <summary>SDS §4.3 — top level of the classification hierarchy.</summary>
public class ItemCategory : AuditableEntity
{
    public int ItemCategoryId { get; set; }
    public string CategoryCode { get; set; } = null!;
    public string CategoryName { get; set; } = null!;
    public string Nature { get; set; } = null!;      // Material | Goods | Asset | Service
    public string? Description { get; set; }
    public int DisplayOrder { get; set; }

    public ICollection<ItemGroup> Groups { get; set; } = new List<ItemGroup>();
}

/// <summary>SDS §4.4.</summary>
public class ItemGroup : AuditableEntity
{
    public int ItemGroupId { get; set; }
    public int ItemCategoryId { get; set; }
    public string GroupCode { get; set; } = null!;
    public string GroupName { get; set; } = null!;
    public string? Description { get; set; }
    public int DisplayOrder { get; set; }

    public ItemCategory Category { get; set; } = null!;
    public ICollection<ItemSubGroup> SubGroups { get; set; } = new List<ItemSubGroup>();
}

/// <summary>SDS §4.5.</summary>
public class ItemSubGroup : AuditableEntity
{
    public int ItemSubGroupId { get; set; }
    public int ItemGroupId { get; set; }
    public string SubGroupCode { get; set; } = null!;
    public string SubGroupName { get; set; } = null!;
    public string? Description { get; set; }
    public int DisplayOrder { get; set; }

    public ItemGroup Group { get; set; } = null!;
    public ICollection<ItemFamily> Families { get; set; } = new List<ItemFamily>();
}

/// <summary>SDS §4.6 — carries the family's default attribute template.</summary>
public class ItemFamily : AuditableEntity
{
    public int ItemFamilyId { get; set; }
    public int ItemSubGroupId { get; set; }
    public string FamilyCode { get; set; } = null!;
    public string FamilyName { get; set; } = null!;
    public int? DefaultAttributeTemplateId { get; set; }
    public int DisplayOrder { get; set; }

    public ItemSubGroup SubGroup { get; set; } = null!;
    public AttributeTemplate? DefaultAttributeTemplate { get; set; }
}

/// <summary>SDS §4.7 — the shared, category-agnostic attribute dictionary.</summary>
public class AttributeDefinition : AuditableEntity
{
    public int AttributeDefinitionId { get; set; }
    public string AttributeCode { get; set; } = null!;
    public string AttributeName { get; set; } = null!;
    public string DataType { get; set; } = null!;    // Text | Number | Boolean | Date | Enum
    public string? UnitOfMeasure { get; set; }
    public string? EnumOptions { get; set; }         // JSON array, required when DataType = 'Enum'
}

/// <summary>SDS §4.8.</summary>
public class AttributeTemplate : AuditableEntity
{
    public int AttributeTemplateId { get; set; }
    public string TemplateCode { get; set; } = null!;
    public string TemplateName { get; set; } = null!;
    public string? Description { get; set; }

    public ICollection<TemplateAttribute> TemplateAttributes { get; set; } = new List<TemplateAttribute>();
}

/// <summary>SDS §4.9 — which attributes belong to a template, in what order.</summary>
public class TemplateAttribute : AuditableEntity
{
    public int TemplateAttributeId { get; set; }
    public int AttributeTemplateId { get; set; }
    public int AttributeDefinitionId { get; set; }
    public bool IsRequired { get; set; } = true;
    public int DisplayOrder { get; set; }
    public string? DefaultValue { get; set; }

    public AttributeTemplate Template { get; set; } = null!;
    public AttributeDefinition Definition { get; set; } = null!;
}
