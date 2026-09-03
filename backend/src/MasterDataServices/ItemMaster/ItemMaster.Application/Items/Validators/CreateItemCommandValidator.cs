namespace ItemMaster.Application.Items.Validators;

using FluentValidation;
using ItemMaster.Application.Items.Commands;

/// <summary>
/// Input validation for <see cref="CreateItemCommand"/>.
/// </summary>
/// <remarks>
/// <para>
/// These rules deliberately duplicate the guards inside
/// <c>ItemMasterRecord</c>'s constructor. That is not redundancy: the validator
/// reports <i>every</i> problem at once with per-property messages, so a client
/// fixing a form sees the whole list; the domain guard throws on the first
/// violation and exists to keep the aggregate correct no matter who constructs
/// it -- an importer, a test, a future handler that skips this pipeline.
/// </para>
/// <para>
/// Both mirror the CHECK constraints in <c>database/05_schema_item.sql</c>.
/// When a rule changes, all three must move together.
/// </para>
/// </remarks>
public sealed class CreateItemCommandValidator : AbstractValidator<CreateItemCommand>
{
    /// <summary>Declares the ruleset.</summary>
    public CreateItemCommandValidator()
    {
        // CK_ItemMaster_CodeFormat: 3-30 chars, alphanumeric and hyphen only.
        // The domain upper-cases the value, so lowercase input is accepted here
        // and normalised later rather than rejected.
        RuleFor(x => x.ItemCode)
            .NotEmpty().WithMessage("Item Code is required.")
            .Length(3, 30).WithMessage("Item Code must be between 3 and 30 characters.")
            .Matches("^[a-zA-Z0-9-]+$")
                .WithMessage("Item Code can only contain alphanumeric characters and hyphens.");

        RuleFor(x => x.ItemName)
            .NotEmpty().WithMessage("Item Name is required.")
            .MaximumLength(200).WithMessage("Item Name must not exceed 200 characters.");

        // CK_ItemMaster_TransactableFlags: at least one of the three must be set.
        // Declared with a name so the failure reports against a stable key
        // instead of the empty string that a bare RuleFor(x => x) produces.
        RuleFor(x => x)
            .Must(x => x.CanPurchase || x.CanSell || x.CanManufacture)
            .WithName("TransactableFlags")
            .WithMessage(
                "Item must be flagged for at least one transaction type "
                + "(Purchase, Sell, or Manufacture).");

        // Foreign keys into the classification hierarchy and UOM. A zero or
        // negative id can never match a row, so reject it before the round trip.
        RuleFor(x => x.ItemCategoryId)
            .GreaterThan(0).WithMessage("Item Category is required.");

        RuleFor(x => x.ItemGroupId)
            .GreaterThan(0).WithMessage("Item Group is required.");

        RuleFor(x => x.ItemSubGroupId)
            .GreaterThan(0).WithMessage("Item Sub Group is required.");

        RuleFor(x => x.ItemFamilyId)
            .GreaterThan(0).WithMessage("Item Family is required.");

        RuleFor(x => x.BaseUOMId)
            .GreaterThan(0).WithMessage("Base Unit of Measure is required.");
    }
}

/// <summary>
/// Input validation for <see cref="UpdateItemStatusCommand"/>.
/// </summary>
/// <remarks>
/// Checks shape only. Whether a given transition is legal from the item's
/// current state is an aggregate concern and stays in
/// <c>ItemMasterRecord.UpdateStatus</c>, which can see that state.
/// </remarks>
public sealed class UpdateItemStatusCommandValidator
    : AbstractValidator<UpdateItemStatusCommand>
{
    private static readonly string[] ValidStatuses =
        ["Draft", "PendingApproval", "Active", "Inactive", "Obsolete"];

    /// <summary>Declares the ruleset.</summary>
    public UpdateItemStatusCommandValidator()
    {
        RuleFor(x => x.ItemId)
            .GreaterThan(0).WithMessage("A valid Item ID is required.");

        // CK_ItemMaster_Status.
        RuleFor(x => x.NewStatus)
            .NotEmpty().WithMessage("Status is required.")
            .Must(status => ValidStatuses.Contains(status))
            .WithMessage(
                $"Status must be one of: {string.Join(", ", ValidStatuses)}.");
    }
}
