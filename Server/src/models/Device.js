import { query, queryOne } from '../config/database.js';

export const Device = {
  async create(userId, deviceId, deviceName, deviceType, platform) {
    try {
      await query(
        `INSERT INTO devices (user_id, device_id, device_name, device_type, platform) 
         VALUES (?, ?, ?, ?, ?)
         ON DUPLICATE KEY UPDATE 
         device_name = VALUES(device_name),
         device_type = VALUES(device_type),
         platform = VALUES(platform),
         last_active_at = NOW()`,
        [userId, deviceId, deviceName, deviceType, platform]
      );
    } catch (error) {
      console.error('Device create error:', error);
    }
  },

  async updateLastActive(userId, deviceId) {
    await query(
      'UPDATE devices SET last_active_at = NOW() WHERE user_id = ? AND device_id = ?',
      [userId, deviceId]
    );
  },

  async findByUser(userId) {
    return await query(
      'SELECT * FROM devices WHERE user_id = ? ORDER BY last_active_at DESC',
      [userId]
    );
  },

  async findByUserAndDevice(userId, deviceId) {
    return await queryOne(
      'SELECT * FROM devices WHERE user_id = ? AND device_id = ?',
      [userId, deviceId]
    );
  },

  async delete(userId, deviceId) {
    await query(
      'DELETE FROM devices WHERE user_id = ? AND device_id = ?',
      [userId, deviceId]
    );
  },

  toPublic(device, currentDeviceId = null) {
    if (!device) return null;
    return {
      deviceId: device.device_id,
      deviceName: device.device_name,
      deviceType: device.device_type,
      platform: device.platform,
      lastActiveAt: device.last_active_at,
      createdAt: device.created_at,
      isCurrentDevice: device.device_id === currentDeviceId
    };
  }
};
