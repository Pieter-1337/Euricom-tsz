import { useCallback, useEffect, useRef, useState } from 'react';

export function useInfiniteScrollSentinel(opts: {
  enabled: boolean;
  onIntersect: () => void;
  rootMargin?: string;
}): (node: HTMLElement | null) => void {
  const { enabled, onIntersect, rootMargin = '200px' } = opts;

  const [node, setNode] = useState<HTMLElement | null>(null);
  const onIntersectRef = useRef(onIntersect);
  onIntersectRef.current = onIntersect;

  useEffect(() => {
    if (!enabled || !node) return;

    const observer = new IntersectionObserver(
      (entries) => {
        if (entries[0]?.isIntersecting) {
          onIntersectRef.current();
        }
      },
      { rootMargin },
    );

    observer.observe(node);
    return () => observer.disconnect();
  }, [enabled, node, rootMargin]);

  return useCallback((n: HTMLElement | null) => {
    setNode(n);
  }, []);
}
