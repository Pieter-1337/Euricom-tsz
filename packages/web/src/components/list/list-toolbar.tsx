import { Input } from '#/components/ui/input.tsx';
import { Label } from '#/components/ui/label.tsx';
import { Switch } from '#/components/ui/switch.tsx';

interface DeletedFilterProps {
  deletedOnly: boolean;
  onDeletedOnlyChange: (deletedOnly: boolean) => void;
}

export interface ListToolbarProps {
  search: string;
  onSearchChange: (value: string) => void;
  total: number | undefined;
  deletedFilter?: DeletedFilterProps;
}

export function ListToolbar(props: ListToolbarProps) {
  return (
    <div className="flex flex-1 items-center gap-4">
      <Input
        placeholder="Filter…"
        value={props.search}
        onChange={(e) => props.onSearchChange(e.target.value)}
        className="max-w-sm"
      />
      {props.deletedFilter && (
        <div className="flex items-center gap-2">
          <Switch
            id="list-toolbar-active"
            checked={!props.deletedFilter.deletedOnly}
            onCheckedChange={(checked) => props.deletedFilter!.onDeletedOnlyChange(!checked)}
          />
          <Label htmlFor="list-toolbar-active">Active</Label>
        </div>
      )}
      <span className="ml-auto whitespace-nowrap text-sm text-muted-foreground">{props.total ?? '—'} items</span>
    </div>
  );
}
