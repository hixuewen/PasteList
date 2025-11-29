import { errorResponse } from '../utils/response.js';
import { ApiError, errorCodes } from '../utils/errors.js';

export const errorHandler = (err, req, res, next) => {
  console.error('Error:', err);

  if (err instanceof ApiError) {
    return errorResponse(res, err);
  }

  // 处理未知错误
  const error = new ApiError(500, errorCodes.INTERNAL_ERROR, '服务器内部错误');
  return errorResponse(res, error);
};

export const notFound = (req, res) => {
  const error = new ApiError(404, errorCodes.RESOURCE_NOT_FOUND, '请求的资源不存在');
  return errorResponse(res, error);
};
