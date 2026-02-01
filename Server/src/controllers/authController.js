import { User } from '../models/User.js';
import { RefreshToken } from '../models/RefreshToken.js';
import { Device } from '../models/Device.js';
import { hashPassword, comparePassword, validatePassword, validateUsername, validateEmail } from '../utils/password.js';
import { generateAccessToken, generateRefreshToken, verifyRefreshToken, getAccessTokenExpiration, getRefreshTokenExpiration } from '../utils/jwt.js';
import { successResponse } from '../utils/response.js';
import { ApiError, errorCodes } from '../utils/errors.js';

export const register = async (req, res, next) => {
  try {
    const { username, email, password, deviceId } = req.body;

    // 验证输入
    if (!validateUsername(username)) {
      throw new ApiError(400, errorCodes.VALIDATION_ERROR, '用户名格式不正确', [
        { field: 'username', message: '用户名必须是3-20个字符，只能包含字母、数字和下划线' }
      ]);
    }

    if (!validateEmail(email)) {
      throw new ApiError(400, errorCodes.VALIDATION_ERROR, '邮箱格式不正确', [
        { field: 'email', message: '请输入有效的邮箱地址' }
      ]);
    }

    if (!validatePassword(password)) {
      throw new ApiError(400, errorCodes.VALIDATION_ERROR, '密码格式不正确', [
        { field: 'password', message: '密码必须是8-32个字符，至少包含大小写字母和数字' }
      ]);
    }

    // 检查用户名是否存在
    const existingUser = await User.findByUsername(username);
    if (existingUser) {
      throw new ApiError(409, errorCodes.USER_ALREADY_EXISTS, '用户名已存在', [
        { field: 'username', message: `用户名 '${username}' 已被使用` }
      ]);
    }

    // 检查邮箱是否存在
    const existingEmail = await User.findByEmail(email);
    if (existingEmail) {
      throw new ApiError(409, errorCodes.EMAIL_ALREADY_EXISTS, '邮箱已被注册', [
        { field: 'email', message: `邮箱 '${email}' 已被注册` }
      ]);
    }

    // 创建用户
    const passwordHash = await hashPassword(password);
    const userId = await User.create(username, email, passwordHash, 0);

    // 获取完整用户信息
    const user = await User.findById(userId);
    await User.updateLastLogin(userId);

    // 生成令牌
    const accessToken = generateAccessToken(userId);
    const refreshToken = generateRefreshToken(userId);

    // 保存刷新令牌
    const refreshExpiresAt = new Date(Date.now() + getRefreshTokenExpiration() * 1000);
    await RefreshToken.create(userId, refreshToken, refreshExpiresAt);

    // 注册设备
    if (deviceId) {
      await Device.create(userId, deviceId, null, null, null);
    }

    successResponse(res, {
      user: User.toPublic(user),
      token: {
        accessToken,
        refreshToken,
        expiresIn: getAccessTokenExpiration(),
        tokenType: 'Bearer'
      }
    }, '注册成功', 201);
  } catch (error) {
    next(error);
  }
};

export const login = async (req, res, next) => {
  try {
    const { username, password, deviceId } = req.body;

    // 查找用户
    const user = await User.findByUsernameOrEmail(username);
    if (!user) {
      throw new ApiError(401, errorCodes.INVALID_CREDENTIALS, '用户名或密码错误');
    }

    // 验证密码
    const isValid = await comparePassword(password, user.password_hash);
    if (!isValid) {
      throw new ApiError(401, errorCodes.INVALID_CREDENTIALS, '用户名或密码错误');
    }

    // 更新最后登录时间
    await User.updateLastLogin(user.id);
    const updatedUser = await User.findById(user.id);

    // 生成令牌
    const accessToken = generateAccessToken(user.id);
    const refreshToken = generateRefreshToken(user.id);

    // 保存刷新令牌
    const refreshExpiresAt = new Date(Date.now() + getRefreshTokenExpiration() * 1000);
    await RefreshToken.create(user.id, refreshToken, refreshExpiresAt);

    // 更新设备信息
    if (deviceId) {
      await Device.create(user.id, deviceId, null, null, null);
    }

    successResponse(res, {
      user: User.toPublic(updatedUser),
      token: {
        accessToken,
        refreshToken,
        expiresIn: getAccessTokenExpiration(),
        tokenType: 'Bearer'
      }
    }, '登录成功');
  } catch (error) {
    next(error);
  }
};

export const refresh = async (req, res, next) => {
  try {
    const { refreshToken } = req.body;

    if (!refreshToken) {
      throw new ApiError(400, errorCodes.VALIDATION_ERROR, '缺少刷新令牌');
    }

    // 验证刷新令牌
    const decoded = verifyRefreshToken(refreshToken);
    if (!decoded) {
      throw new ApiError(401, errorCodes.TOKEN_INVALID, '刷新令牌无效或已过期');
    }

    // 检查令牌是否在数据库中
    const tokenRecord = await RefreshToken.findByToken(refreshToken);
    if (!tokenRecord) {
      throw new ApiError(401, errorCodes.TOKEN_INVALID, '刷新令牌无效或已过期');
    }

    // 生成新令牌
    const newAccessToken = generateAccessToken(decoded.userId);
    const newRefreshToken = generateRefreshToken(decoded.userId);

    // 删除旧令牌，保存新令牌
    await RefreshToken.delete(refreshToken);
    const refreshExpiresAt = new Date(Date.now() + getRefreshTokenExpiration() * 1000);
    await RefreshToken.create(decoded.userId, newRefreshToken, refreshExpiresAt);

    successResponse(res, {
      accessToken: newAccessToken,
      refreshToken: newRefreshToken,
      expiresIn: getAccessTokenExpiration(),
      tokenType: 'Bearer'
    }, '令牌刷新成功');
  } catch (error) {
    next(error);
  }
};

export const logout = async (req, res, next) => {
  try {
    const { refreshToken } = req.body;

    if (refreshToken) {
      await RefreshToken.delete(refreshToken);
    }

    successResponse(res, null, '登出成功');
  } catch (error) {
    next(error);
  }
};

export const validate = async (req, res, next) => {
  try {
    const user = req.user;

    successResponse(res, {
      valid: true,
      userId: user.id,
      username: user.username,
      expiresAt: new Date(Date.now() + 3600 * 1000).toISOString()
    }, '令牌有效');
  } catch (error) {
    next(error);
  }
};
