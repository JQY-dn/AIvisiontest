using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace AIvisiontest.Core.Database
{
    /// <summary>
    /// 数据库服务接口（EF Core 版），用于 Prism 依赖注入。
    /// 封装常用 CRUD + 事务，屏蔽 DbContext 细节。
    /// </summary>
    public interface IDatabaseService
    {
        // ── 连接 ────────────────────────────────────────────────────────────────

        /// <summary>测试数据库连接是否正常</summary>
        Task<bool> TestConnectionAsync();

        // ── 单条查询 ─────────────────────────────────────────────────────────────

        /// <summary>按主键查询单条记录</summary>
        Task<T?> GetByIdAsync<T>(object id) where T : class;

        /// <summary>按条件查询第一条（无结果返回 null）</summary>
        Task<T?> FirstOrDefaultAsync<T>(Expression<Func<T, bool>> predicate) where T : class;

        // ── 集合查询 ─────────────────────────────────────────────────────────────

        /// <summary>查询全表</summary>
        Task<List<T>> GetAllAsync<T>() where T : class;

        /// <summary>按条件查询列表</summary>
        Task<List<T>> WhereAsync<T>(Expression<Func<T, bool>> predicate) where T : class;

        /// <summary>
        /// 返回 IQueryable，适合链式拼接复杂查询（分页、排序、Include 等）。
        /// 注意：必须在 using scope 内使用，不能跨请求持有。
        /// </summary>
        IQueryable<T> Query<T>() where T : class;

        // ── 统计 ─────────────────────────────────────────────────────────────────

        /// <summary>按条件统计行数</summary>
        Task<int> CountAsync<T>(Expression<Func<T, bool>>? predicate = null) where T : class;

        /// <summary>按条件判断是否存在</summary>
        Task<bool> AnyAsync<T>(Expression<Func<T, bool>> predicate) where T : class;

        // ── 写入 ─────────────────────────────────────────────────────────────────

        /// <summary>插入单条记录</summary>
        Task InsertAsync<T>(T entity) where T : class;

        /// <summary>批量插入</summary>
        Task InsertRangeAsync<T>(IEnumerable<T> entities) where T : class;

        /// <summary>更新单条记录</summary>
        Task UpdateAsync<T>(T entity) where T : class;

        /// <summary>删除单条记录</summary>
        Task DeleteAsync<T>(T entity) where T : class;

        /// <summary>按条件批量删除</summary>
        Task DeleteWhereAsync<T>(Expression<Func<T, bool>> predicate) where T : class;

        // ── 事务 ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// 在事务中执行一组操作，异常时自动回滚。
        /// action 内可直接使用 IDatabaseService 的其他方法。
        /// </summary>
        Task ExecuteInTransactionAsync(Func<Task> action);

        // ── 原生 SQL ─────────────────────────────────────────────────────────────

        /// <summary>执行原生 SQL，返回受影响行数（INSERT / UPDATE / DELETE）</summary>
        Task<int> ExecuteSqlAsync(string sql, params object[] parameters);
    }
}
