import { useState } from 'react';
import { Calendar, ChevronLeft, ChevronRight } from 'lucide-react';
import { Button } from '#/components/ui/button';
import { Calendar as CalendarPicker } from '#/components/ui/calendar';
import { Popover, PopoverContent, PopoverTrigger } from '#/components/ui/popover';
import { dateToIsoWeek, isoWeekToMonday, nextWeek, prevWeek, todayWeek } from '#/features/timesheets/iso-week';
import type { TimesheetWeek } from '#/api/timesheets';

type Status = TimesheetWeek['status'];

interface WeekActionsBarProps {
  year: number;
  week: number;
  status: Status;
  isDraft: boolean;
  isAdmin: boolean;
  isReadOnly: boolean;
  isDirty: boolean;
  isFlushing: boolean;
  isLifecycleLoading: boolean;
  hasDayCapacityError: boolean;
  hasInvalidCellInput: boolean;
  navExtras?: React.ReactNode;
  onNavigateToWeek: (year: number, week: number) => void;
  onSave: () => void;
  onSubmit: () => void;
  onApprove: () => void;
  onReopen: () => void;
}

export function WeekActionsBar({
  year,
  week,
  status,
  isDraft,
  isAdmin,
  isReadOnly,
  isDirty,
  isFlushing,
  isLifecycleLoading,
  hasDayCapacityError,
  hasInvalidCellInput,
  navExtras,
  onNavigateToWeek,
  onSave,
  onSubmit,
  onApprove,
  onReopen,
}: WeekActionsBarProps) {
  const [showCalendar, setShowCalendar] = useState(false);
  const { year: prevYear, week: prevWeekNum } = prevWeek(year, week);
  const { year: nextYear, week: nextWeekNum } = nextWeek(year, week);
  const today = todayWeek();

  return (
    <div className="flex items-center justify-between">
      <div className="flex items-center gap-2">
        <Button variant="outline" size="sm" onClick={() => onNavigateToWeek(prevYear, prevWeekNum)}>
          <ChevronLeft className="h-4 w-4" />
        </Button>
        <span className="min-w-[120px] text-center text-sm font-semibold">
          Week {week}, {year}
        </span>
        <Button variant="outline" size="sm" onClick={() => onNavigateToWeek(nextYear, nextWeekNum)}>
          <ChevronRight className="h-4 w-4" />
        </Button>
        <Button
          variant="ghost"
          size="sm"
          className="ml-1 text-xs"
          onClick={() => onNavigateToWeek(today.year, today.week)}
        >
          Today
        </Button>
        <Popover open={showCalendar} onOpenChange={setShowCalendar}>
          <PopoverTrigger asChild>
            <Button variant="ghost" size="sm" className="ml-1" title="Pick a week">
              <Calendar className="h-4 w-4" />
            </Button>
          </PopoverTrigger>
          <PopoverContent className="w-auto p-0" align="start">
            <CalendarPicker
              mode="single"
              defaultMonth={isoWeekToMonday(year, week)}
              onSelect={(d) => {
                if (d) {
                  const { year: y, week: w } = dateToIsoWeek(d);
                  setShowCalendar(false);
                  onNavigateToWeek(y, w);
                }
              }}
              autoFocus
            />
          </PopoverContent>
        </Popover>
        {navExtras}
      </div>

      <div className="flex items-center gap-2">
        {!isReadOnly && isDirty && <span className="text-xs text-amber-500">Unsaved changes</span>}
        {!isReadOnly && isFlushing && <span className="text-xs text-[#6B7682]">Saving...</span>}
        {!isReadOnly && isDraft && (
          <Button
            variant="outline"
            size="sm"
            disabled={!isDirty || isFlushing || isLifecycleLoading || hasDayCapacityError || hasInvalidCellInput}
            onClick={onSave}
          >
            Save
          </Button>
        )}
        {!isReadOnly && isDraft && (
          <Button
            size="sm"
            disabled={isLifecycleLoading || hasDayCapacityError || hasInvalidCellInput}
            onClick={onSubmit}
          >
            Submit
          </Button>
        )}
        {isAdmin && status === 'Submitted' && (
          <Button size="sm" disabled={isLifecycleLoading} onClick={onApprove}>
            Approve
          </Button>
        )}
        {isAdmin && (status === 'Submitted' || status === 'Approved') && (
          <Button variant="outline" size="sm" disabled={isLifecycleLoading} onClick={onReopen}>
            Reopen
          </Button>
        )}
      </div>
    </div>
  );
}
