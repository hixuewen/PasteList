import express from 'express';
import * as deviceController from '../controllers/deviceController.js';
import { authenticate } from '../middleware/auth.js';

const router = express.Router();

// 获取设备列表
router.get('/',
  authenticate,
  deviceController.getDevices
);

// 删除设备
router.delete('/:deviceId',
  authenticate,
  deviceController.deleteDevice
);

export default router;
