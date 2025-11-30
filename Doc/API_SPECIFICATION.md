# PasteList HTTP API 接口规范文档

## 文档信息

- **版本**: v1.0.0
- **创建日期**: 2025-11-29
- **基础URL**: `https://api.pastelist.com/api/v1`
- **认证方式**: JWT Bearer Token
- **数据格式**: JSON

---

## 1. 通用说明

### 1.1 请求头

所有请求应包含以下请求头：

```http
Content-Type: application/json
Accept: application/json
```

需要认证的接口还需包含：

```http
Authorization: Bearer {access_token}
```

### 1.2 响应格式

#### 成功响应

```json
{
  "success": true,
  "data": {
    // 业务数据
  },
  "message": "操作成功",
  "timestamp": "2025-11-29T10:30:00Z"
}
```

#### 错误响应

```json
{
  "success": false,
  "error": {
    "code": "ERROR_CODE",
    "message": "错误描述信息",
    "details": []
  },
  "timestamp": "2025-11-29T10:30:00Z"
}
```

### 1.3 HTTP 状态码

| 状态码 | 说明 |
|--------|------|
| 200 | 请求成功 |
| 201 | 创建成功 |
| 400 | 请求参数错误 |
| 401 | 未认证或 Token 无效 |
| 403 | 无权限访问 |
| 404 | 资源不存在 |
| 409 | 资源冲突（如用户名已存在） |
| 429 | 请求过于频繁 |
| 500 | 服务器内部错误 |

### 1.4 错误码定义

| 错误码 | 说明 |
|--------|------|
| `INVALID_CREDENTIALS` | 用户名或密码错误 |
| `TOKEN_EXPIRED` | Token 已过期 |
| `TOKEN_INVALID` | Token 无效 |
| `USER_NOT_FOUND` | 用户不存在 |
| `USER_ALREADY_EXISTS` | 用户已存在 |
| `EMAIL_ALREADY_EXISTS` | 邮箱已被注册 |
| `VALIDATION_ERROR` | 数据验证失败 |
| `RATE_LIMIT_EXCEEDED` | 请求频率超限 |
| `INTERNAL_ERROR` | 服务器内部错误 |

---

## 2. 用户认证接口

### 2.1 用户注册

**接口**: `POST /auth/register`

**描述**: 注册新用户账号

**是否需要认证**: 否

**请求体**:

```json
{
  "username": "string",      // 用户名，3-20个字符，只允许字母、数字、下划线
  "email": "string",         // 邮箱地址，必须符合邮箱格式
  "password": "string",      // 密码，8-32个字符，至少包含大小写字母和数字
  "deviceId": "string"       // 设备ID，用于标识客户端设备（可选）
}
```

**请求示例**:

```json
{
  "username": "john_doe",
  "email": "john@example.com",
  "password": "SecurePass123",
  "deviceId": "WIN-DESKTOP-001"
}
```

**成功响应** (201):

```json
{
  "success": true,
  "data": {
    "user": {
      "id": 1001,
      "username": "john_doe",
      "email": "john@example.com",
      "createdAt": "2025-11-29T10:30:00Z"
    },
    "token": {
      "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
      "refreshToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
      "expiresIn": 3600,
      "tokenType": "Bearer"
    }
  },
  "message": "注册成功",
  "timestamp": "2025-11-29T10:30:00Z"
}
```

**错误响应** (400/409):

```json
{
  "success": false,
  "error": {
    "code": "USER_ALREADY_EXISTS",
    "message": "用户名已存在",
    "details": [
      {
        "field": "username",
        "message": "用户名 'john_doe' 已被使用"
      }
    ]
  },
  "timestamp": "2025-11-29T10:30:00Z"
}
```

---

### 2.2 用户登录

**接口**: `POST /auth/login`

**描述**: 用户登录，获取访问令牌

**是否需要认证**: 否

**请求体**:

```json
{
  "username": "string",      // 用户名或邮箱
  "password": "string",      // 密码
  "deviceId": "string",      // 设备ID（可选）
  "rememberMe": false        // 是否记住登录状态，影响Token过期时间（可选，默认false）
}
```

