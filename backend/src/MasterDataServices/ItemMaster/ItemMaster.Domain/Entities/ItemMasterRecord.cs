using Weavo.BuildingBlocks.Kernel;

namespace ItemMaster.Domain.Entities;

public class ItemMasterRecord : AggregateRoot<Guid>
{
    public string Name { get; private set; } = string.Empty;
    public decimal BasePrice { get; private set; }
    public bool IsActive { get; private set; }

    // EF Core parameterless constructor
    private ItemMasterRecord() { }

    public ItemMasterRecord(Guid id, string name, decimal basePrice) : base(id)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name is required");
        if (basePrice < 0) throw new ArgumentException("Price cannot be negative");

        Name = name;
        BasePrice = basePrice;
        IsActive = true;

        // Example: RaiseDomainEvent(new ItemCreatedDomainEvent(Id, Name));
    }

    public void UpdatePrice(decimal newPrice)
    {
        if (newPrice < 0) throw new ArgumentException("Price cannot be negative");
        BasePrice = newPrice;
        
        // Example: RaiseDomainEvent(new ItemPriceChangedDomainEvent(Id, newPrice));
    }

    public void Deactivate()
    {
        if (!IsActive) return;
        IsActive = false;
    }
}
