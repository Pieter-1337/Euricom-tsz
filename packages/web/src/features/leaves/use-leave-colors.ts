/**
 * Deterministic palette for leave types, hashed on leaveTypeId.
 * Avoids green (reserved for active nav/focus) and uses neutral-warm hues.
 */

// Neutral-warm hues, design system safe (no euri-green, no conflicting steel-blue)
const PALETTE = [
  { bg: 'bg-amber-500/80', text: 'text-white' },
  { bg: 'bg-blue-500/80', text: 'text-white' },
  { bg: 'bg-violet-500/80', text: 'text-white' },
  { bg: 'bg-rose-500/80', text: 'text-white' },
  { bg: 'bg-cyan-600/80', text: 'text-white' },
  { bg: 'bg-orange-500/80', text: 'text-white' },
  { bg: 'bg-indigo-500/80', text: 'text-white' },
  { bg: 'bg-teal-500/80', text: 'text-white' },
];

function hashId(id: string): number {
  let h = 0;
  for (let i = 0; i < id.length; i++) {
    h = (Math.imul(31, h) + id.charCodeAt(i)) >>> 0;
  }
  return h;
}

export interface LeaveColor {
  bg: string;
  text: string;
}

export function getLeaveColor(leaveTypeId: string): LeaveColor {
  return PALETTE[hashId(leaveTypeId) % PALETTE.length];
}

export function useLeaveColors(leaveTypeIds: string[]): Map<string, LeaveColor> {
  const map = new Map<string, LeaveColor>();
  for (const id of leaveTypeIds) {
    map.set(id, getLeaveColor(id));
  }
  return map;
}
