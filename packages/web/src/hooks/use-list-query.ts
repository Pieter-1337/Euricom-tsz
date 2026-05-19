import { useEffect, useMemo, useState } from 'react';
import { keepPreviousData, useInfiniteQuery } from '@tanstack/react-query';
import type { OnChangeFn, SortingState } from '@tanstack/react-table';
import type { KeysetPage, KeysetQueryParams, SortDir } from '#/api/pagination.ts';
import { useInfiniteScrollSentinel } from '#/hooks/use-infinite-scroll-sentinel';

export function useListQuery<TItem, TSortKey extends string>(opts: {
  queryKey: readonly unknown[];
  fetcher: (params: KeysetQueryParams<TSortKey>) => Promise<KeysetPage<TItem>>;
  defaultSort: { by: TSortKey; dir: SortDir };
  pageSize?: number;
}) {
  const { queryKey, fetcher, defaultSort, pageSize } = opts;

  const [search, setSearch] = useState('');
  const [debouncedSearch, setDebouncedSearch] = useState('');
  const [sorting, setSorting] = useState<SortingState>([{ id: defaultSort.by, desc: defaultSort.dir === 'desc' }]);
  const [deletedOnly, setDeletedOnly] = useState(false);

  useEffect(() => {
    const id = setTimeout(() => setDebouncedSearch(search), 300);
    return () => clearTimeout(id);
  }, [search]);

  const sortBy = (sorting[0]?.id ?? defaultSort.by) as TSortKey;
  const sortDir: SortDir = sorting[0]?.desc ? 'desc' : 'asc';

  const params: KeysetQueryParams<TSortKey> = useMemo(
    () => ({
      search: debouncedSearch || undefined,
      sortBy,
      sortDir,
      pageSize,
      deletedOnly,
    }),
    [debouncedSearch, sortBy, sortDir, pageSize, deletedOnly],
  );

  const query = useInfiniteQuery({
    queryKey: [...queryKey, params],
    queryFn: ({ pageParam }: { pageParam: string | undefined }) => fetcher({ ...params, cursor: pageParam }),
    getNextPageParam: (last: KeysetPage<TItem>) => last.nextCursor ?? undefined,
    initialPageParam: undefined as string | undefined,
    placeholderData: keepPreviousData,
  });

  const { data, isLoading, isFetchingNextPage, error, fetchNextPage, hasNextPage } = query;

  const items = data?.pages.flatMap((p: KeysetPage<TItem>) => p.items) ?? [];
  const total = data?.pages[0]?.total;

  const onSortingChange: OnChangeFn<SortingState> = (updater) => {
    setSorting((prev) => {
      const next = typeof updater === 'function' ? updater(prev) : updater;
      return next.length === 0 ? prev : next;
    });
  };

  const sentinelRef = useInfiniteScrollSentinel({
    enabled: !!(hasNextPage && !isFetchingNextPage),
    onIntersect: () => void fetchNextPage(),
  });

  return {
    items,
    total,
    search,
    setSearch,
    sorting,
    onSortingChange,
    deletedOnly,
    setDeletedOnly,
    sentinelRef,
    isLoading,
    isFetchingNextPage,
    error,
  };
}
