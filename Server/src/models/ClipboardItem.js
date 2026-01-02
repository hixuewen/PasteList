import { query, queryOne } from '../config/database.js';

export const ClipboardItem = {
  // 检查用户是否已存在相同内容的记录
  async existsByContent(userId, content) {
    const existing = await queryOne(
      'SELECT id FROM clipboard_items WHERE user_id = ? AND content = ?',
      [userId, content]
    );
    return existing ? existing.id : null;
  },

  async create(userId, deviceId, content, createdAt = null) {
    // 检查是否已存在相同内容
    const existingId = await ClipboardItem.existsByContent(userId, content);
    if (existingId) {
      // 如果已存在，返回已有记录的 ID
      return { id: existingId, isExisting: true };
    }

    // 将 ISO 8601 格式的时间字符串转换为 Date 对象
    const timestamp = createdAt ? new Date(createdAt) : new Date();
    const result = await query(
      `INSERT INTO clipboard_items (user_id, device_id, content, created_at) 
       VALUES (?, ?, ?, ?)`,
      [userId, deviceId, content, timestamp]
    );
    return { id: result.insertId, isExisting: false };
  },

  async findById(id, userId) {
    return await queryOne(
      'SELECT * FROM clipboard_items WHERE id = ? AND user_id = ?',
      [id, userId]
    );
  },

  async findByUser(userId, options = {}) {
    const {
      page = 1,
      pageSize = 20,
      deviceId = null,
      search = null,
      startDate = null,
      endDate = null,
      sortBy = 'created_at',
      sortOrder = 'desc'
    } = options;

    // 验证 sortBy 字段，防止 SQL 注入
    const allowedSortFields = ['id', 'content', 'created_at', 'updated_at'];
    const validSortBy = allowedSortFields.includes(sortBy) ? sortBy : 'created_at';

    // 验证 sortOrder，防止 SQL 注入
    const validSortOrder = sortOrder.toLowerCase() === 'asc' ? 'ASC' : 'DESC';

    const offset = (page - 1) * pageSize;
    const conditions = ['user_id = ?'];
    const params = [userId];

    if (deviceId) {
      conditions.push('device_id = ?');
      params.push(deviceId);
    }

    if (search) {
      conditions.push('content LIKE ?');
      params.push(`%${search}%`);
    }

    if (startDate) {
      conditions.push('created_at >= ?');
      params.push(startDate);
    }

    if (endDate) {
      conditions.push('created_at <= ?');
      params.push(endDate);
    }

    const whereClause = conditions.join(' AND ');
    const orderClause = `${validSortBy} ${validSortOrder}`;

    const items = await query(
      `SELECT * FROM clipboard_items WHERE ${whereClause} ORDER BY ${orderClause} LIMIT ? OFFSET ?`,
      [...params, pageSize, offset]
    );

    const [{ total }] = await query(
      `SELECT COUNT(*) as total FROM clipboard_items WHERE ${whereClause}`,
      params
    );

    return {
      items,
      total,
      page,
      pageSize,
      totalPages: Math.ceil(total / pageSize)
    };
  },

  async findSinceTime(userId, sinceTime) {
    return await query(
      'SELECT * FROM clipboard_items WHERE user_id = ? AND created_at > ? ORDER BY created_at DESC',
      [userId, sinceTime]
    );
  },

  async update(id, userId, content) {
    await query(
      'UPDATE clipboard_items SET content = ?, updated_at = NOW() WHERE id = ? AND user_id = ?',
      [content, id, userId]
    );
  },

  async delete(id, userId) {
    await query(
      'DELETE FROM clipboard_items WHERE id = ? AND user_id = ?',
      [id, userId]
    );
  },

  toPublic(item) {
    if (!item) return null;
    return {
      id: item.id,
      content: item.content,
      userId: item.user_id,
      deviceId: item.device_id,
      createdAt: item.created_at instanceof Date ? item.created_at.toISOString() : item.created_at,
      updatedAt: item.updated_at instanceof Date ? item.updated_at.toISOString() : item.updated_at
    };
  }
};
