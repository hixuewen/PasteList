# PasteList 服务器同步 API 接口规范

## 概述

本文档描述了PasteList客户端与服务器之间的同步API接口规范。服务器需要实现以下接口以支持与PasteList客户端的服务器同步功能。

**基础URL**: `{ServerUrl}/api/sync`

## API 基础规范

### 通信协议
- **协议**: HTTP/HTTPS
- **数据格式**: JSON
- **字符编码**: UTF-8
- **认证方式**: 无需认证（简化实现）

### 请求头
所有请求都需要包含以下头部信息：
```
Content-Type: application/json
X-Device-ID: {设备唯一标识符}
X-Client-Version: 1.0.0
```

### 响应格式
所有API响应都使用标准HTTP状态码：
- `200 OK`: 成功
- `400 Bad Request`: 请求参数错误
- `500 Internal Server Error`: 服务器内部错误

响应体格式：
```json
{
  "success": true,
  "data": { ... },  // 实际数据（成功时）
  "error": null     // 错误信息（失败时）
}
```

## API 接口列表

### 1. 获取服务器状态
**接口**: `GET /api/sync/status`

**描述**: 验证服务器是否可用，获取服务器基本信息

**请求参数**: 无

**响应示例**:
```json
{
  "success": true,
  "data": {
    "serverName": "PasteList Server",
    "version": "1.0.0",
    "serverTime": "2025-11-20T10:30:00Z",
    "isHealthy": true,
    "errorMessage": null,
    "supportedFeatures": ["push", "pull", "merge"]
  }
}
```

---

### 2. 推送剪贴板数据到服务器
**接口**: `POST /api/sync/push`

**描述**: 客户端推送本地剪贴板数据到服务器

**请求参数**:
```json
{
  "deviceId": "设备唯一标识符",
  "items": [
    {
      "id": 1,
      "content": "剪贴板内容文本或Base64编码",
      "timestamp": "2025-11-20T10:30:00Z"
    }
  ],
  "timestamp": "2025-11-20T10:30:00Z"
}
```

**响应示例**:
```json
{
  "success": true,
  "data": {
    "success": true,
    "pushedCount": 10,
    "skippedCount": 2,
    "serverTimestamp": "2025-11-20T10:30:00Z",
    "errorMessage": null
  }
}
```

**说明**:
- `pushedCount`: 实际插入的记录数
- `skippedCount`: 跳过的重复记录数
- 服务器应自动检测重复记录并跳过

---

### 3. 从服务器拉取剪贴板数据
**接口**: `GET /api/sync/pull`

**描述**: 客户端从服务器拉取剪贴板数据

**请求参数**:
```
?deviceId={设备ID}&since={时间戳}
```

- `deviceId`: 必需，设备唯一标识符
- `since`: 可选，仅返回此时间戳之后的记录（ISO 8601格式）

**响应示例**:
```json
{
  "success": true,
  "data": {
    "items": [
      {
        "id": 100,
        "content": "剪贴板内容",
        "timestamp": "2025-11-20T10:30:00Z"
      }
    ],
    "serverTimestamp": "2025-11-20T10:30:00Z"
  }
}
```

---

### 4. 双向同步（合并数据）
**接口**: `POST /api/sync/merge`

**描述**: 客户端与服务器进行双向同步，合并数据并返回服务器端数据

**请求参数**:
```json
{
  "deviceId": "设备唯一标识符",
  "localItems": [
    {
      "id": 1,
      "content": "本地剪贴板内容",
      "timestamp": "2025-11-20T10:30:00Z"
    }
  ],
  "lastSyncTime": "2025-11-20T09:30:00Z",
  "timestamp": "2025-11-20T10:30:00Z"
}
```

