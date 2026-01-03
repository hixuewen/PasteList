import { query, queryOne } from '../config/database.js';

export const RefreshToken = {
  async create(userId, token, expiresAt) {
    await query(
      'INSERT INTO refresh_tokens (user_id, token, expires_at) VALUES (?, ?, ?)',
      [userId, token, expiresAt]
    );
  },

  async findByToken(token) {
    return await queryOne(
      'SELECT * FROM refresh_tokens WHERE token = ? AND expires_at > NOW()',
      [token]
    );
  },

  async delete(token) {
    await query('DELETE FROM refresh_tokens WHERE token = ?', [token]);
  },

  async deleteByUserId(userId) {
    await query('DELETE FROM refresh_tokens WHERE user_id = ?', [userId]);
  },

  async deleteExpired() {
    await query('DELETE FROM refresh_tokens WHERE expires_at <= NOW()');
  }
};
