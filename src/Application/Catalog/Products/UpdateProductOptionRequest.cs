using Boilerate.Application.Catalog.ProductTypes;
using Boilerate.Application.Common.Exceptions;
using Boilerate.Application.Common.Interfaces;
using Boilerate.Application.Common.Persistence;
using Boilerate.Application.Common.Validation;
using Boilerate.Application.Identity.Users;
using Boilerate.Domain.Common.Exceptions;
using FluentValidation;
using MediatR;

namespace Boilerate.Application.Catalog.Products;

public sealed record UpdateProductOptionRequest(
    Guid ProductId,
    Guid OptionId,
    string Version,
    string Name,
    int SortOrder,
    List<ChoiceInput> AddValues,
    List<OptionValueEdit> Values) : IRequest<ProductDto>;

public sealed class UpdateProductOptionRequestValidator : CustomValidator<UpdateProductOptionRequest>
{
    public UpdateProductOptionRequestValidator()
    {
        RuleFor(x => x.AddValues).NotNull().Must(x => x is null || x.Count <= 200);
        RuleForEach(x => x.AddValues).NotNull();
        RuleFor(x => x.Values).NotNull().Must(x => x is null || x.Count <= 200);
        RuleForEach(x => x.Values).NotNull();
    }
}

public sealed class UpdateProductOptionRequestHandler(
    IApplicationDbContext db,
    ICommerceUnitOfWork work,
    ICurrentUser actor,
    IUserService users) : IRequestHandler<UpdateProductOptionRequest, ProductDto>
{
    public async Task<ProductDto> Handle(UpdateProductOptionRequest request, CancellationToken ct)
    {
        await work.ExecuteAsync(async () =>
        {
            var (product, type) = await ProductPersistenceHelper.LockProductAsync(db, request.ProductId, request.Version, ct);
            var option = product.Options.SingleOrDefault(o => o.Id == request.OptionId)
                ?? throw new NotFoundException("Option not found.");
            option.Rename(request.Name, request.SortOrder);
            foreach (var add in request.AddValues)
            {
                option.AddValue(add.Code, add.Label);
            }

            foreach (var edit in request.Values)
            {
                (option.Values.SingleOrDefault(v => v.Id == edit.Id) ?? throw new DomainException("Unknown option value."))
                    .Rename(edit.Label, edit.SortOrder);
            }

            product.Touch();
            product.Validate(type);
            return true;
        }, ct);

        return await ProductDetailReader.ReadProductAsync(db, actor, users, request.ProductId, false, ct);
    }
}