**响应示例**:
```json
{
  "success": true,
  "data": {
    "success": true,
    "serverItems": [
      {
        "id": 100,
        "content": "服务器端剪贴板内容",
        "timestamp": "2025-11-20T10:35:00Z"
      }
    ],
    "conflictsResolved": 1,
    "pushedCount": 5,
    "pulledCount": 3,
    "serverTimestamp": "2025-11-20T10:35:00Z",
    "errorMessage": null
  }
}
```

**冲突解决策略**:
服务器应实现以下冲突解决机制：
1. **保留较新的版本** (KeepNewer): 比较时间戳，保留较新的记录
2. **保留两个版本** (KeepBoth): 为重复内容创建两个记录
3. **保留本地版本** (KeepLocal): 以客户端数据为准
4. **保留远程版本** (KeepRemote): 以服务器数据为准

默认策略：`KeepNewer`

---

## 数据库设计建议

### 剪贴板表结构 (clipboard_items)
```sql
CREATE TABLE clipboard_items (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    content TEXT NOT NULL,
    timestamp DATETIME NOT NULL,
    device_id TEXT,
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_timestamp ON clipboard_items(timestamp);
CREATE INDEX idx_device_timestamp ON clipboard_items(device_id, timestamp);
```

### 同步记录表 (可选，用于审计)
```sql
CREATE TABLE sync_records (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    device_id TEXT NOT NULL,
    operation_type TEXT NOT NULL, -- 'push', 'pull', 'merge'
    record_count INTEGER NOT NULL,
    timestamp DATETIME DEFAULT CURRENT_TIMESTAMP,
    success BOOLEAN NOT NULL,
    error_message TEXT
);

CREATE INDEX idx_device_id ON sync_records(device_id);
```

---

## 错误处理

### 常见错误码
- `400`: 请求参数错误（如缺少deviceId）
- `500`: 服务器内部错误

### 错误响应示例
```json
{
  "success": false,
  "data": null,
  "error": {
    "code": "INVALID_DEVICE_ID",
    "message": "设备ID不能为空",
    "timestamp": "2025-11-20T10:30:00Z"
  }
}
```

---

## 最佳实践

### 1. 数据安全
- 建议使用HTTPS传输（生产环境）
- 可选：对敏感数据进行加密存储
- 定期清理过期数据（建议保留30天）

### 2. 性能优化
- 实现分页查询（大量数据时）
- 使用数据库索引优化查询性能
- 考虑使用缓存减少数据库查询

### 3. 容错处理
- 实现重试机制应对网络异常
- 记录详细的错误日志
- 优雅降级：部分功能失败不影响整体运行

### 4. 监控与运维
- 监控API响应时间和错误率
- 设置合理的超时时间（建议30秒）
- 实现健康检查接口

---

## 实现示例

### 服务器端技术栈建议
- **.NET**: ASP.NET Core Web API
- **数据库**: PostgreSQL / MySQL / SQLite
- **ORM**: Entity Framework Core
- **日志**: Serilog

### 关键实现点
1. **去重逻辑**: 基于`content`字段去重
2. **时间戳处理**: 使用UTC时间，避免时区问题
3. **事务控制**: 推送操作使用数据库事务保证一致性
4. **并发处理**: 支持多设备并发同步

---

## 测试建议

### 功能测试
1. 测试推送、拉取、合并的基本流程
2. 测试冲突解决策略
3. 测试重复数据处理
4. 测试网络异常恢复

### 性能测试
1. 测试大量数据的同步性能
2. 测试并发同步时的稳定性
3. 测试长期运行的内存使用

### 安全测试
1. 测试恶意数据注入防护
2. 测试数据泄露风险
3. 测试访问控制（如需要）

---

## 版本历史

| 版本 | 日期 | 修改内容 |
|------|------|----------|
| 1.0.0 | 2025-11-20 | 初始版本，支持基本同步功能 |

---

## 联系方式

如有问题或建议，请联系PasteList开发团队。

---

**注意**: 本文档描述的是当前版本的API规范。未来版本可能会添加新功能或修改现有接口，请关注API版本更新。
