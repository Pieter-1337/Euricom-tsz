import { type components } from './schema';

export type LeaveType = components['schemas']['LeaveTypeDto'];

export const LeaveAllowed = {
  NotAllowed: 'NotAllowed',
  Limited: 'Limited',
  Unlimited: 'Unlimited',
} as const satisfies Record<string, components['schemas']['LeaveAllowed']>;
export type LeaveAllowed = (typeof LeaveAllowed)[keyof typeof LeaveAllowed];
export const LEAVE_ALLOWED_VALUES = [
  LeaveAllowed.NotAllowed,
  LeaveAllowed.Limited,
  LeaveAllowed.Unlimited,
] as const;
