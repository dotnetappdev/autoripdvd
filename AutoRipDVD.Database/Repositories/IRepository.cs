namespace AutoRipDVD.Database.Repositories;

public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(string id);
    Task<List<T>> GetAllAsync();
    Task UpsertAsync(T entity);
    Task DeleteAsync(string id);
}
