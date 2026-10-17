import { useCallback, useEffect, useMemo, useState } from 'react';
import { ApiError } from '../../../api/client';
import { ticketsApi } from '../../../api/tickets';
import type { PagedResult, TicketFilters, TicketListItem } from '../../../types/api';

const DEBOUNCE_MS = 300;

export const emptyFilters: TicketFilters = {
  search: '',
  status: undefined,
  priority: undefined,
  categoryId: undefined,
  customerId: undefined,
  assignedAgentId: undefined,
  unassignedOnly: false,
  page: 1,
  pageSize: 20,
  sortBy: 'createdAtUtc',
  sortDirection: 'desc',
};

interface State {
  isLoading: boolean;
  data?: PagedResult<TicketListItem>;
  error?: string;
}

/** Owns the ticket list's filters and the data they produce. */
export function useTicketList() {
  const [filters, setFilters] = useState<TicketFilters>(emptyFilters);
  const [debouncedSearch, setDebouncedSearch] = useState(filters.search);
  const [state, setState] = useState<State>({ isLoading: true });

  // Debounce search text so keystrokes do not send requests on every letter
  useEffect(() => {
    const handle = window.setTimeout(() => {
      setDebouncedSearch(filters.search);
    }, DEBOUNCE_MS);

    return () => window.clearTimeout(handle);
  }, [filters.search]);

  // Combined query where search is debounced, but all dropdown/other filters take immediate effect
  const queryToFetch = useMemo<TicketFilters>(
    () => ({
      ...filters,
      search: debouncedSearch,
    }),
    [filters, debouncedSearch],
  );

  const load = useCallback(async (query: TicketFilters) => {
    setState((current) => ({ ...current, isLoading: true, error: undefined }));

    try {
      const data = await ticketsApi.getTickets(query);
      setState({ isLoading: false, data, error: undefined });
    } catch (error) {
      const message = error instanceof ApiError ? error.message : 'Unable to load tickets.';
      setState((current) => ({ ...current, isLoading: false, error: message }));
    }
  }, []);

  // Track changes across debounced search and any other filter (including future ones like slaStatus)
  const queryKey = useMemo(() => {
    const nonSearch = { ...filters };
    delete (nonSearch as Partial<TicketFilters>).search;
    return JSON.stringify({ ...nonSearch, debouncedSearch });
  }, [filters, debouncedSearch]);

  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    void load(queryToFetch);
    // eslint-disable-next-line react-hooks/exhaustive-deps -- queryKey controls when filter changes trigger fetch
  }, [queryKey, load]);

  const updateFilters = (patch: Partial<TicketFilters>) =>
    setFilters((current) => ({ ...current, ...patch, page: patch.page ?? 1 }));

  const resetFilters = () => setFilters(emptyFilters);

  return { filters, updateFilters, resetFilters, reload: () => load(queryToFetch), ...state };
}

