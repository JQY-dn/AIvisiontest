using AIvisiontest.Core.Database;
using AIvisiontest.Core.Logging;
using AIvisiontest.ViewModels;
using AIvisiontest.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Prism.Ioc;
using Serilog;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;

namespace AIvisiontest
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App
    {

        // 程序启动时初始化 Serilog
        protected override void OnStartup(StartupEventArgs e)
        {

            base.OnStartup(e);
            var log = Container.Resolve<ILogService>();

            DispatcherUnhandledException += (s, ex) =>
            {
                log.Fatal("UI线程未处理异常", ex.Exception, tag: "GlobalHandler");
                ex.Handled = true; // 阻止程序崩溃
            };

            TaskScheduler.UnobservedTaskException += (s, ex) =>
            {
                log.Fatal("后台Task未处理异常", ex.Exception, tag: "GlobalHandler");
                ex.SetObserved();
            };
        }
        protected override Window CreateShell()
        {
            return Container.Resolve<MainWindow>();
        }

        protected override void RegisterTypes(IContainerRegistry containerRegistry)
        {
            // ── 日志（单例）──────────────────────────────────────────────────
            containerRegistry.RegisterInstance<ILogService>(new LogService()
            {
                DedupWindow = TimeSpan.FromSeconds(10),
                DedupThreshold = 3,
                MinimumLevel = LogLevel.Debug,
                MaxFileSizeBytes = 10 * 1024 * 1024,
                RetainDays = 30
            });

            // ── EF Core（单例）───────────────────────────────────────────────
            var connectionString = ConnectionStringProvider.Get(); ;

            var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlServer(connectionString)
                .Options;

            var dbFactory = new PooledDbContextFactory<AppDbContext>(dbOptions);
            var logService = Container.Resolve<ILogService>();

            containerRegistry.RegisterInstance<IDbContextFactory<AppDbContext>>(dbFactory);
            containerRegistry.RegisterInstance<IDatabaseService>(new DatabaseService(dbFactory, logService));

            // ── 导航 ─────────────────────────────────────────────────────────
            containerRegistry.RegisterForNavigation<MainWindow, MainWindowViewModel>();
        }
        // 程序退出时释放 Serilog 资源
        protected override void OnExit(ExitEventArgs e)
        { 

            base.OnExit(e);
        }
    }
}
