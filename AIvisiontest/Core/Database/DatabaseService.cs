using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using AIvisiontest.Core.Logging;

namespace AIvisiontest.Core.Database
{
    /// <summary>
    /// 数据库服务实现（EF Core + SQL Server）。
    /// 每次操作通过工厂创建独立的 DbContext（短生命周期），
    /// 避免 WPF 长生命周期单例中 DbContext 状态污染的问题。
    /// </summary>
    public class DatabaseService : IDatabaseService
    {
        // ── 字段 ─────────────────────────────────────────────────────────────────

        private readonly IDbContextFactory<AppDbContext> _factory;
        private readonly ILogService? _log;

        // 事务专用：同一事务内共享同一个 DbContext
        private AppDbContext? _transactionContext;
        private IDbContextTransaction? _currentTransaction;

        // ── 构造函数 ─────────────────────────────────────────────────────────────

        public DatabaseService(IDbContextFactory<AppDbContext> factory, ILogService? log = null)
        {
            _factory = factory;
            _log = log;
        }

        // ── 获取 DbContext（内部使用）────────────────────────────────────────────

        /// <summary>
        /// 事务期间返回共享 Context；否则创建新的短生命周期 Context。
        /// </summary>
        private AppDbContext GetContext() => _transactionContext ?? _factory.CreateDbContext();

        /// <summary>
        /// 非事务 Context 用完后需要释放。
        /// </summary>
        private void ReleaseContext(AppDbContext ctx)
        {
            if (_transactionContext == null) // 不是事务 Context 才释放
                ctx.Dispose();
        }

