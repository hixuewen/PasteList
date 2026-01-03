import mysql from 'mysql2/promise';
import { config } from './config.js';

let pool = null;

export const getPool = () => {
  if (!pool) {
    pool = mysql.createPool(config.db);
  }
  return pool;
};

export const query = async (sql, params) => {
  const connection = await getPool().getConnection();
  try {
    const [rows] = await connection.execute(sql, params);
    return rows;
  } finally {
    connection.release();
  }
};

export const queryOne = async (sql, params) => {
  const rows = await query(sql, params);
  return rows[0] || null;
};

export const transaction = async (callback) => {
  const connection = await getPool().getConnection();
  try {
    await connection.beginTransaction();
    const result = await callback(connection);
    await connection.commit();
    return result;
  } catch (error) {
    await connection.rollback();
    throw error;
  } finally {
    connection.release();
  }
};
