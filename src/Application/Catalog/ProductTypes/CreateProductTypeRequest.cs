using Boilerate.Application.Common.Persistence;
using Boilerate.Application.Common.Validation;
using Boilerate.Domain.Catalog;
using FluentValidation;
using MediatR;

namespace Boilerate.Application.Catalog.ProductTypes;

public sealed record CreateProductTypeRequest(
    string Code,
    string Name,
    List<FieldDefinitionInput> Fields) : IRequest<ProductType>;

public sealed class CreateProductTypeRequestValidator : CustomValidator<CreateProductTypeRequest>
{
    public CreateProductTypeRequestValidator()
    {
        RuleFor(x => x.Fields).NotNull().Must(x => x is null || x.Count <= 100);
        RuleForEach(x => x.Fields).NotNull().SetValidator(new FieldDefinitionInputValidator());
    }
}

public sealed class CreateProductTypeRequestHandler(
    IApplicationDbContext db,
    ICommerceUnitOfWork work) : IRequestHandler<CreateProductTypeRequest, ProductType>
{
    public Task<ProductType> Handle(CreateProductTypeRequest request, CancellationToken ct) =>
        work.ExecuteAsync(() =>
        {
            var type = new ProductType(request.Code, request.Name);
            ProductTypeFieldHelper.AddFields(type, request.Fields, false);
            db.ProductTypes.Add(type);
            return Task.FromResult(type);
        }, ct);
}
