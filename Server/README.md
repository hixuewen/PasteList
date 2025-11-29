# PasteList Server

基于 Node.js 和 Express 的 PasteList HTTP API 服务器实现。

## 功能特性

- 用户认证（注册、登录、登出、Token刷新）
- JWT Token 管理
- 剪贴板数据同步
- 设备管理
- 请求频率限制
- CORS 支持
- 安全性中间件

## 安装

```bash
npm install
```

## 配置

复制 `.env.example` 到 `.env` 并配置相应的环境变量：

```bash
cp .env.example .env
```

## 数据库初始化

运行以下 SQL 创建数据库表：

```sql
CREATE DATABASE IF NOT EXISTS pastelist CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;

USE pastelist;

CREATE TABLE users (
  id INT PRIMARY KEY AUTO_INCREMENT,
  username VARCHAR(50) UNIQUE NOT NULL,
  email VARCHAR(100) UNIQUE NOT NULL,
  password_hash VARCHAR(255) NOT NULL,
  created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
  updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  last_login_at TIMESTAMP NULL
);

CREATE TABLE devices (
  id INT PRIMARY KEY AUTO_INCREMENT,
  user_id INT NOT NULL,
  device_id VARCHAR(100) NOT NULL,
  device_name VARCHAR(100),
  device_type VARCHAR(20),
  platform VARCHAR(50),
  last_active_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
  created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
  FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE,
  UNIQUE KEY unique_user_device (user_id, device_id)
);

CREATE TABLE clipboard_items (
  id INT PRIMARY KEY AUTO_INCREMENT,
  user_id INT NOT NULL,
  device_id VARCHAR(100) NOT NULL,
  content TEXT NOT NULL,
  created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
  updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE,
  INDEX idx_user_created (user_id, created_at),
  INDEX idx_device (device_id)
);

CREATE TABLE refresh_tokens (
  id INT PRIMARY KEY AUTO_INCREMENT,
  user_id INT NOT NULL,
  token VARCHAR(500) UNIQUE NOT NULL,
  expires_at TIMESTAMP NOT NULL,
  created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
  FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE,
  INDEX idx_token (token),
  INDEX idx_expires (expires_at)
);
```

## 运行

开发模式（带自动重启）：
```bash
npm run dev
```

生产模式：
```bash
npm start
```

## API 文档

详细的 API 文档请参考：`../Doc/API_SPECIFICATION.md`

## 项目结构

```
Server/
├── src/
│   ├── config/          # 配置文件
│   ├── controllers/     # 控制器（路由处理）
│   ├── middleware/      # 中间件
│   ├── models/          # 数据模型
│   ├── routes/          # 路由定义
│   ├── utils/           # 工具函数
│   └── index.js         # 入口文件
├── package.json
├── .env.example
└── README.md
```

## 安全性

- 所有密码使用 bcrypt 哈希存储
- JWT Token 用于身份认证
- 请求频率限制防止滥用
- CORS 配置保护跨域访问
- Helmet 增强安全性
- 生产环境必须使用 HTTPS
