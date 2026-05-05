# 首次启动剪贴板捕获失败修复 实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 修复首次启动并开启捕获后，剪贴板无法正常捕获内容，必须双击历史列表项粘贴一次后才能正常工作的 bug。

**Architecture:** 问题根因有两点：(1) `OnClipboardChanged()` 中 `Thread.Sleep(50)` 阻塞 UI 线程，之后调用 WPF `Clipboard` API 获取内容时，外部进程可能仍持有剪贴板锁，导致 `GetCurrentClipboardContent()` 静默失败返回 null；(2) `GetCurrentClipboardContent()` 内部 `ContainsData(DataFormats.Text)` 仅检查 `CF_UNICODETEXT`，但 WPF 的 `DataFormats.Text` 映射需经过 `DataFormats.GetDataFormat` 转换，若首次调用时格式未正确预注册，可能漏检。修复方案：用带指数退避的轮询重试替代 `Thread.Sleep`，增强 `GetCurrentClipboardContent()` 的健壮性，并在应用启动时自动开启监听。

**Tech Stack:** .NET 8.0, WPF, P/Invoke (user32.dll Win32 Clipboard API), MVVM

---

## 文件结构

| 文件 | 操作 | 职责 |
|------|------|------|
| `Services/ClipboardService.cs` | 修改 | 核心修复：重试机制、增强剪贴板内容读取 |
| `ViewModels/MainWindowViewModel.cs` | 修改 | 自动启动监听 |
| `Services/ClipboardService.cs` | 仅参考 | 理解现有逻辑 |
| `MainWindow.xaml.cs` | 仅参考 | 理解初始化流程 |

---

### Task 1: 增强 `GetCurrentClipboardContent()` 的格式检测和异常处理

**文件:**
- 修改: `Services/ClipboardService.cs:238-290`

**说明:** 当前 `GetCurrentClipboardContent()` 使用 `ContainsData(DataFormats.Text)` 做早期检查，该 API 只匹配 `CF_UNICODETEXT`。部分应用仅设置 `CF_TEXT`(ANSI) 或 `CF_OEMTEXT`。需同时检查 `DataFormats.UnicodeText` 和 `DataFormats.Text`。同时增强错误日志，使静默失败可追踪。

- [ ] **Step 1: 修改早期返回检查，增加 UnicodeText 格式检测**

将 `Services/ClipboardService.cs` 第 242-247 行的 early return 检查修改为：

```csharp
// 检查多种文本格式（兼容仅设置 ANSI 文本 CF_TEXT 的应用）
bool hasText = Clipboard.ContainsData(DataFormats.Text) ||
               Clipboard.ContainsData(DataFormats.UnicodeText);

if (!hasText &&
    !Clipboard.ContainsData(DataFormats.Bitmap) &&
    !Clipboard.ContainsData(DataFormats.FileDrop))
{
    return null;
}
```

- [ ] **Step 2: 增强异常日志，记录剪贴板访问失败的具体原因**

修改 `GetCurrentClipboardContent()` 的 catch 块（约第 285-289 行），从 `Debug.WriteLine` 改为使用 `_logger` 记录：

```csharp
catch (Exception ex)
{
    _logger?.LogWarning($"获取剪贴板内容时发生错误: {ex.GetType().Name}: {ex.Message}");
    return null;
}
```

- [ ] **Step 3: 构建验证编译**

```bash
dotnet build
```
预期：编译通过，无错误。

- [ ] **Step 4: Commit**

```bash
git add Services/ClipboardService.cs
git commit -m "fix: 增强剪贴板格式检测，支持 ANSI 文本格式"
```

---

### Task 2: 替换 `Thread.Sleep(50)` 为带重试的健壮剪贴板读取

**文件:**
- 修改: `Services/ClipboardService.cs:370-396`

**说明:** 当前 `OnClipboardChanged()` 在 WndProc 钩子中用 `Thread.Sleep(50)` 阻塞 UI 线程，然后调用 `GetCurrentClipboardContent()`。一旦剪贴板仍被锁定（外部进程未释放），内容读取静默失败。用轮询重试机制替代，尝试多次读取剪贴板，指数退避延迟。

- [ ] **Step 1: 添加带重试的剪贴板内容读取方法**

在 `ClipboardService` 类中（`OnClipboardChanged()` 方法之前）添加私有方法 `TryGetClipboardContentWithRetry`：

```csharp
/// <summary>
/// 带重试机制的剪贴板内容读取
/// 外部应用更改剪贴板后，剪贴板可能仍被短暂锁定，需要重试
/// </summary>
private ClipboardItem? TryGetClipboardContentWithRetry(int maxRetries = 5)
{
    for (int i = 0; i < maxRetries; i++)
    {
        if (i > 0)
        {
            // 指数退避：50ms, 100ms, 200ms, 400ms
            int delayMs = 50 * (1 << (i - 1));
            System.Threading.Thread.Sleep(delayMs);
        }

        var item = GetCurrentClipboardContent();
        if (item != null)
        {
            if (i > 0)
            {
                _logger?.LogDebug($"剪贴板内容在第 {i + 1} 次尝试时成功读取");
            }
            return item;
        }
    }

    _logger?.LogWarning($"尝试 {maxRetries} 次后仍无法读取剪贴板内容");
    return null;
}
```

- [ ] **Step 2: 修改 `OnClipboardChanged()` 使用新的重试方法**

将 `Services/ClipboardService.cs` 第 373-396 行的 `OnClipboardChanged()` 方法修改为：

