using Boilerate.Application.Common.Exceptions;
using Boilerate.Application.Common.Persistence;
using Boilerate.Application.Common.Validation;
using Boilerate.Domain.Catalog;
using Boilerate.Domain.Common.Exceptions;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Boilerate.Application.Catalog.ProductTypes;

public sealed record UpdateProductTypeRequest(
    Guid Id,
    string Version,
    string Name,
    bool IsActive,
    List<FieldDefinitionInput> AddFields,
    List<FieldDefinitionEdit> Fields) : IRequest<ProductType>;

public sealed class UpdateProductTypeRequestValidator : CustomValidator<UpdateProductTypeRequest>
{
    public UpdateProductTypeRequestValidator()
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

public sealed class UpdateProductTypeRequestHandler(
    IApplicationDbContext db,
    ICommerceUnitOfWork work) : IRequestHandler<UpdateProductTypeRequest, ProductType>
{
    public Task<ProductType> Handle(UpdateProductTypeRequest request, CancellationToken ct) =>
        work.ExecuteAsync(async () =>
        {
            var type = await CommerceConcurrency.LockProductTypeAsync(db, request.Id, ct);
            CommerceConcurrency.CheckVersion(request.Version, type.Version);
            if (!request.IsActive && await db.Products.AnyAsync(p => p.ProductTypeId == type.Id && p.Status == ProductStatus.Published, ct))
            {
                throw new ConflictException("Unpublish products before deactivating their type.");
            }

            type.Edit(request.Name, request.IsActive);
            ProductTypeFieldHelper.AddFields(
                type,
                request.AddFields,
                await db.Products.AnyAsync(p => p.ProductTypeId == type.Id, ct));
            foreach (var edit in request.Fields)
            {
                var field = type.Fields.SingleOrDefault(f => f.Id == edit.Id)
                    ?? throw new DomainException("Unknown field.");
                if (!field.IsActive && edit.IsActive && field.IsRequired &&
                    await db.Products.AnyAsync(
                        p => p.ProductTypeId == type.Id && p.Status == ProductStatus.Published && !p.FieldValues.Any(v => v.FieldDefinitionId == field.Id),
                        ct))
                {
                    throw new ConflictException("Published products must have values before reactivating a required field.");
                }

                field.Edit(edit.Label, edit.HelpText, edit.IsFilterable, edit.IsActive, edit.SortOrder);
                foreach (var choice in edit.AddChoices)
                {
                    field.AddChoice(choice.Code, choice.Label);
                }

                foreach (var choice in edit.Choices)
                {
                    (field.Choices.SingleOrDefault(c => c.Id == choice.Id) ?? throw new DomainException("Unknown choice."))
                        .Edit(choice.Label, choice.IsActive, choice.SortOrder);
                }
            }

            return type;
        }, ct);
}
