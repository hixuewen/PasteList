import { validationResult } from 'express-validator';
import { ApiError, errorCodes } from '../utils/errors.js';

export const validate = (req, res, next) => {
  const errors = validationResult(req);
  
  if (!errors.isEmpty()) {
    const details = errors.array().map(err => ({
      field: err.path || err.param,
      message: err.msg
    }));
    
    throw new ApiError(400, errorCodes.VALIDATION_ERROR, '数据验证失败', details);
  }
  
  next();
};