**请求示例**:

```json
{
  "username": "john_doe",
  "password": "SecurePass123",
  "deviceId": "WIN-DESKTOP-001",
  "rememberMe": true
}
```

**成功响应** (200):

```json
{
  "success": true,
  "data": {
    "user": {
      "id": 1001,
      "username": "john_doe",
      "email": "john@example.com",
      "createdAt": "2025-11-29T10:30:00Z",
      "lastLoginAt": "2025-11-29T15:45:00Z"
    },
    "token": {
      "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
      "refreshToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
      "expiresIn": 86400,
      "tokenType": "Bearer"
    }
  },
  "message": "登录成功",
  "timestamp": "2025-11-29T15:45:00Z"
}
```

**错误响应** (401):

```json
{
  "success": false,
  "error": {
    "code": "INVALID_CREDENTIALS",
    "message": "用户名或密码错误",
    "details": []
  },
  "timestamp": "2025-11-29T15:45:00Z"
}
```

---

### 2.3 刷新令牌

**接口**: `POST /auth/refresh`

**描述**: 使用刷新令牌获取新的访问令牌

**是否需要认证**: 否（使用 Refresh Token）

**请求体**:

```json
{
  "refreshToken": "string"   // 刷新令牌
}
```

**请求示例**:

```json
{
  "refreshToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
}
```

**成功响应** (200):

```json
{
  "success": true,
  "data": {
    "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
    "refreshToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
    "expiresIn": 3600,
    "tokenType": "Bearer"
  },
  "message": "令牌刷新成功",
  "timestamp": "2025-11-29T16:00:00Z"
}
```

**错误响应** (401):

```json
{
  "success": false,
  "error": {
    "code": "TOKEN_INVALID",
    "message": "刷新令牌无效或已过期",
    "details": []
  },
  "timestamp": "2025-11-29T16:00:00Z"
}
```

---

### 2.4 用户登出

**接口**: `POST /auth/logout`

**描述**: 注销当前登录会话，使令牌失效

**是否需要认证**: 是

**请求体**:

```json
{
  "refreshToken": "string"   // 需要注销的刷新令牌（可选）
}
```

**请求示例**:

```json
{
  "refreshToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
}
```

**成功响应** (200):

```json
{
  "success": true,
  "data": null,
  "message": "登出成功",
  "timestamp": "2025-11-29T17:00:00Z"
}
```

---

### 2.5 验证令牌

**接口**: `GET /auth/validate`

**描述**: 验证当前访问令牌是否有效

**是否需要认证**: 是

**请求参数**: 无

**成功响应** (200):

```json
{
  "success": true,
  "data": {
    "valid": true,
    "userId": 1001,
    "username": "john_doe",
    "expiresAt": "2025-11-29T18:00:00Z"
  },
  "message": "令牌有效",
  "timestamp": "2025-11-29T17:30:00Z"
}
```

**错误响应** (401):

```json
{
  "success": false,
  "error": {
    "code": "TOKEN_EXPIRED",
    "message": "访问令牌已过期",
    "details": []
  },
  "timestamp": "2025-11-29T17:30:00Z"
}
```

---

## 3. 用户信息接口

### 3.1 获取当前用户信息

**接口**: `GET /users/me`

**描述**: 获取当前登录用户的详细信息

**是否需要认证**: 是

**请求参数**: 无

**成功响应** (200):

```json
{
  "success": true,
  "data": {
    "id": 1001,
    "username": "john_doe",
    "email": "john@example.com",
    "createdAt": "2025-11-29T10:30:00Z",
    "updatedAt": "2025-11-29T15:45:00Z",
    "lastLoginAt": "2025-11-29T15:45:00Z",
    "devices": [
      {
        "deviceId": "WIN-DESKTOP-001",
        "deviceName": "Windows Desktop",
        "lastActiveAt": "2025-11-29T17:30:00Z"
      }
    ]
  },
  "message": "获取用户信息成功",
  "timestamp": "2025-11-29T17:30:00Z"
}
```