```csharp
/// <summary>
/// 处理剪贴板变化事件（带重试机制）
/// </summary>
private void OnClipboardChanged()
{
    try
    {
        var clipboardItem = TryGetClipboardContentWithRetry();
        if (clipboardItem != null)
        {
            if (_lastClipboardContent != clipboardItem.Content)
            {
                _lastClipboardContent = clipboardItem.Content;
                var preview = clipboardItem.Content?.Length > 50
                    ? clipboardItem.Content.Substring(0, 50) + "..."
                    : clipboardItem.Content;
                _logger?.LogClipboardOperation("检测到变化", "Text",
                    clipboardItem.Content?.Length ?? 0, preview);
                ClipboardChanged?.Invoke(this, new ClipboardChangedEventArgs(clipboardItem));
            }
        }
    }
    catch (Exception ex)
    {
        _logger?.LogError(ex, "处理剪贴板变化时发生错误");
    }
}
```

- [ ] **Step 3: 构建验证编译**

```bash
dotnet build
```
预期：编译通过，无错误。

- [ ] **Step 4: Commit**

```bash
git add Services/ClipboardService.cs
git commit -m "fix: 用重试机制替换固定延迟，提升剪贴板内容读取可靠性"
```

---

### Task 3: 应用启动时自动开启剪贴板监听

**文件:**
- 修改: `MainWindow.xaml.cs:135-143`
- 参考: `ViewModels/MainWindowViewModel.cs:952-966`（仅引用其 `StartListeningAsync()`）

**说明:** 目前用户必须手动点击"开始监听"按钮才能启用捕获。应在应用启动完成后自动调用 `StartListeningAsync()`，避免用户忘记手动开启。

- [ ] **Step 1: 在 MainWindow 初始化完成后自动启动监听**

修改 `MainWindow.xaml.cs` 的 `MainWindow_Loaded` 方法（约第 131-150 行），在加载历史记录后自动启动监听：

```csharp
private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
{
    try
    {
        await InitializeAsync();

        if (_viewModel != null)
        {
            await _viewModel.LoadHistoryAsync();
            _viewModel.StatusMessage = "应用程序已就绪，按 Alt+Z 可唤起窗口";

            // 自动启动剪贴板监听
            await _viewModel.StartListeningAsync();
            _logger?.LogInfo("剪贴板监听已自动启动");
        }
    }
    catch (Exception ex)
    {
        MessageBox.Show($"应用程序初始化失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        Close();
    }
}
```

- [ ] **Step 2: 构建验证编译**

```bash
dotnet build
```
预期：编译通过，无错误。

- [ ] **Step 3: Commit**

```bash
git add MainWindow.xaml.cs
git commit -m "feat: 应用启动后自动开启剪贴板监听"
```

---

### Task 4: 添加 WPF Clipboard API 预热（防 COM 初始化延迟）

**文件:**
- 修改: `Services/ClipboardService.cs:163-176`

**说明:** WPF `Clipboard` 类底层依赖 COM 组件。在 `StartListening()` 首次读取剪贴板作为初始状态时（`GetCurrentClipboardContent()`），如果 COM 组件尚未完全初始化（WPF 延迟初始化），读取静默失败 → `_lastClipboardContent` 仍为 null → 外部变化能正确触发，但日志中初始状态缺失。虽非致命，但影响可观测性。在监听器启动后增加一次显式的剪贴板预热操作，确保 COM 组件就绪。

- [ ] **Step 1: 优化初始剪贴板状态捕获，增加重试**

修改 `Services/ClipboardService.cs` 第 163-176 行的初始剪贴板捕获逻辑：

```csharp
// 获取当前剪贴板内容作为初始状态（带重试，确保 COM 组件已就绪）
try
{
    ClipboardItem? initialContent = null;
    for (int retry = 0; retry < 3; retry++)
    {
        if (retry > 0)
        {
            System.Threading.Thread.Sleep(100);
            _logger?.LogDebug($"初始剪贴板读取重试 {retry}/3");
        }
        initialContent = GetCurrentClipboardContent();
        if (initialContent != null) break;
    }

    if (initialContent != null)
    {
        _lastClipboardContent = initialContent.Content;
        _logger?.LogDebug($"已获取初始剪贴板内容，长度: {initialContent.Content.Length}");
    }
    else
    {
        _logger?.LogDebug("初始剪贴板为空或无法读取，监听器将在首次变化时捕获");
    }
}
catch (Exception ex)
{
    _logger?.LogWarning($"获取初始剪贴板内容失败: {ex.Message}");
}
```

- [ ] **Step 2: 构建验证编译**

```bash
dotnet build
```
预期：编译通过，无错误。

- [ ] **Step 3: Commit**

```bash
git add Services/ClipboardService.cs
git commit -m "fix: 初始剪贴板状态捕获增加重试，预热 WPF Clipboard COM 组件"
```

---

### Task 5: 端到端验证与日志检查

**文件:**
- 无需修改

**说明:** 构建运行后，模拟首次使用场景验证修复。

- [ ] **Step 1: 构建 Release 版本**

```bash
dotnet build -c Release
```
预期：编译通过。

- [ ] **Step 2: 手动验证清单**

执行以下步骤并确认行为正确：
1. 启动应用程序 → 状态栏应显示"正在监听剪贴板..."
2. 在其他应用（如记事本）中选中文字并 Ctrl+C → 剪贴板内容应出现在历史列表中
3. 重复复制不同内容 → 每次均应正常捕获
4. 检查日志文件 `Logs/PasteList-{Date}.log` → 确认无剪贴板读取失败的 Warning/Error
5. 双击历史记录项 → 应正常粘贴到前一个活动窗口
6. 再次从其他应用复制内容 → 仍应正常捕获（验证不是"粘贴一次后修复"）

- [ ] **Step 3: Commit（如有文档更新）**

```bash
git add -A
git commit -m "docs: 完成首次捕获修复的验证"
```
