export type KeysetPage<T> = {
  items: T[];
  nextCursor: string | null;
  total: number;
};

export type SortDir = 'asc' | 'desc';

export type KeysetQueryParams<TSortKey extends string = string> = {
  search?: string;
  sortBy?: TSortKey;
  sortDir?: SortDir;
  pageSize?: number;
  cursor?: string;
  deletedOnly?: boolean;
};
