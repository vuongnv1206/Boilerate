using Boilerate.Application.Common.Validation;
using Boilerate.Domain.Catalog;
using FluentValidation;

namespace Boilerate.Application.Catalog.ProductTypes;

public sealed record ChoiceInput(string Code, string Label);

public sealed record FieldDefinitionInput(
    string Code,
    ProductFieldType DataType,
    string Label,
    string? HelpText,
    bool IsRequired,
    bool IsFilterable,
    List<ChoiceInput> Choices);

public sealed record FieldChoiceEdit(Guid Id, string Label, bool IsActive, int SortOrder);

public sealed record FieldDefinitionEdit(
    Guid Id,
    string Label,
    string? HelpText,
    bool IsFilterable,
    bool IsActive,
    int SortOrder,
    List<ChoiceInput> AddChoices,
    List<FieldChoiceEdit> Choices);

public sealed class FieldDefinitionInputValidator : CustomValidator<FieldDefinitionInput>
{
    public FieldDefinitionInputValidator()
    {
        RuleFor(x => x.Code).MaximumLength(100);
        RuleFor(x => x.Label).NotEmpty();
        RuleFor(x => x.Choices).NotNull().Must(x => x is null || x.Count <= 200);
        RuleForEach(x => x.Choices).NotNull();
    }
}

internal static class ProductTypeFieldHelper
{
    internal static void AddFields(ProductType type, IEnumerable<FieldDefinitionInput> fields, bool used)
    {
        foreach (var input in fields)
        {
            var field = type.AddField(
                input.Code,
                input.DataType,
                input.Label,
                input.HelpText,
                input.IsRequired,
                input.IsFilterable,
                used);
            foreach (var choice in input.Choices)
            {
                field.AddChoice(choice.Code, choice.Label);
            }
        }
    }
}
