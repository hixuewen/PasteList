import rateLimit from 'express-rate-limit';
import { config } from '../config/config.js';
import { ApiError, errorCodes } from '../utils/errors.js';

const createRateLimiter = (windowMs, max) => {
  return rateLimit({
    windowMs,
    max,
    standardHeaders: true,
    legacyHeaders: false,
    handler: (req, res) => {
      const error = new ApiError(429, errorCodes.RATE_LIMIT_EXCEEDED, '请求过于频繁，请稍后再试');
      return res.status(429).json({
        success: false,
        error: {
          code: error.code,
          message: error.message,
          details: []
        },
        timestamp: new Date().toISOString()
      });
    }
  });
};

// 登录/注册限制：5次/分钟/IP
export const authLimiter = createRateLimiter(60 * 1000, 5);

// Token刷新限制：10次/分钟
export const refreshLimiter = createRateLimiter(60 * 1000, 10);

// 数据同步限制：100次/小时
export const syncLimiter = createRateLimiter(60 * 60 * 1000, 100);

// 通用API限制：1000次/小时
export const generalLimiter = createRateLimiter(
  config.rateLimit.windowMs,
  config.rateLimit.maxRequests
);
