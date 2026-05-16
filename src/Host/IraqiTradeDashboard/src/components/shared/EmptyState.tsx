import { LucideIcon } from 'lucide-react';
import { ReactNode } from 'react';
import { cn } from '@/lib/utils';

export function EmptyState({
  icon: Icon, title, description, action, className,
}: {
  icon: LucideIcon;
  title: string;
  description?: string;
  action?: ReactNode;
  className?: string;
}) {
  return (
    <div className={cn(
      'flex flex-col items-center justify-center rounded-lg border border-dashed border-border/60 bg-card/30 p-12 text-center',
      className
    )}>
      <div className="mb-4 flex h-14 w-14 items-center justify-center rounded-full bg-secondary/60 ring-1 ring-border">
        <Icon className="h-6 w-6 text-muted-foreground" />
      </div>
      <h3 className="font-display text-lg font-medium">{title}</h3>
      {description && <p className="mt-1.5 max-w-md text-sm text-muted-foreground">{description}</p>}
      {action && <div className="mt-5">{action}</div>}
    </div>
  );
}
