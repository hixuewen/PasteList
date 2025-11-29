import { ClipboardItem } from '../models/ClipboardItem.js';
import { Device } from '../models/Device.js';
import { successResponse } from '../utils/response.js';
import { ApiError, errorCodes } from '../utils/errors.js';

export const createItem = async (req, res, next) => {
  try {
    const { content, deviceId, createdAt } = req.body;
    const userId = req.userId;

    if (!content || content.length > 10000) {
      throw new ApiError(400, errorCodes.VALIDATION_ERROR, '内容不能为空且不能超过10000字符');
    }

    // 更新设备活跃时间
    if (deviceId) {
      await Device.updateLastActive(userId, deviceId);
    }

    const itemId = await ClipboardItem.create(userId, deviceId, content, createdAt);
    const item = await ClipboardItem.findById(itemId, userId);

    successResponse(res, ClipboardItem.toPublic(item), '剪贴板项创建成功', 201);
  } catch (error) {
    next(error);
  }
};

export const createBatch = async (req, res, next) => {
  try {
    const { items } = req.body;
    const userId = req.userId;

    if (!Array.isArray(items) || items.length === 0) {
      throw new ApiError(400, errorCodes.VALIDATION_ERROR, '必须提供至少一个剪贴板项');
    }

    const createdItems = [];
    let failed = 0;

    for (const item of items) {
      try {
        const { content, deviceId, createdAt } = item;
        const itemId = await ClipboardItem.create(userId, deviceId, content, createdAt);
        const createdItem = await ClipboardItem.findById(itemId, userId);
        createdItems.push(ClipboardItem.toPublic(createdItem));
      } catch (error) {
        failed++;
      }
    }

    successResponse(res, {
      created: createdItems.length,
      failed,
      items: createdItems
    }, '批量上传成功', 201);
  } catch (error) {
    next(error);
  }
};

export const getItems = async (req, res, next) => {
  try {
    const userId = req.userId;
    const options = {
      page: parseInt(req.query.page) || 1,
      pageSize: Math.min(parseInt(req.query.pageSize) || 20, 100),
      deviceId: req.query.deviceId,
      search: req.query.search,
      startDate: req.query.startDate,
      endDate: req.query.endDate,
      sortBy: req.query.sortBy || 'created_at',
      sortOrder: req.query.sortOrder || 'desc'
    };

    const result = await ClipboardItem.findByUser(userId, options);

    successResponse(res, {
      items: result.items.map(item => ClipboardItem.toPublic(item)),
      pagination: {
        currentPage: result.page,
        pageSize: result.pageSize,
        totalItems: result.total,
        totalPages: result.totalPages
      }
    }, '获取列表成功');
  } catch (error) {
    next(error);
  }
};

export const getItem = async (req, res, next) => {
  try {
    const { id } = req.params;
    const userId = req.userId;

    const item = await ClipboardItem.findById(parseInt(id), userId);
    if (!item) {
      throw new ApiError(404, errorCodes.RESOURCE_NOT_FOUND, '剪贴板项不存在');
    }

    successResponse(res, ClipboardItem.toPublic(item), '获取成功');
  } catch (error) {
    next(error);
  }
};

export const updateItem = async (req, res, next) => {
  try {
    const { id } = req.params;
    const { content } = req.body;
    const userId = req.userId;

    if (!content || content.length > 10000) {
      throw new ApiError(400, errorCodes.VALIDATION_ERROR, '内容不能为空且不能超过10000字符');
    }

    const item = await ClipboardItem.findById(parseInt(id), userId);
    if (!item) {
      throw new ApiError(404, errorCodes.RESOURCE_NOT_FOUND, '剪贴板项不存在');
    }

    await ClipboardItem.update(parseInt(id), userId, content);
    const updatedItem = await ClipboardItem.findById(parseInt(id), userId);

    successResponse(res, ClipboardItem.toPublic(updatedItem), '更新成功');
  } catch (error) {
    next(error);
  }
};

export const deleteItem = async (req, res, next) => {
  try {
    const { id } = req.params;
    const userId = req.userId;

    const item = await ClipboardItem.findById(parseInt(id), userId);
    if (!item) {
      throw new ApiError(404, errorCodes.RESOURCE_NOT_FOUND, '剪贴板项不存在');
    }

    await ClipboardItem.delete(parseInt(id), userId);
    successResponse(res, null, '删除成功');
  } catch (error) {
    next(error);
  }
};

export const deleteBatch = async (req, res, next) => {
  try {
    const { ids } = req.body;
    const userId = req.userId;

    if (!Array.isArray(ids) || ids.length === 0) {
      throw new ApiError(400, errorCodes.VALIDATION_ERROR, '必须提供至少一个ID');
    }

    const deleted = await ClipboardItem.deleteBatch(ids, userId);

    successResponse(res, {
      deleted,
      failed: ids.length - deleted
    }, '批量删除成功');
  } catch (error) {
    next(error);
  }
};

export const syncItems = async (req, res, next) => {
  try {
    const { deviceId, lastSyncTime, localItems = [] } = req.body;
    const userId = req.userId;

    if (!deviceId) {
      throw new ApiError(400, errorCodes.VALIDATION_ERROR, '缺少设备ID');
    }

    // 更新设备活跃时间
    await Device.updateLastActive(userId, deviceId);

    // 上传本地新增的项
    const uploaded = [];
    for (const localItem of localItems) {
      try {
        const { localId, content, createdAt } = localItem;
        const itemId = await ClipboardItem.create(userId, deviceId, content, createdAt);
        uploaded.push({
          localId,
          serverId: itemId,
          success: true
        });
      } catch (error) {
        uploaded.push({
          localId: localItem.localId,
          success: false,
          error: error.message
        });
      }
    }

    // 获取远程更新（其他设备的数据）
    const sinceTime = lastSyncTime || new Date(0).toISOString();
    const remoteItems = await ClipboardItem.findSinceTime(userId, sinceTime);

    // 过滤掉当前设备的数据
    const filteredRemoteItems = remoteItems
      .filter(item => item.device_id !== deviceId)
      .map(item => ClipboardItem.toPublic(item));

    successResponse(res, {
      syncTime: new Date().toISOString(),
      uploaded,
      remoteItems: filteredRemoteItems
    }, '同步成功');
  } catch (error) {
    next(error);
  }
};