        // ── 连接 ─────────────────────────────────────────────────────────────────

        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                await using var ctx = _factory.CreateDbContext();
                var canConnect = await ctx.Database.CanConnectAsync();
                _log?.Info(canConnect ? "数据库连接正常" : "数据库无法连接", tag: nameof(DatabaseService));
                return canConnect;
            }
            catch (Exception ex)
            {
                _log?.Error("数据库连接测试失败", ex, tag: nameof(DatabaseService));
                return false;
            }
        }

        // ── 单条查询 ─────────────────────────────────────────────────────────────

        public async Task<T?> GetByIdAsync<T>(object id) where T : class
        {
            var ctx = GetContext();
            try { return await ctx.Set<T>().FindAsync(id); }
            catch (Exception ex) { _log?.Error($"GetByIdAsync<{typeof(T).Name}> 失败", ex, tag: nameof(DatabaseService)); throw; }
            finally { ReleaseContext(ctx); }
        }

        public async Task<T?> FirstOrDefaultAsync<T>(Expression<Func<T, bool>> predicate) where T : class
        {
            var ctx = GetContext();
            try { return await ctx.Set<T>().FirstOrDefaultAsync(predicate); }
            catch (Exception ex) { _log?.Error($"FirstOrDefaultAsync<{typeof(T).Name}> 失败", ex, tag: nameof(DatabaseService)); throw; }
            finally { ReleaseContext(ctx); }
        }

        // ── 集合查询 ─────────────────────────────────────────────────────────────

        public async Task<List<T>> GetAllAsync<T>() where T : class
        {
            var ctx = GetContext();
            try { return await ctx.Set<T>().ToListAsync(); }
            catch (Exception ex) { _log?.Error($"GetAllAsync<{typeof(T).Name}> 失败", ex, tag: nameof(DatabaseService)); throw; }
            finally { ReleaseContext(ctx); }
        }

        public async Task<List<T>> WhereAsync<T>(Expression<Func<T, bool>> predicate) where T : class
        {
            var ctx = GetContext();
            try { return await ctx.Set<T>().Where(predicate).ToListAsync(); }
            catch (Exception ex) { _log?.Error($"WhereAsync<{typeof(T).Name}> 失败", ex, tag: nameof(DatabaseService)); throw; }
            finally { ReleaseContext(ctx); }
        }

        /// <summary>
        /// 返回 IQueryable，适合在 ViewModel 层自由拼接（分页、排序、Include）。
        /// 使用示例：
        ///   var list = await _db.Query&lt;Product&gt;()
        ///       .Where(p => p.Price > 100)
        ///       .OrderBy(p => p.Name)
        ///       .Skip(0).Take(20)
        ///       .ToListAsync();
        /// </summary>
        public IQueryable<T> Query<T>() where T : class
        {
            // 注意：此处 Context 不释放，由调用方负责执行后 GC
            // 建议配合 AsNoTracking() 使用以提升只读查询性能
            return _factory.CreateDbContext().Set<T>().AsNoTracking();
        }

        // ── 统计 ─────────────────────────────────────────────────────────────────

        public async Task<int> CountAsync<T>(Expression<Func<T, bool>>? predicate = null) where T : class
        {
            var ctx = GetContext();
            try
            {
                return predicate == null
                    ? await ctx.Set<T>().CountAsync()
                    : await ctx.Set<T>().CountAsync(predicate);
            }
            catch (Exception ex) { _log?.Error($"CountAsync<{typeof(T).Name}> 失败", ex, tag: nameof(DatabaseService)); throw; }
            finally { ReleaseContext(ctx); }
        }

        public async Task<bool> AnyAsync<T>(Expression<Func<T, bool>> predicate) where T : class
        {
            var ctx = GetContext();
            try { return await ctx.Set<T>().AnyAsync(predicate); }
            catch (Exception ex) { _log?.Error($"AnyAsync<{typeof(T).Name}> 失败", ex, tag: nameof(DatabaseService)); throw; }
            finally { ReleaseContext(ctx); }
        }

        // ── 写入 ─────────────────────────────────────────────────────────────────

        public async Task InsertAsync<T>(T entity) where T : class
        {
            var ctx = GetContext();
            try
            {
                await ctx.Set<T>().AddAsync(entity);
                await ctx.SaveChangesAsync();
            }
            catch (Exception ex) { _log?.Error($"InsertAsync<{typeof(T).Name}> 失败", ex, tag: nameof(DatabaseService)); throw; }
            finally { ReleaseContext(ctx); }
        }

        public async Task InsertRangeAsync<T>(IEnumerable<T> entities) where T : class
        {
            var ctx = GetContext();
            try
            {
                await ctx.Set<T>().AddRangeAsync(entities);
                await ctx.SaveChangesAsync();
            }
            catch (Exception ex) { _log?.Error($"InsertRangeAsync<{typeof(T).Name}> 失败", ex, tag: nameof(DatabaseService)); throw; }
            finally { ReleaseContext(ctx); }
        }

        public async Task UpdateAsync<T>(T entity) where T : class
        {
            var ctx = GetContext();
            try
            {
                ctx.Set<T>().Update(entity);
                await ctx.SaveChangesAsync();
            }
            catch (Exception ex) { _log?.Error($"UpdateAsync<{typeof(T).Name}> 失败", ex, tag: nameof(DatabaseService)); throw; }
            finally { ReleaseContext(ctx); }
        }

        public async Task DeleteAsync<T>(T entity) where T : class
        {
            var ctx = GetContext();
            try
            {
                ctx.Set<T>().Remove(entity);
                await ctx.SaveChangesAsync();
            }
            catch (Exception ex) { _log?.Error($"DeleteAsync<{typeof(T).Name}> 失败", ex, tag: nameof(DatabaseService)); throw; }
            finally { ReleaseContext(ctx); }
        }

        public async Task DeleteWhereAsync<T>(Expression<Func<T, bool>> predicate) where T : class
        {
            var ctx = GetContext();
            try
            {
                var items = await ctx.Set<T>().Where(predicate).ToListAsync();
                ctx.Set<T>().RemoveRange(items);
                await ctx.SaveChangesAsync();
            }
            catch (Exception ex) { _log?.Error($"DeleteWhereAsync<{typeof(T).Name}> 失败", ex, tag: nameof(DatabaseService)); throw; }
            finally { ReleaseContext(ctx); }
        }

        // ── 事务 ─────────────────────────────────────────────────────────────────

        public async Task ExecuteInTransactionAsync(Func<Task> action)
        {
            // 创建事务专用 Context，整个事务期间共享
            _transactionContext = _factory.CreateDbContext();
            _currentTransaction = await _transactionContext.Database.BeginTransactionAsync();

            try
            {
                await action();
                await _currentTransaction.CommitAsync();
                _log?.Info("事务提交成功", tag: nameof(DatabaseService));
            }
            catch (Exception ex)
            {
                await _currentTransaction.RollbackAsync();
                _log?.Error("事务执行失败，已回滚", ex, tag: nameof(DatabaseService));
                throw;
            }
            finally
            {
                await _currentTransaction.DisposeAsync();
                await _transactionContext.DisposeAsync();
                _currentTransaction = null;
                _transactionContext = null;
            }
        }

        // ── 原生 SQL ─────────────────────────────────────────────────────────────

        public async Task<int> ExecuteSqlAsync(string sql, params object[] parameters)
        {
            var ctx = GetContext();
            try { return await ctx.Database.ExecuteSqlRawAsync(sql, parameters); }
            catch (Exception ex) { _log?.Error($"ExecuteSqlAsync 失败: {sql}", ex, tag: nameof(DatabaseService)); throw; }
            finally { ReleaseContext(ctx); }
        }
    }
}
