import type { ComponentProps } from 'react';

import { Input } from '#/components/ui/input';

type DateInputProps = Omit<ComponentProps<typeof Input>, 'type'>;

function DateInput(props: DateInputProps) {
  return <Input type="date" {...props} />;
}

export { DateInput };