---

### 3.2 更新用户信息

**接口**: `PUT /users/me`

**描述**: 更新当前用户的个人信息

**是否需要认证**: 是

**请求体**:

```json
{
  "email": "string",         // 新邮箱地址（可选）
  "currentPassword": "string", // 当前密码（修改邮箱或密码时必填）
  "newPassword": "string"    // 新密码（可选）
}
```

**请求示例**:

```json
{
  "email": "newemail@example.com",
  "currentPassword": "SecurePass123"
}
```

**成功响应** (200):

```json
{
  "success": true,
  "data": {
    "id": 1001,
    "username": "john_doe",
    "email": "newemail@example.com",
    "updatedAt": "2025-11-29T18:00:00Z"
  },
  "message": "用户信息更新成功",
  "timestamp": "2025-11-29T18:00:00Z"
}
```

**错误响应** (400):

```json
{
  "success": false,
  "error": {
    "code": "INVALID_CREDENTIALS",
    "message": "当前密码错误",
    "details": []
  },
  "timestamp": "2025-11-29T18:00:00Z"
}
```

---

### 3.3 修改密码

**接口**: `POST /users/me/password`

**描述**: 修改当前用户密码

**是否需要认证**: 是

**请求体**:

```json
{
  "currentPassword": "string",  // 当前密码
  "newPassword": "string"       // 新密码，8-32个字符
}
```

**请求示例**:

```json
{
  "currentPassword": "SecurePass123",
  "newPassword": "NewSecurePass456"
}
```

**成功响应** (200):

```json
{
  "success": true,
  "data": null,
  "message": "密码修改成功，请重新登录",
  "timestamp": "2025-11-29T18:30:00Z"
}
```

---

## 4. 剪贴板数据接口

### 4.1 上传剪贴板项

**接口**: `POST /clipboard/items`

**描述**: 上传本地剪贴板数据到服务器

**是否需要认证**: 是

**请求体**:

```json
{
  "content": "string",       // 剪贴板内容，最大10000字符
  "deviceId": "string",      // 设备ID
  "createdAt": "string"      // 创建时间（ISO 8601格式）
}
```

**请求示例**:

```json
{
  "content": "这是要同步的剪贴板内容",
  "deviceId": "WIN-DESKTOP-001",
  "createdAt": "2025-11-29T18:45:00Z"
}
```

**成功响应** (201):

```json
{
  "success": true,
  "data": {
    "id": 5001,
    "content": "这是要同步的剪贴板内容",
    "userId": 1001,
    "deviceId": "WIN-DESKTOP-001",
    "createdAt": "2025-11-29T18:45:00Z",
    "updatedAt": "2025-11-29T18:45:00Z"
  },
  "message": "剪贴板项创建成功",
  "timestamp": "2025-11-29T18:45:00Z"
}
```

---

### 4.2 批量上传剪贴板项

**接口**: `POST /clipboard/items/batch`

**描述**: 批量上传多个剪贴板项

**是否需要认证**: 是

**请求体**:

```json
{
  "items": [
    {
      "content": "string",
      "deviceId": "string",
      "createdAt": "string"
    }
  ]
}
```

**请求示例**:

```json
{
  "items": [
    {
      "content": "第一条剪贴板内容",
      "deviceId": "WIN-DESKTOP-001",
      "createdAt": "2025-11-29T18:40:00Z"
    },
    {
      "content": "第二条剪贴板内容",
      "deviceId": "WIN-DESKTOP-001",
      "createdAt": "2025-11-29T18:42:00Z"
    }
  ]
}
```

**成功响应** (201):

```json
{
  "success": true,
  "data": {
    "created": 2,
    "failed": 0,
    "items": [
      {
        "id": 5002,
        "content": "第一条剪贴板内容",
        "createdAt": "2025-11-29T18:40:00Z"
      },
      {
        "id": 5003,
        "content": "第二条剪贴板内容",
        "createdAt": "2025-11-29T18:42:00Z"
      }
    ]
  },
  "message": "批量上传成功",
  "timestamp": "2025-11-29T18:45:00Z"
}
```

