namespace Wms.Application.Features.Stock.Idempotency;

public interface IIdempotencyService
{
    /// <summary>
    /// Memastikan operasi dijalankan tepat satu kali.
    /// Jika key sudah ada untuk user dan operasi ini, mengembalikan hasil yang disimpan sebelumnya.
    /// </summary>
    Task<TResult> ExecuteAsync<TResult>(
        Guid userId,
        string operation,
        string key,
        Func<Task<TResult>> action,
        CancellationToken cancellationToken = default);
}

