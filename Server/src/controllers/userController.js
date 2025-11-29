import { User } from '../models/User.js';
import { Device } from '../models/Device.js';
import { hashPassword, comparePassword, validatePassword, validateEmail } from '../utils/password.js';
import { successResponse } from '../utils/response.js';
import { ApiError, errorCodes } from '../utils/errors.js';

export const getCurrentUser = async (req, res, next) => {
  try {
    const user = req.user;
    const devices = await Device.findByUser(user.id);

    const userData = User.toPublic(user);
    userData.devices = devices.map(d => Device.toPublic(d));

    successResponse(res, userData, '获取用户信息成功');
  } catch (error) {
    next(error);
  }
};

export const updateUser = async (req, res, next) => {
  try {
    const { email, currentPassword, newPassword } = req.body;
    const userId = req.userId;

    // 如果要修改邮箱或密码，必须提供当前密码
    if ((email || newPassword) && !currentPassword) {
      throw new ApiError(400, errorCodes.VALIDATION_ERROR, '修改邮箱或密码时必须提供当前密码');
    }

    // 验证当前密码
    if (currentPassword) {
      const user = await User.findById(userId);
      const isValid = await comparePassword(currentPassword, user.password_hash);
      if (!isValid) {
        throw new ApiError(400, errorCodes.INVALID_CREDENTIALS, '当前密码错误');
      }
    }

    // 更新邮箱
    if (email) {
      if (!validateEmail(email)) {
        throw new ApiError(400, errorCodes.VALIDATION_ERROR, '邮箱格式不正确');
      }

      const existingEmail = await User.findByEmail(email);
      if (existingEmail && existingEmail.id !== userId) {
        throw new ApiError(409, errorCodes.EMAIL_ALREADY_EXISTS, '邮箱已被使用');
      }

      await User.updateEmail(userId, email);
    }

    // 更新密码
    if (newPassword) {
      if (!validatePassword(newPassword)) {
        throw new ApiError(400, errorCodes.VALIDATION_ERROR, '新密码格式不正确', [
          { field: 'newPassword', message: '密码必须是8-32个字符，至少包含大小写字母和数字' }
        ]);
      }

      const passwordHash = await hashPassword(newPassword);
      await User.updatePassword(userId, passwordHash);
    }

    const updatedUser = await User.findById(userId);
    successResponse(res, User.toPublic(updatedUser), '用户信息更新成功');
  } catch (error) {
    next(error);
  }
};

export const changePassword = async (req, res, next) => {
  try {
    const { currentPassword, newPassword } = req.body;
    const userId = req.userId;

    // 验证当前密码
    const user = await User.findById(userId);
    const isValid = await comparePassword(currentPassword, user.password_hash);
    if (!isValid) {
      throw new ApiError(400, errorCodes.INVALID_CREDENTIALS, '当前密码错误');
    }

    // 验证新密码
    if (!validatePassword(newPassword)) {
      throw new ApiError(400, errorCodes.VALIDATION_ERROR, '新密码格式不正确', [
        { field: 'newPassword', message: '密码必须是8-32个字符，至少包含大小写字母和数字' }
      ]);
    }

    // 更新密码
    const passwordHash = await hashPassword(newPassword);
    await User.updatePassword(userId, passwordHash);

    successResponse(res, null, '密码修改成功，请重新登录');
  } catch (error) {
    next(error);
  }
};
