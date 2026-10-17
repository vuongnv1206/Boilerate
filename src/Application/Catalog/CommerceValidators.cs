using FluentValidation;
namespace Boilerate.Application.Catalog;

public sealed class ProductContentValidator : AbstractValidator<ProductContent>
{
    public ProductContentValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Slug).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(20000);
        RuleFor(x => x.CategoryIds).NotNull().Must(x => x is null || x.Count <= 100);
        RuleFor(x => x.Fields).NotNull().Must(x => x is null || x.Count <= 100);
        RuleFor(x => x.Media).NotNull().Must(x => x is null || x.Count <= 30);
        RuleForEach(x => x.Fields).NotNull();
        RuleForEach(x => x.Media).NotNull();
    }
}
public sealed class VariantInputValidator : AbstractValidator<VariantInput>
{
    public VariantInputValidator()
    {
        RuleFor(x => x.Sku).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Options).NotNull().Must(x => x is null || x.Count <= 10);
    }
}
public sealed class OptionInputValidator : AbstractValidator<OptionInput>
{
    public OptionInputValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Values).NotEmpty().Must(x => x is null || x.Count <= 200);
        RuleForEach(x => x.Values).NotNull();
    }
}
public sealed class CreateProductValidator : AbstractValidator<CreateProduct>
{
    public CreateProductValidator()
    {
        RuleFor(x => x.ProductTypeId).NotEmpty();
        RuleFor(x => x.ProductTypeVersion).NotEmpty();
        RuleFor(x => x.Content).NotNull().SetValidator(new ProductContentValidator());
        RuleFor(x => x.Options).NotNull().Must(x => x is null || x.Count <= 10);
        RuleForEach(x => x.Options).NotNull().SetValidator(new OptionInputValidator());
        RuleFor(x => x.Variants).NotEmpty().Must(x => x is null || x.Count <= 1000);
        RuleForEach(x => x.Variants).NotNull().SetValidator(new VariantInputValidator());
    }
}
public sealed class EditProductValidator : AbstractValidator<EditProduct>
{
    public EditProductValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Version).NotEmpty();
        RuleFor(x => x.ProductTypeVersion).NotEmpty();
        RuleFor(x => x.Content).NotNull().SetValidator(new ProductContentValidator());
    }
}
public sealed class AddVariantValidator : AbstractValidator<AddVariant>
{
    public AddVariantValidator()
    {
        RuleFor(x => x.Version).NotEmpty();
        RuleFor(x => x.Variant).NotNull().SetValidator(new VariantInputValidator());
    }
}
public sealed class PreviewVariantsValidator : AbstractValidator<PreviewVariants>
{
    public PreviewVariantsValidator()
    {
        RuleFor(x => x.Options).NotNull().Must(x => x is null || x.Count <= 10);
        RuleForEach(x => x.Options).NotNull().SetValidator(new OptionInputValidator());
    }
}
public sealed class FieldDefinitionInputValidator : AbstractValidator<FieldDefinitionInput>
{
    public FieldDefinitionInputValidator()
    {
        RuleFor(x => x.Code).NotEmpty();
        RuleFor(x => x.Label).NotEmpty();
        RuleFor(x => x.Choices).NotNull().Must(x => x is null || x.Count <= 200);
        RuleForEach(x => x.Choices).NotNull();
    }
}
public sealed class CreateProductTypeValidator : AbstractValidator<CreateProductType>
{
    public CreateProductTypeValidator()
    {
        RuleFor(x => x.Fields).NotNull().Must(x => x is null || x.Count <= 100);
        RuleForEach(x => x.Fields).NotNull().SetValidator(new FieldDefinitionInputValidator());
    }
}
public sealed class EditProductTypeValidator : AbstractValidator<EditProductType>
{
    public EditProductTypeValidator()
    {
        RuleFor(x => x.AddFields).NotNull().Must(x => x is null || x.Count <= 100);
        RuleForEach(x => x.AddFields).NotNull().SetValidator(new FieldDefinitionInputValidator());
        RuleFor(x => x.Fields).NotNull().Must(x => x is null || x.Count <= 100);
        RuleForEach(x => x.Fields).NotNull().ChildRules(field =>
        {
            field.RuleFor(x => x.AddChoices).NotNull().Must(x => x is null || x.Count <= 200);
            field.RuleForEach(x => x.AddChoices).NotNull();
            field.RuleFor(x => x.Choices).NotNull().Must(x => x is null || x.Count <= 200);
            field.RuleForEach(x => x.Choices).NotNull();
        });
    }
}
public sealed class EditOptionValidator : AbstractValidator<EditOption>
{
    public EditOptionValidator()
    {
        RuleFor(x => x.AddValues).NotNull().Must(x => x is null || x.Count <= 200);
        RuleForEach(x => x.AddValues).NotNull();
        RuleFor(x => x.Values).NotNull().Must(x => x is null || x.Count <= 200);
        RuleForEach(x => x.Values).NotNull();
    }
}