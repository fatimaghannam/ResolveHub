using System.Linq.Expressions;

namespace ResolveHub.Api.Infrastructure;

public static class UtcDateRangeExtensions
{
    public static IQueryable<T> ApplyUtcDateRange<T>(this IQueryable<T> query,
        DateTimeOffset? fromUtc, DateTimeOffset? toUtcExclusive,
        Expression<Func<T, DateTime>> timestamp)
    {
        if (fromUtc is not null)
        {
            var lowerBound = Expression.GreaterThanOrEqual(timestamp.Body,
                Expression.Constant(fromUtc.Value.UtcDateTime));
            query = query.Where(Expression.Lambda<Func<T, bool>>(
                lowerBound, timestamp.Parameters));
        }

        if (toUtcExclusive is not null)
        {
            var upperBound = Expression.LessThan(timestamp.Body,
                Expression.Constant(toUtcExclusive.Value.UtcDateTime));
            query = query.Where(Expression.Lambda<Func<T, bool>>(
                upperBound, timestamp.Parameters));
        }

        return query;
    }
}
