import { query, queryOne } from '../config/database.js';

export const User = {
  async create(username, email, passwordHash) {
    const result = await query(
      'INSERT INTO users (username, email, password_hash) VALUES (?, ?, ?)',
      [username, email, passwordHash]
    );
    return result.insertId;
  },

  async findById(id) {
    return await queryOne('SELECT * FROM users WHERE id = ?', [id]);
  },

  async findByUsername(username) {
    return await queryOne('SELECT * FROM users WHERE username = ?', [username]);
  },

  async findByEmail(email) {
    return await queryOne('SELECT * FROM users WHERE email = ?', [email]);
  },

  async findByUsernameOrEmail(identifier) {
    return await queryOne(
      'SELECT * FROM users WHERE username = ? OR email = ?',
      [identifier, identifier]
    );
  },

  async updateLastLogin(id) {
    await query('UPDATE users SET last_login_at = NOW() WHERE id = ?', [id]);
  },

  async updateEmail(id, email) {
    await query('UPDATE users SET email = ?, updated_at = NOW() WHERE id = ?', [email, id]);
  },

  async updatePassword(id, passwordHash) {
    await query('UPDATE users SET password_hash = ?, updated_at = NOW() WHERE id = ?', [passwordHash, id]);
  },

  toPublic(user) {
    if (!user) return null;
    const { password_hash, ...publicUser } = user;
    return {
      id: publicUser.id,
      username: publicUser.username,
      email: publicUser.email,
      createdAt: publicUser.created_at,
      updatedAt: publicUser.updated_at,
      lastLoginAt: publicUser.last_login_at
    };
  }
};
