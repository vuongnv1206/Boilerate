using Boilerate.Domain.Catalog;
using Boilerate.Domain.Common.Contracts;
using Boilerate.Domain.Common.Exceptions;
namespace Boilerate.Domain.Ordering;

public sealed class Cart : CommerceEntity, IAggregateRoot
{
    private readonly List<CartItem> _items = [];
    public Guid CustomerId { get; private set; }
    public IReadOnlyCollection<CartItem> Items => _items;

    private Cart()
    {
    }

    public Cart(Guid customerId)
    {
        if (customerId == Guid.Empty)
            throw new DomainException("CustomerId is required.");
        CustomerId = customerId;
    }

    public void ReplaceItems(IEnumerable<(Guid VariantId, int Quantity)> items)
    {
        if (items is null)
            throw new DomainException("Cart items are required.");
        var replacement = items.Take(51).ToList();
        if (replacement.Count > 50 || replacement.Any(i => i.VariantId == Guid.Empty || i.Quantity is < 1 or > 999) ||
            replacement.Select(i => i.VariantId).Distinct().Count() != replacement.Count)
            throw new DomainException("A cart accepts at most 50 distinct SKUs with quantities from 1 to 999.");
        var selected = replacement.ToDictionary(i => i.VariantId, i => i.Quantity);
        _items.RemoveAll(i => !selected.ContainsKey(i.VariantId));
        foreach (var item in replacement)
        {
            var existing = _items.SingleOrDefault(i => i.VariantId == item.VariantId);
            if (existing is null)
                _items.Add(new CartItem(Id, item.VariantId, item.Quantity));
            else
                existing.SetQuantity(item.Quantity);
        }
        Touch();
    }
}

public sealed class CartItem
{
    public Guid CartId { get; private set; }
    public Guid VariantId { get; private set; }
    public int Quantity { get; private set; }

    private CartItem()
    {
    }

    internal CartItem(Guid cartId, Guid variantId, int quantity)
    {
        CartId = cartId;
        VariantId = variantId;
        Quantity = quantity;
    }

    internal void SetQuantity(int quantity)
    {
        if (quantity is < 1 or > 999)
            throw new DomainException("Quantity must be from 1 to 999.");
        Quantity = quantity;
    }
}
