import express from 'express';
import { body } from 'express-validator';
import * as clipboardController from '../controllers/clipboardController.js';
import { authenticate } from '../middleware/auth.js';
import { validate } from '../middleware/validator.js';
import { syncLimiter } from '../middleware/rateLimiter.js';

const router = express.Router();

// 创建剪贴板项
router.post('/items',
  authenticate,
  [
    body('content').notEmpty().withMessage('内容不能为空'),
    body('deviceId').notEmpty().withMessage('设备ID不能为空')
  ],
  validate,
  clipboardController.createItem
);

// 批量创建剪贴板项
router.post('/items/batch',
  authenticate,
  [
    body('items').isArray().withMessage('items必须是数组')
  ],
  validate,
  clipboardController.createBatch
);

// 获取剪贴板项列表
router.get('/items',
  authenticate,
  clipboardController.getItems
);

// 获取单个剪贴板项
router.get('/items/:id',
  authenticate,
  clipboardController.getItem
);

// 更新剪贴板项
router.put('/items/:id',
  authenticate,
  [
    body('content').notEmpty().withMessage('内容不能为空')
  ],
  validate,
  clipboardController.updateItem
);

// 删除剪贴板项
router.delete('/items/:id',
  authenticate,
  clipboardController.deleteItem
);

// 批量删除剪贴板项
router.delete('/items/batch',
  authenticate,
  [
    body('ids').isArray().withMessage('ids必须是数组')
  ],
  validate,
  clipboardController.deleteBatch
);

// 同步剪贴板数据
router.post('/sync',
  authenticate,
  syncLimiter,
  [
    body('deviceId').notEmpty().withMessage('设备ID不能为空')
  ],
  validate,
  clipboardController.syncItems
);

export default router;
