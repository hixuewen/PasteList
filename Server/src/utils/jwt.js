import jwt from 'jsonwebtoken';
import { config } from '../config/config.js';

export const generateAccessToken = (userId, rememberMe = false) => {
  const expiresIn = rememberMe ? config.jwt.accessExpireRemember : config.jwt.accessExpire;
  return jwt.sign({ userId, type: 'access' }, config.jwt.secret, { expiresIn });
};

export const generateRefreshToken = (userId, rememberMe = false) => {
  const expiresIn = rememberMe ? config.jwt.refreshExpireRemember : config.jwt.refreshExpire;
  return jwt.sign({ userId, type: 'refresh' }, config.jwt.refreshSecret, { expiresIn });
};

export const verifyAccessToken = (token) => {
  try {
    const decoded = jwt.verify(token, config.jwt.secret);
    if (decoded.type !== 'access') {
      throw new Error('Invalid token type');
    }
    return decoded;
  } catch (error) {
    return null;
  }
};

export const verifyRefreshToken = (token) => {
  try {
    const decoded = jwt.verify(token, config.jwt.refreshSecret);
    if (decoded.type !== 'refresh') {
      throw new Error('Invalid token type');
    }
    return decoded;
  } catch (error) {
    return null;
  }
};

export const getTokenExpiration = (rememberMe = false, isRefresh = false) => {
  if (isRefresh) {
    return rememberMe ? config.jwt.refreshExpireRemember : config.jwt.refreshExpire;
  }
  return rememberMe ? config.jwt.accessExpireRemember : config.jwt.accessExpire;
};