---

### 4.3 获取剪贴板项列表

**接口**: `GET /clipboard/items`

**描述**: 获取当前用户的剪贴板历史记录

**是否需要认证**: 是

**请求参数**:

| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| page | integer | 否 | 页码，默认1 |
| pageSize | integer | 否 | 每页数量，默认20，最大100 |
| deviceId | string | 否 | 按设备ID筛选 |
| search | string | 否 | 搜索关键词 |
| startDate | string | 否 | 开始日期（ISO 8601） |
| endDate | string | 否 | 结束日期（ISO 8601） |
| sortBy | string | 否 | 排序字段，默认createdAt |
| sortOrder | string | 否 | 排序方向，asc/desc，默认desc |

**请求示例**:

```
GET /clipboard/items?page=1&pageSize=20&deviceId=WIN-DESKTOP-001&sortOrder=desc
```

**成功响应** (200):

```json
{
  "success": true,
  "data": {
    "items": [
      {
        "id": 5003,
        "content": "最新的剪贴板内容",
        "userId": 1001,
        "deviceId": "WIN-DESKTOP-001",
        "createdAt": "2025-11-29T18:42:00Z",
        "updatedAt": "2025-11-29T18:42:00Z"
      },
      {
        "id": 5002,
        "content": "较早的剪贴板内容",
        "userId": 1001,
        "deviceId": "WIN-DESKTOP-001",
        "createdAt": "2025-11-29T18:40:00Z",
        "updatedAt": "2025-11-29T18:40:00Z"
      }
    ],
    "pagination": {
      "currentPage": 1,
      "pageSize": 20,
      "totalItems": 45,
      "totalPages": 3
    }
  },
  "message": "获取列表成功",
  "timestamp": "2025-11-29T19:00:00Z"
}
```

---

### 4.4 获取单个剪贴板项

**接口**: `GET /clipboard/items/{id}`

**描述**: 根据ID获取特定剪贴板项的详细信息

**是否需要认证**: 是

**路径参数**:

| 参数 | 类型 | 说明 |
|------|------|------|
| id | integer | 剪贴板项ID |

**成功响应** (200):

```json
{
  "success": true,
  "data": {
    "id": 5001,
    "content": "这是要同步的剪贴板内容",
    "userId": 1001,
    "deviceId": "WIN-DESKTOP-001",
    "createdAt": "2025-11-29T18:45:00Z",
    "updatedAt": "2025-11-29T18:45:00Z"
  },
  "message": "获取成功",
  "timestamp": "2025-11-29T19:00:00Z"
}
```

**错误响应** (404):

```json
{
  "success": false,
  "error": {
    "code": "RESOURCE_NOT_FOUND",
    "message": "剪贴板项不存在",
    "details": []
  },
  "timestamp": "2025-11-29T19:00:00Z"
}
```

---

### 4.5 更新剪贴板项

**接口**: `PUT /clipboard/items/{id}`

**描述**: 更新指定剪贴板项的内容

**是否需要认证**: 是

**路径参数**:

| 参数 | 类型 | 说明 |
|------|------|------|
| id | integer | 剪贴板项ID |

**请求体**:

```json
{
  "content": "string"        // 更新后的内容
}
```

**成功响应** (200):

```json
{
  "success": true,
  "data": {
    "id": 5001,
    "content": "更新后的剪贴板内容",
    "userId": 1001,
    "deviceId": "WIN-DESKTOP-001",
    "createdAt": "2025-11-29T18:45:00Z",
    "updatedAt": "2025-11-29T19:10:00Z"
  },
  "message": "更新成功",
  "timestamp": "2025-11-29T19:10:00Z"
}
```

---

### 4.6 删除剪贴板项

**接口**: `DELETE /clipboard/items/{id}`

**描述**: 删除指定的剪贴板项

**是否需要认证**: 是

**路径参数**:

| 参数 | 类型 | 说明 |
|------|------|------|
| id | integer | 剪贴板项ID |

**成功响应** (200):

