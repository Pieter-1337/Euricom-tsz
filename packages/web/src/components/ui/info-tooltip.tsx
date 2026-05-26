import { Info } from 'lucide-react';
import { Tooltip as TooltipPrimitive } from 'radix-ui';
import { cn } from '#/lib/utils';

interface InfoTooltipProps {
  content: React.ReactNode;
  className?: string;
  ariaLabel?: string;
}

export function InfoTooltip({ content, className, ariaLabel = 'More info' }: InfoTooltipProps) {
  return (
    <TooltipPrimitive.Provider delayDuration={200}>
      <TooltipPrimitive.Root>
        <TooltipPrimitive.Trigger asChild>
          <button
            type="button"
            aria-label={ariaLabel}
            className={cn(
              'inline-flex h-4 w-4 cursor-help items-center justify-center rounded text-[#6B7682] hover:text-[#3A4651] dark:text-white/40 dark:hover:text-white/70 focus-visible:outline-[#00FF00] focus-visible:outline-2 focus-visible:outline-offset-1',
              className,
            )}
          >
            <Info className="h-3.5 w-3.5" strokeWidth={1.75} />
          </button>
        </TooltipPrimitive.Trigger>
        <TooltipPrimitive.Portal>
          <TooltipPrimitive.Content
            sideOffset={4}
            className="z-50 rounded-md border bg-popover px-2.5 py-1.5 text-xs text-popover-foreground shadow-md data-[state=closed]:animate-out data-[state=closed]:fade-out-0 data-[state=closed]:zoom-out-95 data-[state=open]:animate-in data-[state=open]:fade-in-0 data-[state=open]:zoom-in-95"
          >
            {content}
            <TooltipPrimitive.Arrow className="fill-popover" />
          </TooltipPrimitive.Content>
        </TooltipPrimitive.Portal>
      </TooltipPrimitive.Root>
    </TooltipPrimitive.Provider>
  );
}
