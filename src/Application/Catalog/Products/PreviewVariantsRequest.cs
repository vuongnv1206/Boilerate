using Boilerate.Application.Common.Validation;
using Boilerate.Domain.Catalog;
using Boilerate.Domain.Common.Exceptions;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Options;

namespace Boilerate.Application.Catalog.Products;

public sealed record PreviewVariantsRequest(List<OptionInput> Options) : IRequest<List<Dictionary<string, string>>>;

public sealed class PreviewVariantsRequestValidator : CustomValidator<PreviewVariantsRequest>
{
    public PreviewVariantsRequestValidator()
    {
        RuleFor(x => x.Options).NotNull().Must(x => x is null || x.Count <= 10);
        RuleForEach(x => x.Options).NotNull().SetValidator(new OptionInputValidator());
    }
}

public sealed class PreviewVariantsRequestHandler(IOptions<CommerceSettings> settings)
    : IRequestHandler<PreviewVariantsRequest, List<Dictionary<string, string>>>
{
    public Task<List<Dictionary<string, string>>> Handle(PreviewVariantsRequest request, CancellationToken ct)
    {
        int maxOptions = settings.Value.MaximumOptions;
        int maxVariants = settings.Value.MaximumVariants;
        if (request.Options is null || request.Options.Count > maxOptions)
        {
            throw new DomainException("Option limit exceeded.");
        }

        var sample = new Product(Guid.NewGuid(), "Preview", "preview");
        var result = new List<Dictionary<string, string>> { new() };
        foreach (var input in request.Options)
        {
            var option = sample.AddOption(input.Code, input.Name, maxOptions);
            if (input.Values is null || input.Values.Count == 0 || (long)result.Count * input.Values.Count > maxVariants)
            {
                throw new DomainException("Invalid options or combination limit exceeded.");
            }

            foreach (var value in input.Values)
            {
                option.AddValue(value.Code, value.Label);
            }

            result = result
                .SelectMany(existing => option.Values.Select(value =>
                {
                    var next = new Dictionary<string, string>(existing) { { option.Code, value.Code } };
                    return next;
                }))
                .ToList();
        }

        return Task.FromResult(result);
    }
}

