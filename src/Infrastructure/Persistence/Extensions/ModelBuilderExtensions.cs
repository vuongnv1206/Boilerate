using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using System.Linq.Expressions;

namespace Boilerate.Infrastructure.Persistence.Extensions;

/// <summary>
/// Extension methods for ModelBuilder to work with global query filters.
/// </summary>
public static class ModelBuilderExtensions
{
    /// <summary>
    /// Apply global query filter cho tất cả entities implement một interface.
    /// Sử dụng ReplacingExpressionVisitor để thay thế parameter trực tiếp, giúp SQL sinh ra sạch hơn.
    /// </summary>
    public static ModelBuilder AppendGlobalQueryFilter<TInterface>(this ModelBuilder modelBuilder, Expression<Func<TInterface, bool>> filter)
    {
        // Lấy danh sách các entities là Root (BaseType null) và có triển khai TInterface
        var entities = modelBuilder.Model.GetEntityTypes()
            .Where(e => e.BaseType is null && typeof(TInterface).IsAssignableFrom(e.ClrType))
            .Select(e => e.ClrType);

        foreach (var entity in entities)
        {
            var parameterType = Expression.Parameter(entity);
            
            // Thay thế parameter của filter (TInterface) bằng parameter của thực thể thực tế
            var filterBody = ReplacingExpressionVisitor.Replace(filter.Parameters.Single(), parameterType, filter.Body);

            // Kiểm tra xem đã có Query Filter nào trước đó chưa
            if (modelBuilder.Entity(entity).Metadata.GetDeclaredQueryFilters().SingleOrDefault(f => f.IsAnonymous)?.Expression is { } existingFilter)
            {
                var existingFilterBody = ReplacingExpressionVisitor.Replace(existingFilter.Parameters.Single(), parameterType, existingFilter.Body);

                // Kết hợp filter cũ và filter mới bằng phép AND
                filterBody = Expression.AndAlso(existingFilterBody, filterBody);
            }

            // Áp dụng filter cuối cùng
            modelBuilder.Entity(entity).HasQueryFilter(Expression.Lambda(filterBody, parameterType));
        }

        return modelBuilder;
    }
}
