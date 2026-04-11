using System.Linq.Expressions;

namespace HrSystemApp.Application.Interfaces.Repositories;

/// <summary>
/// Generic repository interface
/// </summary>
public interface IRepository<T> where T : class
{
    /// <summary>
/// Retrieves an entity by its identifier.
/// </summary>
/// <param name="id">The identifier of the entity to retrieve.</param>
/// <param name="cancellationToken">Token to observe while waiting for the operation to complete.</param>
/// <returns>The entity with the specified identifier, or null if no matching entity exists.</returns>
Task<T?> GetByIdAsync(object id, CancellationToken cancellationToken = default);
    /// <summary>
/// Retrieves all entities of type T as a read-only list.
/// </summary>
/// <param name="cancellationToken">Token to cancel the operation.</param>
/// <returns>A read-only list containing all entities of type T.</returns>
Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default);
    /// <summary>
/// Retrieves entities that match the given predicate.
/// </summary>
/// <param name="predicate">Expression used to filter entities.</param>
/// <param name="cancellationToken">Token to cancel the operation.</param>
/// <returns>A read-only list of entities that satisfy the predicate; an empty list if no matches are found.</returns>
Task<IReadOnlyList<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);
    /// <summary>
/// Retrieves entities that satisfy the specified filter and optionally includes related properties.
/// </summary>
/// <param name="predicate">Expression used to filter which entities are returned.</param>
/// <param name="cancellationToken">Token to observe while waiting for the task to complete.</param>
/// <param name="includes">One or more expressions specifying related properties to include in the query results.</param>
/// <returns>A read-only list of entities that match the predicate.</returns>
Task<IReadOnlyList<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default, params Expression<Func<T, object>>[] includes);
    /// <summary>
/// Adds the provided entity to the repository and returns the added entity.
/// </summary>
/// <param name="entity">The entity to add.</param>
/// <param name="cancellationToken">A token to cancel the operation.</param>
/// <returns>The added entity, potentially with updated values (for example, generated identifiers).</returns>
Task<T> AddAsync(T entity, CancellationToken cancellationToken = default);
    /// <summary>
/// Updates an existing entity in the repository.
/// </summary>
/// <param name="entity">The entity to update.</param>
/// <param name="cancellationToken">A token to cancel the operation.</param>
Task UpdateAsync(T entity, CancellationToken cancellationToken = default);
    /// <summary>
/// Deletes the specified entity from the repository.
/// </summary>
/// <param name="entity">The entity to delete.</param>
Task DeleteAsync(T entity, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);
    Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken cancellationToken = default);
}
