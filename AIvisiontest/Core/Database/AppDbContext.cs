using AIvisiontest.Core.Logging;
using AIvisiontest.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Logging;

namespace AIvisiontest.Core.Database
{
    /// <summary>
    /// EF Core 数据库上下文。
    /// 新增实体时：
    ///   1. 在此文件添加 DbSet&lt;T&gt;
    ///   2. 运行 CLI：dotnet ef migrations add &lt;MigrationName&gt;
    ///   3. 运行 CLI：dotnet ef database update
    /// </summary>
    public class AppDbContext : DbContext
    {
        // ── DbSet（每张表对应一个） ───────────────────────────────────────────────
        // 示例：public DbSet<Product> Products => Set<Product>();
        // 在此处添加你的实体 ↓


        /// <summary>批次表</summary>
        public DbSet<Lot> Lots => Set<Lot>();

        /// <summary>检测设备表</summary>
        public DbSet<Equipment> Equipments => Set<Equipment>();

        /// <summary>晶圆检测结果表</summary>
        public DbSet<WaferInspectionResult> WaferInspectionResults => Set<WaferInspectionResult>();


        // ── 构造函数 ─────────────────────────────────────────────────────────────

        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        // ── 模型配置 ─────────────────────────────────────────────────────────────

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // 自动扫描当前程序集中所有继承 IEntityTypeConfiguration<T> 的配置类
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        }
    }

    // ── Design-Time 工厂（CLI 迁移用）────────────────────────────────────────────
    // dotnet ef migrations add / database update 时会调用此工厂创建 DbContext
    // 不需要运行时 DI，只需要提供连接字符串即可

    /// <summary>
    /// 仅供 EF CLI 工具使用，不参与运行时 DI。
    /// </summary>
    public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();

            // ⚠️ 仅用于 CLI 迁移，请替换为你的开发连接字符串
            optionsBuilder.UseSqlServer(
                ConnectionStringProvider.Get());

            return new AppDbContext(optionsBuilder.Options);
        }
    }
}
