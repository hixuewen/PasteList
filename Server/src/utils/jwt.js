import jwt from 'jsonwebtoken';
import { config } from '../config/config.js';

export const generateAccessToken = (userId) => {
  return jwt.sign({ userId, type: 'access' }, config.jwt.secret, { expiresIn: config.jwt.accessExpire });
};

export const generateRefreshToken = (userId) => {
  return jwt.sign({ userId, type: 'refresh' }, config.jwt.refreshSecret, { expiresIn: config.jwt.refreshExpire });
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

export const getAccessTokenExpiration = () => {
  return config.jwt.accessExpire;
};

export const getRefreshTokenExpiration = () => {
  return config.jwt.refreshExpire;
};
