import { ReactNode } from 'react';

export function PageHeader({ children, actions }: { children?: ReactNode; actions?: ReactNode }) {
  return (
    <div className="mb-6 flex items-center justify-between">
      <div>{children}</div>
      {actions && <div className="flex items-center gap-2">{actions}</div>}
    </div>
  );
}
