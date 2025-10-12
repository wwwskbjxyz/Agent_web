function resolveTimestamp(value?: number | string | Date | null): number | null {
  if (value == null) {
    return null;
  }

  if (value instanceof Date) {
    const ms = value.getTime();
    return Number.isNaN(ms) ? null : ms;
  }

  if (typeof value === 'number') {
    if (!Number.isFinite(value) || value <= 0) {
      return null;
    }
    return value > 1e12 ? value : value * 1000;
  }

  if (typeof value === 'string') {
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

export function formatDateTime(value?: number | string | Date | null, fallback = '-'): string {
  const timestamp = resolveTimestamp(value);
  if (timestamp == null) {
    return fallback;
  }

  const date = new Date(timestamp);
  if (Number.isNaN(date.getTime())) {
    return fallback;
  }

  const pad = (input: number) => input.toString().padStart(2, '0');
  const year = date.getFullYear();
  const month = pad(date.getMonth() + 1);
  const day = pad(date.getDate());
  const hour = pad(date.getHours());
  const minute = pad(date.getMinutes());
  const second = pad(date.getSeconds());

  return `${year}-${month}-${day} ${hour}:${minute}:${second}`;
}

export function formatDate(value?: number | string | Date | null, fallback = '-'): string {
  const timestamp = resolveTimestamp(value);
  if (timestamp == null) {
    return fallback;
  }
  const date = new Date(timestamp);
  if (Number.isNaN(date.getTime())) {
    return fallback;
  }
  const pad = (input: number) => input.toString().padStart(2, '0');
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`;
}

export function formatTime(value?: number | string | Date | null, fallback = '-'): string {
  const timestamp = resolveTimestamp(value);
  if (timestamp == null) {
    return fallback;
  }
  const date = new Date(timestamp);
  if (Number.isNaN(date.getTime())) {
    return fallback;
  }
  const pad = (input: number) => input.toString().padStart(2, '0');
  return `${pad(date.getHours())}:${pad(date.getMinutes())}`;
}

export function formatRelativeHours(hours?: number | string | null): string {
  if (hours == null) {
    return '-';
  }
  const numeric = Number(hours);
  if (!Number.isFinite(numeric)) {
    return '-';
  }
  if (numeric <= 24) {
    return `${numeric.toFixed(1)} 小时`;
  }
  return `${(numeric / 24).toFixed(1)} 天`;
}

export function parseUnixSeconds(value?: number | string | null): number | null {
  if (value == null) {
    return null;
  }
  const numeric = Number(value);
  if (!Number.isFinite(numeric) || numeric <= 0) {
    return null;
  }
  return numeric * 1000;
}

export function isSameDay(a?: number | string | Date | null, b?: number | string | Date | null): boolean {
  const tsA = resolveTimestamp(a);
  const tsB = resolveTimestamp(b);
  if (tsA == null || tsB == null) {
    return false;
  }
  const dateA = new Date(tsA);
  const dateB = new Date(tsB);
  return (
    dateA.getFullYear() === dateB.getFullYear() &&
    dateA.getMonth() === dateB.getMonth() &&
    dateA.getDate() === dateB.getDate()
  );
}

export function toUnixSeconds(value: number | string | Date | null | undefined): number | null {
  const timestamp = resolveTimestamp(value);
  if (timestamp == null) {
    return null;
  }
  return Math.floor(timestamp / 1000);
}

export function formatDurationFromSeconds(value?: number | string | null): string {
  if (value == null) {
    return '';
  }

  const totalSeconds = Number(value);
  if (!Number.isFinite(totalSeconds) || totalSeconds <= 0) {
    return '';
  }

  const seconds = Math.floor(totalSeconds);
  const units: string[] = [];

  const days = Math.floor(seconds / 86400);
  const hours = Math.floor((seconds % 86400) / 3600);
  const minutes = Math.floor((seconds % 3600) / 60);
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

  if (units.length === 1 && remainSeconds > 0 && units[0].includes('分钟') && remainSeconds >= 30) {
    units[0] = `${units[0]}${remainSeconds}秒`;
  }

  if (units.length === 0) {
    return `${remainSeconds}秒`;
  }

  return units.join('');
}
