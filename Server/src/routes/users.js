import express from 'express';
import { body } from 'express-validator';
import * as userController from '../controllers/userController.js';
import { authenticate } from '../middleware/auth.js';
import { validate } from '../middleware/validator.js';

const router = express.Router();

// 获取当前用户信息
router.get('/me',
  authenticate,
  userController.getCurrentUser
);

// 更新用户信息
router.put('/me',
  authenticate,
  userController.updateUser
);

// 修改密码
router.post('/me/password',
  authenticate,
  [
    body('currentPassword').notEmpty().withMessage('当前密码不能为空'),
    body('newPassword').notEmpty().withMessage('新密码不能为空')
  ],
  validate,
  userController.changePassword
);

export default router;
