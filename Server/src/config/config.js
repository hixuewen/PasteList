import dotenv from 'dotenv';

dotenv.config();

export const config = {
  port: process.env.PORT || 3000,
  nodeEnv: process.env.NODE_ENV || 'development',
  
  db: {
    host: process.env.DB_HOST || 'localhost',
    port: parseInt(process.env.DB_PORT) || 3306,
    database: process.env.DB_NAME || 'pastelist',
    user: process.env.DB_USER || 'root',
    password: process.env.DB_PASSWORD || '',
    waitForConnections: true,
    connectionLimit: 10,
    queueLimit: 0
  },
  
  jwt: {
    secret: process.env.JWT_SECRET || 'your_jwt_secret',
    refreshSecret: process.env.JWT_REFRESH_SECRET || 'your_jwt_refresh_secret',
    accessExpire: parseInt(process.env.JWT_ACCESS_EXPIRE) || 3600,
    accessExpireRemember: parseInt(process.env.JWT_ACCESS_EXPIRE_REMEMBER) || 86400,
    refreshExpire: parseInt(process.env.JWT_REFRESH_EXPIRE) || 604800,
    refreshExpireRemember: parseInt(process.env.JWT_REFRESH_EXPIRE_REMEMBER) || 2592000
  },
  
  cors: {
    origin: process.env.CORS_ORIGIN || '*'
  },
  
  rateLimit: {
    windowMs: parseInt(process.env.RATE_LIMIT_WINDOW_MS) || 3600000,
    maxRequests: parseInt(process.env.RATE_LIMIT_MAX_REQUESTS) || 1000
  }
};
