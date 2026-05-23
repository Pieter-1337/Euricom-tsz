import { type ComponentType, type ReactNode, type SVGProps } from 'react';
import { cn } from '#/lib/utils';

type IconComponent = ComponentType<SVGProps<SVGSVGElement>>;

export interface EmptyStateProps {
  icon?: IconComponent;
  title: string;
  description?: ReactNode;
  action?: ReactNode;
  className?: string;
}

export function EmptyState({ icon: Icon, title, description, action, className }: EmptyStateProps) {
  return (
    <div className={cn('mx-auto flex max-w-sm flex-col items-center justify-center gap-3 text-center', className)}>
      {Icon && (
        <div className="bg-muted text-muted-foreground flex h-10 w-10 items-center justify-center rounded-full">
          <Icon className="h-5 w-5" strokeWidth={1.75} />
        </div>
      )}
      <div className="space-y-1">
        <p className="text-foreground text-sm font-semibold">{title}</p>
        {description && <p className="text-muted-foreground text-[13px] leading-relaxed">{description}</p>}
      </div>
      {action && <div className="pt-1">{action}</div>}
    </div>
  );
}
