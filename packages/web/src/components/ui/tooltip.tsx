import { Tooltip as TooltipPrimitive } from 'radix-ui';
import { cn } from '#/lib/utils';

export const TooltipProvider = TooltipPrimitive.Provider;
export const TooltipRoot = TooltipPrimitive.Root;
export const TooltipTrigger = TooltipPrimitive.Trigger;

export function TooltipContent({
  className,
  sideOffset = 4,
  children,
  ...props
}: React.ComponentPropsWithoutRef<typeof TooltipPrimitive.Content>) {
  return (
    <TooltipPrimitive.Portal>
      <TooltipPrimitive.Content
        sideOffset={sideOffset}
        className={cn(
          'z-50 max-w-xs rounded-md border bg-popover px-2.5 py-1.5 text-xs text-popover-foreground shadow-md',
          'data-[state=closed]:animate-out data-[state=closed]:fade-out-0 data-[state=closed]:zoom-out-95',
          'data-[state=open]:animate-in data-[state=open]:fade-in-0 data-[state=open]:zoom-in-95',
          className,
        )}
        {...props}
      >
        {children}
        <TooltipPrimitive.Arrow className="fill-popover" />
      </TooltipPrimitive.Content>
    </TooltipPrimitive.Portal>
  );
}

export function Tooltip({
  content,
  children,
  delayDuration = 100,
}: {
  content: React.ReactNode;
  children: React.ReactNode;
  delayDuration?: number;
}) {
  return (
    <TooltipRoot delayDuration={delayDuration}>
      <TooltipTrigger asChild>{children}</TooltipTrigger>
      <TooltipContent>{content}</TooltipContent>
    </TooltipRoot>
  );
}
