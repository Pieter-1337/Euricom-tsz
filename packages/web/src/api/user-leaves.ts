import { type components } from './schema';

export type UserLeave = components['schemas']['UserLeaveDto'];
export type UpdateUserLeavesBody = components['schemas']['UpdateUserLeavesBody'];
export type UpdateUserLeavesItem = components['schemas']['UpdateUserLeavesItem'];

export const LeaveAllowed = {
  NotAllowed: 'NotAllowed',
  Limited: 'Limited',
  Unlimited: 'Unlimited',
} as const satisfies Record<string, components['schemas']['LeaveAllowed']>;
export type LeaveAllowed = (typeof LeaveAllowed)[keyof typeof LeaveAllowed];
export const LEAVE_ALLOWED_VALUES = [LeaveAllowed.NotAllowed, LeaveAllowed.Limited, LeaveAllowed.Unlimited] as const;
