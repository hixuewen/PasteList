using System.Configuration;
using System.Data;
using System.Windows;
using System;
using System.IO;
using System.Threading;

namespace PasteList
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : System.Windows.Application
    {
        // 用于确保只有一个实例运行的互斥体
        private static Mutex? _mutex;
        private const string AppMutexName = "PasteList_SingleInstance_Mutex";
        
        /// <summary>
        /// 应用程序启动时的初始化
        /// </summary>
        protected override void OnStartup(StartupEventArgs e)
        {
            // 确保只有一个实例运行
            _mutex = new Mutex(true, AppMutexName, out bool isNewInstance);

            if (!isNewInstance)
            {
                // 如果已有实例运行，显示提示并退出
                MessageBox.Show("剪贴板历史记录应用已经在运行了！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                Shutdown();
                return;
            }

            // 添加全局异常处理
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            base.OnStartup(e);
        }
        
        /// <summary>
        /// 处理UI线程未处理的异常
        /// </summary>
        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            LogException(e.Exception);
            MessageBox.Show($"应用程序发生错误：{e.Exception.Message}\n\n详细信息已记录到日志文件。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        }
        
        /// <summary>
        /// 处理非UI线程未处理的异常
        /// </summary>
        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                LogException(ex);
                MessageBox.Show($"应用程序发生严重错误：{ex.Message}\n\n详细信息已记录到日志文件。", "严重错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        /// <summary>
        /// 记录异常到日志文件
        /// </summary>
        private void LogException(Exception ex)
        {
            try
            {
                string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "error.log");
                string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}\n\n";
                File.AppendAllText(logPath, logEntry);
            }
            catch
            {
                // 忽略日志记录错误
            }
        }
        
        /// <summary>
        /// 应用程序退出时释放资源
        /// </summary>
        protected override void OnExit(ExitEventArgs e)
        {
            // 释放互斥体
            _mutex?.ReleaseMutex();
            _mutex?.Dispose();
            
            base.OnExit(e);
        }
    }

}
