import bcrypt from 'bcryptjs';

const SALT_ROUNDS = 10;

export const hashPassword = async (password) => {
  return await bcrypt.hash(password, SALT_ROUNDS);
};

export const comparePassword = async (password, hash) => {
  return await bcrypt.compare(password, hash);
};

export const validatePassword = (password) => {
  // 8-32个字符，至少包含大小写字母和数字
  const regex = /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{8,32}$/;
  return regex.test(password);
};

export const validateUsername = (username) => {
  // 3-20个字符，只允许字母、数字、下划线
  const regex = /^[a-zA-Z0-9_]{3,20}$/;
  return regex.test(username);
};

export const validateEmail = (email) => {
  const regex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
  return regex.test(email);
};
