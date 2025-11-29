import { verifyAccessToken } from '../utils/jwt.js';
import { ApiError, errorCodes } from '../utils/errors.js';
import { User } from '../models/User.js';

export const authenticate = async (req, res, next) => {
  try {
    const authHeader = req.headers.authorization;
    
    if (!authHeader || !authHeader.startsWith('Bearer ')) {
      throw new ApiError(401, errorCodes.UNAUTHORIZED, '未提供认证令牌');
    }

    const token = authHeader.substring(7);
    const decoded = verifyAccessToken(token);

    if (!decoded) {
      throw new ApiError(401, errorCodes.TOKEN_INVALID, '访问令牌无效或已过期');
    }

    const user = await User.findById(decoded.userId);
    if (!user) {
      throw new ApiError(401, errorCodes.USER_NOT_FOUND, '用户不存在');
    }

    req.user = user;
    req.userId = user.id;
    next();
  } catch (error) {
    if (error instanceof ApiError) {
      next(error);
    } else {
      next(new ApiError(401, errorCodes.UNAUTHORIZED, '认证失败'));
    }
  }
};
