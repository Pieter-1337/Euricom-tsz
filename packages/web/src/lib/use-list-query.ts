import { useEffect, useState } from 'react';
import { keepPreviousData, useInfiniteQuery } from '@tanstack/react-query';
import type { KeysetPage, KeysetQueryParams, SortDir } from '#/api/pagination.ts';
import { useInfiniteScrollSentinel } from '#/lib/use-infinite-scroll-sentinel.ts';

export function useListQuery<TItem, TSortKey extends string>(opts: {
  queryKey: readonly unknown[];
  fetcher: (params: KeysetQueryParams<TSortKey>) => Promise<KeysetPage<TItem>>;
  defaultSort: { by: TSortKey; dir: SortDir };
  pageSize?: number;
}) {
  const { queryKey, fetcher, defaultSort, pageSize } = opts;

  const [search, setSearch] = useState('');
  const [debouncedSearch, setDebouncedSearch] = useState('');
  const [sortBy, setSortBy] = useState<TSortKey>(defaultSort.by);
  const [sortDir, setSortDir] = useState<SortDir>(defaultSort.dir);
  const [deletedOnly, setDeletedOnly] = useState(false);

  useEffect(() => {
    const id = setTimeout(() => setDebouncedSearch(search), 300);
    return () => clearTimeout(id);
  }, [search]);

  const params: KeysetQueryParams<TSortKey> = {
    search: debouncedSearch || undefined,
    sortBy,
    sortDir,
    pageSize,
    deletedOnly,
  };

  const query = useInfiniteQuery({
    queryKey: [...queryKey, params],
    queryFn: ({ pageParam }: { pageParam: string | undefined }) =>
      fetcher({ ...params, cursor: pageParam }),
    getNextPageParam: (last: KeysetPage<TItem>) => last.nextCursor ?? undefined,
    initialPageParam: undefined as string | undefined,
    placeholderData: keepPreviousData,
  });

  const { data, isLoading, isFetchingNextPage, error, fetchNextPage, hasNextPage } = query;

  const items = data?.pages.flatMap((p: KeysetPage<TItem>) => p.items) ?? [];
  const total = data?.pages[0]?.total;

  function setSort(by: TSortKey) {
    if (by === sortBy) {
      setSortDir((d) => (d === 'asc' ? 'desc' : 'asc'));
    } else {
      setSortBy(by);
      setSortDir('asc');
    }
  }

  const sentinelRef = useInfiniteScrollSentinel({
    enabled: !!(hasNextPage && !isFetchingNextPage),
    onIntersect: () => void fetchNextPage(),
  });

  return {
    items,
    total,
    search,
    setSearch,
    sortBy,
    sortDir,
    setSort,
    deletedOnly,
    setDeletedOnly,
    sentinelRef,
    isLoading,
    isFetchingNextPage,
    error,
  };
}
