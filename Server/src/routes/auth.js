import express from 'express';
import { body } from 'express-validator';
import * as authController from '../controllers/authController.js';
import { authenticate } from '../middleware/auth.js';
import { validate } from '../middleware/validator.js';
import { authLimiter, refreshLimiter } from '../middleware/rateLimiter.js';

const router = express.Router();

// 注册
router.post('/register',
  authLimiter,
  [
    body('username').notEmpty().withMessage('用户名不能为空'),
    body('email').notEmpty().withMessage('邮箱不能为空').isEmail().withMessage('邮箱格式不正确'),
    body('password').notEmpty().withMessage('密码不能为空')
  ],
  validate,
  authController.register
);

// 登录
router.post('/login',
  authLimiter,
  [
    body('username').notEmpty().withMessage('用户名不能为空'),
    body('password').notEmpty().withMessage('密码不能为空')
  ],
  validate,
  authController.login
);

// 刷新令牌
router.post('/refresh',
  refreshLimiter,
  [
    body('refreshToken').notEmpty().withMessage('刷新令牌不能为空')
  ],
  validate,
  authController.refresh
);

// 登出
router.post('/logout',
  authenticate,
  authController.logout
);

// 验证令牌
router.get('/validate',
  authenticate,
  authController.validate
);

export default router;
