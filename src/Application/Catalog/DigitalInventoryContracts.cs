using Boilerate.Domain.Catalog;
using Boilerate.Domain.DigitalInventory;
using MediatR;

namespace Boilerate.Application.Catalog;

public sealed record DigitalStockInput(string Reference, string Content);
public sealed record ImportDigitalStock(Guid ProductId, Guid VariantId, string Version, List<DigitalStockInput> Items) : IRequest<List<DigitalStockDto>>;
public sealed record SaveSharedDigitalContent(Guid ProductId, Guid VariantId, string Version, string Content, bool IsActive = true) : IRequest<DigitalStockDto>;
public sealed record SetDigitalStockAvailability(Guid ProductId, Guid VariantId, Guid StockItemId, string Version, bool IsAvailable) : IRequest<DigitalStockDto>;
public sealed record DigitalStockDto(Guid Id, Guid ProductId, Guid VariantId, ProductFulfillmentKind FulfillmentKind,
    string Reference, DigitalStockState State, string Version, string ProductVersion);
public sealed record DigitalSecretDto(Guid Id, string Content);

public sealed class ImportDigitalStockHandler(ICommerceService service) : IRequestHandler<ImportDigitalStock, List<DigitalStockDto>>
{
    public Task<List<DigitalStockDto>> Handle(ImportDigitalStock request, CancellationToken ct) => service.ImportDigitalAsync(request, ct);
}
public sealed class SaveSharedDigitalContentHandler(ICommerceService service) : IRequestHandler<SaveSharedDigitalContent, DigitalStockDto>
{
    public Task<DigitalStockDto> Handle(SaveSharedDigitalContent request, CancellationToken ct) => service.SaveSharedAsync(request, ct);
}
public sealed class SetDigitalStockAvailabilityHandler(ICommerceService service) : IRequestHandler<SetDigitalStockAvailability, DigitalStockDto>
{
    public Task<DigitalStockDto> Handle(SetDigitalStockAvailability request, CancellationToken ct) => service.SetDigitalStockAvailabilityAsync(request, ct);
}