```json
{
  "success": true,
  "data": null,
  "message": "删除成功",
  "timestamp": "2025-11-29T19:15:00Z"
}
```

---

### 4.7 同步剪贴板数据

**接口**: `POST /clipboard/sync`

**描述**: 双向同步剪贴板数据（客户端推送本地更新，服务器返回远程更新）

**是否需要认证**: 是

**请求体**:

```json
{
  "deviceId": "string",
  "lastSyncTime": "string",  // 上次同步时间（ISO 8601）
  "localItems": [            // 本地新增或更新的项
    {
      "localId": "string",   // 本地临时ID
      "content": "string",
      "createdAt": "string"
    }
  ]
}
```

**请求示例**:

```json
{
  "deviceId": "WIN-DESKTOP-001",
  "lastSyncTime": "2025-11-29T18:00:00Z",
  "localItems": [
    {
      "localId": "local-001",
      "content": "新增的本地内容",
      "createdAt": "2025-11-29T19:00:00Z"
    }
  ]
}
```

**成功响应** (200):

```json
{
  "success": true,
  "data": {
    "syncTime": "2025-11-29T19:25:00Z",
    "uploaded": [
      {
        "localId": "local-001",
        "serverId": 5010,
        "success": true
      }
    ],
    "remoteItems": [
      {
        "id": 5011,
        "content": "其他设备新增的内容",
        "deviceId": "MOBILE-ANDROID-001",
        "createdAt": "2025-11-29T19:15:00Z",
        "updatedAt": "2025-11-29T19:15:00Z"
      }
    ]
  },
  "message": "同步成功",
  "timestamp": "2025-11-29T19:25:00Z"
}
```

---

## 5. 设备管理接口

### 5.1 获取设备列表

**接口**: `GET /devices`

**描述**: 获取当前用户的所有设备信息

**是否需要认证**: 是

**成功响应** (200):

```json
{
  "success": true,
  "data": {
    "devices": [
      {
        "deviceId": "WIN-DESKTOP-001",
        "deviceName": "Windows Desktop",
        "deviceType": "desktop",
        "platform": "Windows 11",
        "lastActiveAt": "2025-11-29T19:25:00Z",
        "createdAt": "2025-11-29T10:30:00Z",
        "isCurrentDevice": true
      },
      {
        "deviceId": "MOBILE-ANDROID-001",
        "deviceName": "Pixel 7",
        "deviceType": "mobile",
        "platform": "Android 14",
        "lastActiveAt": "2025-11-29T19:15:00Z",
        "createdAt": "2025-11-28T15:20:00Z",
        "isCurrentDevice": false
      }
    ]
  },
  "message": "获取设备列表成功",
  "timestamp": "2025-11-29T19:30:00Z"
}
```

---

### 5.2 移除设备

**接口**: `DELETE /devices/{deviceId}`

**描述**: 移除指定设备的访问权限

**是否需要认证**: 是

**路径参数**:

| 参数 | 类型 | 说明 |
|------|------|------|
| deviceId | string | 设备ID |

**成功响应** (200):

```json
{
  "success": true,
  "data": null,
  "message": "设备已移除",
  "timestamp": "2025-11-29T19:35:00Z"
}
```

---

## 6. 数据模型定义

### 6.1 User（用户）

```typescript
{
  id: number;              // 用户ID
  username: string;        // 用户名
  email: string;           // 邮箱
  createdAt: string;       // 创建时间（ISO 8601）
  updatedAt: string;       // 更新时间（ISO 8601）
  lastLoginAt: string;     // 最后登录时间（ISO 8601）
}
```

### 6.2 AuthToken（认证令牌）

```typescript
{
  accessToken: string;     // 访问令牌
  refreshToken: string;    // 刷新令牌
  expiresIn: number;       // 过期时间（秒）
  tokenType: string;       // 令牌类型，通常为"Bearer"
}
```

### 6.3 ClipboardItem（剪贴板项）

