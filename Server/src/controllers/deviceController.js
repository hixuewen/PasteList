import { Device } from '../models/Device.js';
import { successResponse } from '../utils/response.js';
import { ApiError, errorCodes } from '../utils/errors.js';

export const getDevices = async (req, res, next) => {
  try {
    const userId = req.userId;
    const currentDeviceId = req.headers['x-device-id'] || null;

    const devices = await Device.findByUser(userId);
    const devicesData = devices.map(d => Device.toPublic(d, currentDeviceId));

    successResponse(res, { devices: devicesData }, '获取设备列表成功');
  } catch (error) {
    next(error);
  }
};

export const deleteDevice = async (req, res, next) => {
  try {
    const { deviceId } = req.params;
    const userId = req.userId;

    const device = await Device.findByUserAndDevice(userId, deviceId);
    if (!device) {
      throw new ApiError(404, errorCodes.RESOURCE_NOT_FOUND, '设备不存在');
    }

    await Device.delete(userId, deviceId);
    successResponse(res, null, '设备已移除');
  } catch (error) {
    next(error);
  }
};
