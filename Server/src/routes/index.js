import express from 'express';
import authRoutes from './auth.js';
import userRoutes from './users.js';
import clipboardRoutes from './clipboard.js';
import deviceRoutes from './devices.js';

const router = express.Router();

router.use('/auth', authRoutes);
router.use('/users', userRoutes);
router.use('/clipboard', clipboardRoutes);
router.use('/devices', deviceRoutes);

export default router;
