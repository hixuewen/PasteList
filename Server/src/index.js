import express from 'express';
import cors from 'cors';
import helmet from 'helmet';
import morgan from 'morgan';
import { config } from './config/config.js';
import routes from './routes/index.js';
import { errorHandler, notFound } from './middleware/errorHandler.js';
import { generalLimiter } from './middleware/rateLimiter.js';
import { getPool } from './config/database.js';

const app = express();

// 安全中间件
app.use(helmet());

// CORS配置
app.use(cors({
  origin: config.cors.origin,
  credentials: true
}));

// 日志中间件
if (config.nodeEnv === 'development') {
  app.use(morgan('dev'));
} else {
  app.use(morgan('combined'));
}

// 解析JSON
app.use(express.json());
app.use(express.urlencoded({ extended: true }));

// 通用频率限制
app.use('/api/v1', generalLimiter);

// 健康检查
app.get('/health', (req, res) => {
  res.json({ status: 'ok', timestamp: new Date().toISOString() });
});

// API路由
app.use('/api/v1', routes);

// 404处理
app.use(notFound);

// 错误处理
app.use(errorHandler);

// 启动服务器
const startServer = async () => {
  try {
    // 测试数据库连接
    const pool = getPool();
    await pool.query('SELECT 1');
    console.log('✓ Database connected successfully');

    app.listen(config.port, () => {
      console.log(`✓ Server is running on port ${config.port}`);
      console.log(`✓ Environment: ${config.nodeEnv}`);
      console.log(`✓ API Base URL: http://localhost:${config.port}/api/v1`);
    });
  } catch (error) {
    console.error('✗ Failed to start server:', error);
    process.exit(1);
  }
};

// 优雅关闭
process.on('SIGTERM', async () => {
  console.log('SIGTERM received, closing server gracefully...');
  const pool = getPool();
  await pool.end();
  process.exit(0);
});

process.on('SIGINT', async () => {
  console.log('SIGINT received, closing server gracefully...');
  const pool = getPool();
  await pool.end();
  process.exit(0);
});

startServer();

export default app;
