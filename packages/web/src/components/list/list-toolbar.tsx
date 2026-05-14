import { Input } from '#/components/ui/input.tsx';
import { Label } from '#/components/ui/label.tsx';
import { Switch } from '#/components/ui/switch.tsx';

interface ToggleProps {
  label: string;
  checked: boolean;
  onChange: (checked: boolean) => void;
}

interface ListToolbarProps {
  search: string;
  onSearchChange: (value: string) => void;
  total: number | undefined;
  toggle?: ToggleProps;
}

export function ListToolbar({ search, onSearchChange, total, toggle }: ListToolbarProps) {
  return (
    <div className="flex flex-1 items-center gap-4">
      <Input
        placeholder="Filter…"
        value={search}
        onChange={(e) => onSearchChange(e.target.value)}
        className="max-w-sm"
      />
      {toggle && (
        <div className="flex items-center gap-2">
          <Switch
            id="list-toolbar-toggle"
            checked={toggle.checked}
            onCheckedChange={toggle.onChange}
          />
          <Label htmlFor="list-toolbar-toggle">{toggle.label}</Label>
        </div>
      )}
      <span className="ml-auto whitespace-nowrap text-sm text-muted-foreground">
        {total ?? '—'} items
      </span>
    </div>
  );
}
