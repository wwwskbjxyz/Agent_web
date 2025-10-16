"use strict";
function resolveTimestamp(value) {
  if (value == null) {
    return null;
  }
  if (value instanceof Date) {
    const ms = value.getTime();
    return Number.isNaN(ms) ? null : ms;
  }
  if (typeof value === "number") {
    if (!Number.isFinite(value) || value <= 0) {
      return null;
    }
    return value > 1e12 ? value : value * 1e3;
  }
  if (typeof value === "string") {
    const trimmed = value.trim();
    if (!trimmed) {
      return null;
    }
    const numeric = Number(trimmed);
    if (Number.isFinite(numeric)) {
      return resolveTimestamp(numeric);
    }
    const parsed = Date.parse(trimmed);
    if (Number.isNaN(parsed)) {
      return null;
    }
    return parsed;
  }
  return null;
}
function formatDateTime(value, fallback = "-") {
  const timestamp = resolveTimestamp(value);
  if (timestamp == null) {
    return fallback;
  }
  const date = new Date(timestamp);
  if (Number.isNaN(date.getTime())) {
    return fallback;
  }
  const pad = (input) => input.toString().padStart(2, "0");
  const year = date.getFullYear();
  const month = pad(date.getMonth() + 1);
  const day = pad(date.getDate());
  const hour = pad(date.getHours());
  const minute = pad(date.getMinutes());
  const second = pad(date.getSeconds());
  return `${year}-${month}-${day} ${hour}:${minute}:${second}`;
}
function formatDate(value, fallback = "-") {
  const timestamp = resolveTimestamp(value);
  if (timestamp == null) {
    return fallback;
  }
  const date = new Date(timestamp);
  if (Number.isNaN(date.getTime())) {
    return fallback;
  }
  const pad = (input) => input.toString().padStart(2, "0");
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`;
}
function formatTime(value, fallback = "-") {
  const timestamp = resolveTimestamp(value);
  if (timestamp == null) {
    return fallback;
  }
  const date = new Date(timestamp);
  if (Number.isNaN(date.getTime())) {
    return fallback;
  }
  const pad = (input) => input.toString().padStart(2, "0");
  return `${pad(date.getHours())}:${pad(date.getMinutes())}`;
}
function formatDurationFromSeconds(value) {
  if (value == null) {
    return "";
  }
  const totalSeconds = Number(value);
  if (!Number.isFinite(totalSeconds) || totalSeconds <= 0) {
    return "";
  }
  const seconds = Math.floor(totalSeconds);
  const units = [];
  const days = Math.floor(seconds / 86400);
  const hours = Math.floor(seconds % 86400 / 3600);
  const minutes = Math.floor(seconds % 3600 / 60);
  const remainSeconds = seconds % 60;
  if (days > 0) {
    units.push(`${days}天`);
  }
  if (hours > 0) {
    units.push(`${hours}小时`);
  }
  if (minutes > 0 && units.length < 2) {
    units.push(`${minutes}分钟`);
  }
  if (!units.length) {
    if (minutes > 0) {
      units.push(`${minutes}分钟`);
    } else if (remainSeconds > 0) {
      units.push(`${remainSeconds}秒`);
    }
  }
  if (units.length === 1 && remainSeconds > 0 && units[0].includes("分钟") && remainSeconds >= 30) {
    units[0] = `${units[0]}${remainSeconds}秒`;
  }
  if (units.length === 0) {
    return `${remainSeconds}秒`;
  }
  return units.join("");
}
exports.formatDate = formatDate;
exports.formatDateTime = formatDateTime;
exports.formatDurationFromSeconds = formatDurationFromSeconds;
exports.formatTime = formatTime;
//# sourceMappingURL=../../.sourcemap/mp-weixin/utils/time.js.map