```typescript
{
  id: number;              // 剪贴板项ID
  content: string;         // 内容
  userId: number;          // 用户ID
  deviceId: string;        // 设备ID
  createdAt: string;       // 创建时间（ISO 8601）
  updatedAt: string;       // 更新时间（ISO 8601）
}
```

### 6.4 Device（设备）

```typescript
{
  deviceId: string;        // 设备唯一标识
  deviceName: string;      // 设备名称
  deviceType: string;      // 设备类型（desktop/mobile/tablet）
  platform: string;        // 操作系统平台
  lastActiveAt: string;    // 最后活跃时间（ISO 8601）
  createdAt: string;       // 创建时间（ISO 8601）
  isCurrentDevice: boolean;// 是否为当前设备
}
```

---

## 7. 安全性规范

### 7.1 HTTPS 要求

- 所有 API 请求必须通过 HTTPS 协议
- 服务器应拒绝非 HTTPS 请求
- 推荐使用 TLS 1.2 或更高版本

### 7.2 Token 安全

- Access Token 有效期：1小时（rememberMe=false）或 24小时（rememberMe=true）
- Refresh Token 有效期：7天（rememberMe=false）或 30天（rememberMe=true）
- Token 应在客户端安全存储（建议使用 Windows Credential Manager）
- Token 泄露后应立即调用登出接口使其失效

### 7.3 密码要求

- 最小长度：8个字符
- 最大长度：32个字符
- 必须包含：大写字母、小写字母、数字
- 推荐包含：特殊字符
- 服务器端使用 BCrypt 或 Argon2 进行哈希存储

### 7.4 请求频率限制

| 接口类型 | 限制 |
|---------|------|
| 登录/注册 | 5次/分钟/IP |
| Token刷新 | 10次/分钟/用户 |
| 数据同步 | 100次/小时/用户 |
| 其他接口 | 1000次/小时/用户 |

### 7.5 数据验证

- 所有输入数据必须进行服务器端验证
- 防止 SQL 注入、XSS 攻击
- 内容长度限制：剪贴板内容最大 10000 字符

---

## 8. 版本控制

API 版本通过 URL 路径进行管理：

```
https://api.pastelist.com/api/v1/...
https://api.pastelist.com/api/v2/...  (未来版本)
```

---

## 9. 附录

### 9.1 完整示例：用户登录流程

```bash
# 1. 用户登录
curl -X POST https://api.pastelist.com/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "username": "john_doe",
    "password": "SecurePass123",
    "deviceId": "WIN-DESKTOP-001",
    "rememberMe": true
  }'

# 响应：获得 accessToken 和 refreshToken

# 2. 使用 Token 获取用户信息
curl -X GET https://api.pastelist.com/api/v1/users/me \
  -H "Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."

# 3. 上传剪贴板数据
curl -X POST https://api.pastelist.com/api/v1/clipboard/items \
  -H "Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..." \
  -H "Content-Type: application/json" \
  -d '{
    "content": "要同步的内容",
    "deviceId": "WIN-DESKTOP-001",
    "createdAt": "2025-11-29T19:00:00Z"
  }'

# 4. Token 过期后刷新
curl -X POST https://api.pastelist.com/api/v1/auth/refresh \
  -H "Content-Type: application/json" \
  -d '{
    "refreshToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
  }'

# 5. 登出
curl -X POST https://api.pastelist.com/api/v1/auth/logout \
  -H "Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..." \
  -H "Content-Type: application/json" \
  -d '{
    "refreshToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
  }'
```

### 9.2 错误处理最佳实践

```csharp
// C# 客户端示例
try
{
    var response = await apiClient.LoginAsync(request);
    // 处理成功响应
}
catch (ApiException ex) when (ex.StatusCode == 401)
{
    // Token 过期，尝试刷新
    await RefreshTokenAsync();
    // 重试请求
}
catch (ApiException ex) when (ex.StatusCode == 429)
{
    // 频率限制，等待后重试
    await Task.Delay(60000);
}
catch (ApiException ex)
{
    // 其他 API 错误
    logger.LogError($"API Error: {ex.Message}");
}
```

---

**文档结束**

如有疑问或需要更新，请联系 API 维护团队。
