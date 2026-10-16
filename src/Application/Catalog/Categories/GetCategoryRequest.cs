using Boilerate.Application.Common.Caching;
using Boilerate.Application.Common.Exceptions;
using Boilerate.Application.Common.Persistence;
using Boilerate.Domain.Catalog;
using Mapster;
using MediatR;

namespace Boilerate.Application.Catalog.Categories;

public class GetCategoryRequest : IRequest<CategoryDto>
{
    public Guid Id { get; set; }

    public GetCategoryRequest(Guid id) => Id = id;
}

public class GetCategoryRequestHandler : IRequestHandler<GetCategoryRequest, CategoryDto>
{
    private readonly IReadRepository<Category> _repository;
    private readonly ICacheService _cache;

    public GetCategoryRequestHandler(IReadRepository<Category> repository, ICacheService cache)
    {
        _repository = repository;
        _cache = cache;
    }

    public async Task<CategoryDto> Handle(GetCategoryRequest request, CancellationToken cancellationToken)
    {
        var cacheKey = $"Category:{request.Id}";
        var categoryDto = await _cache.GetAsync<CategoryDto>(cacheKey, cancellationToken);

        if (categoryDto == null)
        {
            var category = await _repository.GetByIdAsync(request.Id, cancellationToken)
                ?? throw new NotFoundException($"Không tìm thấy danh mục với Id {request.Id}.");

            categoryDto = category.Adapt<CategoryDto>();
            await _cache.SetAsync(cacheKey, categoryDto, TimeSpan.FromMinutes(30), cancellationToken);
        }

        return categoryDto;
    }
}
