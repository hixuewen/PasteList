using System;
using System.Windows.Input;

namespace PasteList
{
    /// <summary>
    /// RelayCommand - 简单的命令实现，支持同步和异步执行
    /// </summary>
    public class RelayCommand : ICommand
    {
        private readonly Func<Task>? _executeAsync;
        private readonly Action? _execute;
        private readonly Func<bool> _canExecute;
        private readonly bool _isAsync;

        /// <summary>
        /// 构造函数（同步版本）
        /// </summary>
        /// <param name="execute">执行方法</param>
        /// <param name="canExecute">可执行判断方法</param>
        public RelayCommand(Action execute, Func<bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute ?? (() => true);
            _isAsync = false;
        }

        /// <summary>
        /// 构造函数（异步版本）
        /// </summary>
        /// <param name="executeAsync">异步执行方法</param>
        /// <param name="canExecute">可执行判断方法</param>
        public RelayCommand(Func<Task> executeAsync, Func<bool>? canExecute = null)
        {
            _executeAsync = executeAsync ?? throw new ArgumentNullException(nameof(executeAsync));
            _canExecute = canExecute ?? (() => true);
            _isAsync = true;
        }

        /// <summary>
        /// 可执行状态变化事件
        /// </summary>
        public event EventHandler? CanExecuteChanged;

        /// <summary>
        /// 判断命令是否可执行
        /// </summary>
        /// <param name="parameter">参数</param>
        /// <returns>是否可执行</returns>
        public bool CanExecute(object? parameter)
        {
            return _canExecute();
        }

        /// <summary>
        /// 执行命令
        /// </summary>
        /// <param name="parameter">参数</param>
        public void Execute(object? parameter)
        {
            if (_isAsync)
            {
                _ = _executeAsync?.Invoke();
            }
            else
            {
                _execute?.Invoke();
            }
        }

        /// <summary>
        /// 异步执行命令
        /// </summary>
        /// <param name="parameter">参数</param>
        /// <returns>异步任务</returns>
        public async Task ExecuteAsync(object? parameter)
        {
            if (_isAsync && _executeAsync != null)
            {
                await _executeAsync.Invoke();
            }
            else if (!_isAsync)
            {
                _execute?.Invoke();
                await Task.CompletedTask;
            }
            else
            {
                await Task.CompletedTask;
            }
        }

        /// <summary>
        /// 触发可执行状态变化事件
        /// </summary>
        public void RaiseCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
